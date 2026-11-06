using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Pgvector;
using Tdmu.Advisor.Api.Features.KnowledgeBase;
using Tdmu.Advisor.Api.Features.KnowledgeBase.Query;
using Tdmu.Advisor.Api.Infrastructure;
using Tdmu.Advisor.Api.Infrastructure.Gemini;

namespace Tdmu.Advisor.Api.Features.Agent.Search;

public partial class KnowledgeSearch(AdvisorDbContext db, IKnowledgeQuery kb, IGeminiClient gemini, IOptions<RagOptions> options) : IKnowledgeSearch
{
    private const int Candidates = 20, RrfK = 60, MaxOrTerms = 32;
    private const double CurrentYearBoost = 1.1;

    private sealed record FaqRow(string TraLoi, string? NguonTen, string? NguonUrl, double Sim);
    private sealed record ChunkRow(Guid Id, string NoiDung, string? ViTri, string TieuDe, string? NguonTen, string? NguonUrl, int? Nam);

    [GeneratedRegex(@"[\p{L}\p{N}]{2,}")]
    private static partial Regex Terms();

    public async Task<SearchResult> SearchAsync(string cauHoi, KbScope scope, CancellationToken ct)
    {
        var rag = options.Value;
        if (string.IsNullOrWhiteSpace(cauHoi)) return new("khong_co", null, []);
        var rid = await kb.ResolveReleaseAsync(scope, ct);
        var vec = new Vector((await gemini.EmbedAsync([cauHoi], EmbeddingTask.RetrievalQuery, ct))[0]);

        // HNSW lọc release_id SAU khi lấy ~ef_search ứng viên; mỗi release giữ bản sao đầy đủ nên cần quét lặp để đủ kết quả trong phạm vi.
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await db.Database.ExecuteSqlRawAsync("SET LOCAL hnsw.iterative_scan = relaxed_order", ct);
        await db.Database.ExecuteSqlRawAsync("SET LOCAL hnsw.ef_search = 100", ct);

        // relaxed_order có thể trả kết quả lệch thứ tự nhẹ: sắp xếp lại theo khoảng cách ở truy vấn ngoài.
        var faq = (await db.Database.SqlQuery<FaqRow>($"""
            SELECT tra_loi, nguon_ten, nguon_url, (1 - d)::float8 AS sim FROM (
                SELECT tra_loi, nguon_ten, nguon_url, embedding <=> {vec} AS d
                FROM faq WHERE release_id IS NOT DISTINCT FROM {rid}::uuid AND embedding IS NOT NULL
                ORDER BY embedding <=> {vec} LIMIT 1) s
            ORDER BY d
            """).ToListAsync(ct)).FirstOrDefault();
        if (faq is not null && faq.Sim >= rag.FaqThreshold)
            return new("faq", faq.TraLoi, [new(faq.TraLoi, faq.NguonTen ?? "Câu hỏi thường gặp", faq.NguonUrl, null, faq.Sim)]);

        var byVector = await db.Database.SqlQuery<Guid>($"""
            SELECT id AS "Value" FROM (
                SELECT id, embedding <=> {vec} AS d FROM tai_lieu_chunk
                WHERE release_id IS NOT DISTINCT FROM {rid}::uuid AND embedding IS NOT NULL
                ORDER BY embedding <=> {vec} LIMIT {Candidates}) s
            ORDER BY d
            """).ToListAsync(ct);
        await tx.CommitAsync(ct);

        var byText = await db.Database.SqlQuery<Guid>($"""
            SELECT id AS "Value" FROM tai_lieu_chunk
            WHERE release_id IS NOT DISTINCT FROM {rid}::uuid AND tsv @@ plainto_tsquery('simple', unaccent({cauHoi}))
            ORDER BY ts_rank(tsv, plainto_tsquery('simple', unaccent({cauHoi}))) DESC LIMIT {Candidates}
            """).ToListAsync(ct);
        if (byText.Count == 0)
        {
            // plainto_tsquery nối các từ bằng AND nên câu hỏi dài hay không khớp: thử lại với OR. unaccent có thể bung ký tự (vd "½" -> "1/2"),
            // nên SQL lọc lại từng từ đã unaccent về chữ/số rồi mới nối bằng " | " (không bao giờ sinh cú pháp tsquery).
            var any = string.Join(" ", Terms().Matches(cauHoi).Select(m => m.Value).Distinct().Take(MaxOrTerms));
            if (any.Length > 0)
                byText = await db.Database.SqlQuery<Guid>($"""
                    WITH q AS (
                        SELECT to_tsquery('simple', array_to_string(ARRAY(
                            SELECT t FROM unnest(regexp_split_to_array(regexp_replace(unaccent({any}), '[^[:alnum:]]+', ' ', 'g'), ' ')) t
                            WHERE length(t) >= 2 LIMIT {MaxOrTerms}), ' | ')) AS tq)
                    SELECT c.id AS "Value" FROM tai_lieu_chunk c, q
                    WHERE c.release_id IS NOT DISTINCT FROM {rid}::uuid AND c.tsv @@ q.tq
                    ORDER BY ts_rank(c.tsv, q.tq) DESC LIMIT {Candidates}
                    """).ToListAsync(ct);
        }

        var fused = Rrf.Fuse([byVector, byText], RrfK);
        if (fused.Count == 0) return new("khong_co", null, []);

        var ids = fused.Select(x => x.Id).ToArray();
        var rows = (await db.Database.SqlQuery<ChunkRow>($"""
            SELECT c.id AS id, c.noi_dung AS noi_dung, c.vi_tri AS vi_tri, t.tieu_de AS tieu_de,
                   t.nguon_ten AS nguon_ten, t.nguon_url AS nguon_url, t.nam_ap_dung AS nam
            FROM tai_lieu_chunk c
            JOIN tai_lieu t ON t.logical_id = c.tai_lieu_lid AND t.release_id IS NOT DISTINCT FROM c.release_id
            WHERE c.id = ANY({ids})
            """).ToListAsync(ct)).ToDictionary(x => x.Id);

        var year = await kb.CurrentYearAsync(scope, ct);
        var hits = fused.Where(x => rows.ContainsKey(x.Id))
            .Select(x =>
            {
                var r = rows[x.Id];
                var score = year is not null && r.Nam == year ? x.Score * CurrentYearBoost : x.Score;
                return new SearchHit(r.NoiDung, r.NguonTen ?? r.TieuDe, r.NguonUrl, r.ViTri, score);
            })
            .OrderByDescending(h => h.Score).Take(rag.TopK).ToList();
        return hits.Count == 0 ? new("khong_co", null, []) : new("tai_lieu", null, hits);
    }
}
