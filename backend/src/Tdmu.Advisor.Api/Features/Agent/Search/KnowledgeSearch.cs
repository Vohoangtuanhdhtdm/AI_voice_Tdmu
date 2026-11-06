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
    private const int Candidates = 20, RrfK = 60;
    private const double CurrentYearBoost = 1.1;

    private sealed record FaqRow(string TraLoi, string? NguonTen, string? NguonUrl, double Sim);
    private sealed record ChunkRow(Guid Id, string NoiDung, string? ViTri, string TieuDe, string? NguonTen, string? NguonUrl, int? Nam);

    [GeneratedRegex(@"[\p{L}\p{N}]{2,}")]
    private static partial Regex Terms();

    public async Task<SearchResult> SearchAsync(string cauHoi, KbScope scope, CancellationToken ct)
    {
        var rag = options.Value;
        var rid = await kb.ResolveReleaseAsync(scope, ct);
        var vec = new Vector((await gemini.EmbedAsync([cauHoi], EmbeddingTask.RetrievalQuery, ct))[0]);

        var faq = (await db.Database.SqlQuery<FaqRow>($"""
            SELECT tra_loi AS tra_loi, nguon_ten AS nguon_ten, nguon_url AS nguon_url, (1 - (embedding <=> {vec}))::float8 AS sim
            FROM faq WHERE release_id IS NOT DISTINCT FROM {rid}::uuid AND embedding IS NOT NULL
            ORDER BY embedding <=> {vec} LIMIT 1
            """).ToListAsync(ct)).FirstOrDefault();
        if (faq is not null && faq.Sim >= rag.FaqThreshold)
            return new("faq", faq.TraLoi, [new(faq.TraLoi, faq.NguonTen ?? "Câu hỏi thường gặp", faq.NguonUrl, null, faq.Sim)]);

        var byVector = await db.Database.SqlQuery<Guid>($"""
            SELECT id AS "Value" FROM tai_lieu_chunk
            WHERE release_id IS NOT DISTINCT FROM {rid}::uuid AND embedding IS NOT NULL
            ORDER BY embedding <=> {vec} LIMIT {Candidates}
            """).ToListAsync(ct);

        var byText = await db.Database.SqlQuery<Guid>($"""
            SELECT id AS "Value" FROM tai_lieu_chunk
            WHERE release_id IS NOT DISTINCT FROM {rid}::uuid AND tsv @@ plainto_tsquery('simple', unaccent({cauHoi}))
            ORDER BY ts_rank(tsv, plainto_tsquery('simple', unaccent({cauHoi}))) DESC LIMIT {Candidates}
            """).ToListAsync(ct);
        if (byText.Count == 0)
        {
            // plainto_tsquery nối các từ bằng AND nên câu hỏi dài hay không khớp: thử lại với OR (từ chỉ gồm chữ/số nên an toàn cho to_tsquery).
            var any = string.Join(" | ", Terms().Matches(cauHoi).Select(m => m.Value).Distinct());
            if (any.Length > 0)
                byText = await db.Database.SqlQuery<Guid>($"""
                    SELECT id AS "Value" FROM tai_lieu_chunk
                    WHERE release_id IS NOT DISTINCT FROM {rid}::uuid AND tsv @@ to_tsquery('simple', unaccent({any}))
                    ORDER BY ts_rank(tsv, to_tsquery('simple', unaccent({any}))) DESC LIMIT {Candidates}
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
