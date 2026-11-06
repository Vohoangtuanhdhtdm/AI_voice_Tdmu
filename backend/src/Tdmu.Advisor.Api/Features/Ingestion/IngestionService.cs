using Microsoft.EntityFrameworkCore;
using Pgvector;
using Tdmu.Advisor.Api.Features.KnowledgeBase.Entities;
using Tdmu.Advisor.Api.Infrastructure;
using Tdmu.Advisor.Api.Infrastructure.Auditing;
using Tdmu.Advisor.Api.Infrastructure.Gemini;
using Tdmu.Advisor.Api.Infrastructure.Storage;

namespace Tdmu.Advisor.Api.Features.Ingestion;

public class IngestionService(
    AdvisorDbContext db, IFileStore store, TextExtractor extractor, IGeminiClient gemini,
    AuditSuppression suppression, ILogger<IngestionService> log)
{
    private const int EmbedBatchSize = 50;
    private const int MaxErrorChars = 300;

    /// <summary>Nhận và xử lý các tài liệu nháp đang ChoXuLy. Trả về số tài liệu mà lần gọi này đã nhận xử lý (kể cả thất bại).</summary>
    public async Task<int> ProcessPendingDocumentsAsync(CancellationToken ct)
    {
        var ids = await db.TaiLieu.AsNoTracking()
            .Where(x => x.ReleaseId == null && x.TrangThai == TrangThaiTaiLieu.ChoXuLy)
            .OrderBy(x => x.CapNhatLuc).Select(x => x.Id).ToListAsync(ct);

        var processed = 0;
        foreach (var id in ids)
        {
            // Nhận việc nguyên tử: chỉ một worker chuyển được ChoXuLy → DangXuLy.
            var claimed = await db.TaiLieu
                .Where(x => x.Id == id && x.ReleaseId == null && x.TrangThai == TrangThaiTaiLieu.ChoXuLy)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.TrangThai, TrangThaiTaiLieu.DangXuLy)
                    .SetProperty(x => x.Loi, (string?)null), ct);
            if (claimed == 0) continue;
            processed++;
            await ProcessOneAsync(id, ct);
        }
        return processed;
    }

    private async Task ProcessOneAsync(Guid id, CancellationToken ct)
    {
        db.ChangeTracker.Clear();
        try
        {
            var doc = await db.TaiLieu.AsNoTracking().FirstAsync(x => x.Id == id, ct);
            var data = await store.ReadAsync(doc.DuongDanFile, ct);
            var blocks = await extractor.ExtractAsync(data, Path.GetExtension(doc.DuongDanFile), ct);
            var drafts = Chunker.Split(blocks);
            if (drafts.Count == 0) { await FailAsync(id, "Không trích được nội dung từ file"); return; }

            var vectors = await EmbedAsync(drafts.Select(d => d.NoiDung).ToList(), ct);
            await ReplaceChunksAsync(doc, drafts, vectors, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            await db.TaiLieu.Where(x => x.Id == id && x.TrangThai == TrangThaiTaiLieu.DangXuLy)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.TrangThai, TrangThaiTaiLieu.ChoXuLy), CancellationToken.None);
            throw;
        }
        catch (Exception ex)
        {
            log.LogWarning(ex, "Xử lý tài liệu {Id} thất bại", id);
            await FailAsync(id, ex is GeminiNotConfiguredException ? "Chưa cấu hình Gemini API key" : "Lỗi xử lý: " + Shorten(ex.Message));
        }
    }

    private async Task ReplaceChunksAsync(TaiLieu doc, IReadOnlyList<ChunkDraft> drafts, IReadOnlyList<float[]> vectors, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        db.ChangeTracker.Clear();
        // Chunk là dữ liệu suy ra từ file nên không ghi audit từng dòng (mỗi tài liệu có thể có hàng trăm chunk).
        using (suppression.Begin())
        {
            await db.TaiLieuChunk.Where(x => x.ReleaseId == null && x.TaiLieuLid == doc.LogicalId).ExecuteDeleteAsync(ct);
            for (var i = 0; i < drafts.Count; i++)
                db.TaiLieuChunk.Add(new TaiLieuChunk
                {
                    TaiLieuLid = doc.LogicalId, ThuTu = drafts[i].ThuTu, NoiDung = drafts[i].NoiDung,
                    ViTri = drafts[i].ViTri.Length > 0 ? drafts[i].ViTri : null,
                    NguonTen = doc.NguonTen, NguonUrl = doc.NguonUrl,
                    Embedding = new Vector(vectors[i]),
                });
            await db.SaveChangesAsync(ct);
        }
        var lid = doc.LogicalId;
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE tai_lieu_chunk SET tsv = to_tsvector('simple', unaccent(noi_dung)) WHERE release_id IS NULL AND tai_lieu_lid = {lid}", ct);
        var done = await db.TaiLieu.Where(x => x.Id == doc.Id && x.TrangThai == TrangThaiTaiLieu.DangXuLy)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.TrangThai, TrangThaiTaiLieu.Xong).SetProperty(x => x.Loi, (string?)null), ct);
        if (done == 0) return; // Tài liệu đã bị xóa hoặc đặt lại khi đang xử lý: hủy giao dịch, bỏ chunk vừa tạo.
        await tx.CommitAsync(ct);
    }

    private async Task FailAsync(Guid id, string message)
    {
        db.ChangeTracker.Clear();
        await db.TaiLieu.Where(x => x.Id == id)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.TrangThai, TrangThaiTaiLieu.ThatBai).SetProperty(x => x.Loi, message), CancellationToken.None);
    }

    private static string Shorten(string message)
    {
        var line = message.Split('\n', 2)[0].Trim();
        return line.Length > MaxErrorChars ? line[..MaxErrorChars] : line;
    }

    private async Task<List<float[]>> EmbedAsync(IReadOnlyList<string> texts, CancellationToken ct)
    {
        var all = new List<float[]>(texts.Count);
        for (var i = 0; i < texts.Count; i += EmbedBatchSize)
            all.AddRange(await gemini.EmbedAsync(texts.Skip(i).Take(EmbedBatchSize).ToList(), EmbeddingTask.RetrievalDocument, ct));
        if (all.Count != texts.Count) throw new InvalidOperationException("Số embedding trả về không khớp số đoạn văn");
        return all;
    }

    /// <summary>Embed các FAQ nháp chưa có embedding. Trả về số FAQ đã được gán embedding.</summary>
    public async Task<int> EmbedPendingFaqsAsync(CancellationToken ct)
    {
        var pending = await db.Faq.AsNoTracking().Where(x => x.ReleaseId == null && x.Embedding == null)
            .OrderBy(x => x.CapNhatLuc).Take(200)
            .Select(x => new { x.Id, x.CapNhatLuc, x.CauHoi, x.CachHoiKhac }).ToListAsync(ct);
        if (pending.Count == 0) return 0;

        var vectors = await EmbedAsync(pending.Select(f => f.CauHoi + "\n" + string.Join("\n", f.CachHoiKhac)).ToList(), ct);
        var updated = 0;
        for (var i = 0; i < pending.Count; i++)
        {
            var p = pending[i];
            var v = new Vector(vectors[i]);
            // Chỉ ghi nếu FAQ không bị sửa trong lúc embed (sửa sẽ đổi CapNhatLuc và đặt lại embedding).
            updated += await db.Faq.Where(x => x.Id == p.Id && x.CapNhatLuc == p.CapNhatLuc && x.Embedding == null)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.Embedding, v), ct);
        }
        return updated;
    }
}
