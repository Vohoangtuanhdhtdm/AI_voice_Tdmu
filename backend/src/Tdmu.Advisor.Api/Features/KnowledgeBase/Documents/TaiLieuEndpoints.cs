using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Tdmu.Advisor.Api.Features.KnowledgeBase.Entities;
using Tdmu.Advisor.Api.Infrastructure;
using Tdmu.Advisor.Api.Infrastructure.Http;
using Tdmu.Advisor.Api.Infrastructure.Storage;

namespace Tdmu.Advisor.Api.Features.KnowledgeBase.Documents;

public record TaiLieuDto(
    Guid LogicalId, string TieuDe, string TenFileGoc, int? NamApDung, TrangThaiTaiLieu TrangThai, string? Loi,
    int SoDoan, DateTimeOffset CapNhatLuc, string? NguonTen, string? NguonUrl);

public static class TaiLieuEndpoints
{
    public const long MaxBytes = 20L * 1024 * 1024;

    private static readonly Dictionary<string, string> ContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        [".pdf"] = "application/pdf",
        [".docx"] = "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        [".md"] = "text/markdown",
        [".txt"] = "text/plain",
    };

    private static TaiLieuDto ToDto(TaiLieu t, int soDoan) =>
        new(t.LogicalId, t.TieuDe, t.TenFileGoc, t.NamApDung, t.TrangThai, t.Loi, soDoan, t.CapNhatLuc, t.NguonTen, t.NguonUrl);

    public static void MapTaiLieuEndpoints(this RouteGroupBuilder admin)
    {
        var g = admin.MapGroup("/tai-lieu");

        g.MapGet("", async (AdvisorDbContext db, CancellationToken ct) =>
        {
            var rows = await db.TaiLieu.AsNoTracking().Where(x => x.ReleaseId == null).OrderBy(x => x.CapNhatLuc).ToListAsync(ct);
            var counts = await db.TaiLieuChunk.AsNoTracking().Where(x => x.ReleaseId == null)
                .GroupBy(x => x.TaiLieuLid).Select(x => new { Lid = x.Key, N = x.Count() }).ToDictionaryAsync(x => x.Lid, x => x.N, ct);
            return Results.Ok(rows.Select(t => ToDto(t, counts.GetValueOrDefault(t.LogicalId))));
        });

        g.MapPost("", async (IFormFile? file, [FromForm] string? tieuDe, [FromForm] int? namApDung,
            [FromForm] string? nguonTen, [FromForm] string? nguonUrl,
            AdvisorDbContext db, IFileStore store, CancellationToken ct) =>
        {
            if (file is null || file.Length == 0) return ProblemResults.Problem(400, "Chưa chọn file hoặc file rỗng");
            if (string.IsNullOrWhiteSpace(tieuDe)) return ProblemResults.Problem(400, "Tiêu đề không được để trống");
            var ext = Path.GetExtension(file.FileName ?? "").ToLowerInvariant();
            if (!ContentTypes.TryGetValue(ext, out var contentType))
                return ProblemResults.Problem(400, "Chỉ nhận file PDF, DOCX, MD hoặc TXT");
            if (file.Length > MaxBytes) return ProblemResults.Problem(413, "File vượt quá 20 MB");

            var t = new TaiLieu
            {
                TieuDe = tieuDe.Trim(), NamApDung = namApDung, NguonTen = nguonTen, NguonUrl = nguonUrl,
                TenFileGoc = Path.GetFileName(file.FileName!), LoaiNoiDung = contentType, KichThuoc = file.Length,
                TrangThai = TrangThaiTaiLieu.ChoXuLy,
            };
            await using (var stream = file.OpenReadStream())
                t.DuongDanFile = await store.SaveAsync(t.LogicalId, ext, stream, ct);
            try
            {
                db.TaiLieu.Add(t);
                await db.SaveChangesAsync(ct);
            }
            catch
            {
                await store.DeleteAsync(t.DuongDanFile, CancellationToken.None); // Không để file mồ côi khi ghi CSDL lỗi.
                throw;
            }
            return Results.Created($"/api/admin/tai-lieu/{t.LogicalId}", ToDto(t, 0));
        }).DisableAntiforgery();

        g.MapPost("/{logicalId:guid}/xu-ly-lai", async (Guid logicalId, AdvisorDbContext db, CancellationToken ct) =>
        {
            var t = await db.TaiLieu.FirstOrDefaultAsync(x => x.ReleaseId == null && x.LogicalId == logicalId, ct);
            if (t is null) return ProblemResults.NotFound("Không tìm thấy tài liệu");
            t.TrangThai = TrangThaiTaiLieu.ChoXuLy;
            t.Loi = null;
            t.CapNhatLuc = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        });

        g.MapDelete("/{logicalId:guid}", async (Guid logicalId, AdvisorDbContext db, IFileStore store, CancellationToken ct) =>
        {
            var t = await db.TaiLieu.FirstOrDefaultAsync(x => x.ReleaseId == null && x.LogicalId == logicalId, ct);
            if (t is null) return ProblemResults.NotFound("Không tìm thấy tài liệu");
            await using (var tx = await db.Database.BeginTransactionAsync(ct))
            {
                await db.TaiLieuChunk.Where(x => x.ReleaseId == null && x.TaiLieuLid == logicalId).ExecuteDeleteAsync(ct);
                db.TaiLieu.Remove(t);
                await db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);
            }
            // Release đã xuất bản có thể còn giữ cùng file: chỉ xóa khi không còn dòng nào tham chiếu.
            if (!await db.TaiLieu.AnyAsync(x => x.DuongDanFile == t.DuongDanFile, ct))
                await store.DeleteAsync(t.DuongDanFile, ct);
            return Results.NoContent();
        });
    }
}
