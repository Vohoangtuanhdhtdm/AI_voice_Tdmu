using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pgvector;
using Tdmu.Advisor.Api.Features.KnowledgeBase.Entities;
using Tdmu.Advisor.Api.Infrastructure;
using Tdmu.Advisor.Tests.Infrastructure;

namespace Tdmu.Advisor.Tests;

[Collection("api")]
public class TaiLieuEndpointsTests(ApiFactory f)
{
    private static async Task<JsonElement> Json(HttpResponseMessage r) => await r.Content.ReadFromJsonAsync<JsonElement>();

    private async Task<T> WithDb<T>(Func<AdvisorDbContext, Task<T>> fn)
    {
        using var scope = f.Services.CreateScope();
        return await fn(scope.ServiceProvider.GetRequiredService<AdvisorDbContext>());
    }

    private static MultipartFormDataContent Form(string fileName, byte[] bytes, string tieuDe = "Đề án tuyển sinh 2026")
    {
        var content = new MultipartFormDataContent();
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new("application/octet-stream");
        content.Add(file, "file", fileName);
        content.Add(new StringContent(tieuDe), "tieuDe");
        content.Add(new StringContent("2026"), "namApDung");
        return content;
    }

    private static async Task<Guid> Upload(HttpClient c, string name = "de-an.pdf")
    {
        var res = await c.PostAsync("/api/admin/tai-lieu", Form(name, [1, 2, 3, 4]));
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        return (await Json(res)).GetProperty("logicalId").GetGuid();
    }

    [Fact]
    public async Task Upload_pdf_creates_pending_document()
    {
        await f.ResetDatabaseAsync();
        var c = await AuthHelper.AdminClientAsync(f);
        var res = await c.PostAsync("/api/admin/tai-lieu", Form("de-an.pdf", [1, 2, 3, 4]));
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        var j = await Json(res);
        Assert.Equal("ChoXuLy", j.GetProperty("trangThai").GetString());
        Assert.Equal("de-an.pdf", j.GetProperty("tenFileGoc").GetString());
        Assert.Equal(2026, j.GetProperty("namApDung").GetInt32());
        var lid = j.GetProperty("logicalId").GetGuid();
        var row = await WithDb(db => db.TaiLieu.SingleAsync(x => x.LogicalId == lid));
        Assert.Equal($"{lid}.pdf", row.DuongDanFile);
        Assert.Equal(4, row.KichThuoc);
        Assert.Equal(new byte[] { 1, 2, 3, 4 }, await File.ReadAllBytesAsync(Path.Combine(f.StorageRoot, row.DuongDanFile)));

        var list = (await Json(await c.GetAsync("/api/admin/tai-lieu"))).EnumerateArray().Single();
        Assert.Equal(0, list.GetProperty("soDoan").GetInt32());
    }

    [Fact]
    public async Task Upload_exe_returns_400()
    {
        await f.ResetDatabaseAsync();
        var c = await AuthHelper.AdminClientAsync(f);
        var res = await c.PostAsync("/api/admin/tai-lieu", Form("virus.exe", [1, 2]));
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Equal("Chỉ nhận file PDF, DOCX, MD hoặc TXT", (await Json(res)).GetProperty("title").GetString());
        Assert.Equal(0, await WithDb(db => db.TaiLieu.CountAsync()));
    }

    [Fact]
    public async Task Upload_over_20mb_returns_413()
    {
        await f.ResetDatabaseAsync();
        var c = await AuthHelper.AdminClientAsync(f);
        var res = await c.PostAsync("/api/admin/tai-lieu", Form("lon.pdf", new byte[20 * 1024 * 1024 + 1]));
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, res.StatusCode);
        Assert.Equal("File vượt quá 20 MB", (await Json(res)).GetProperty("title").GetString());
    }

    [Fact]
    public async Task Reprocess_resets_status()
    {
        await f.ResetDatabaseAsync();
        var c = await AuthHelper.AdminClientAsync(f);
        var lid = await Upload(c);
        await WithDb(async db =>
        {
            var t = await db.TaiLieu.SingleAsync(x => x.LogicalId == lid);
            t.TrangThai = TrangThaiTaiLieu.ThatBai; t.Loi = "hỏng";
            await db.SaveChangesAsync();
            return 0;
        });

        Assert.Equal(HttpStatusCode.NoContent, (await c.PostAsync($"/api/admin/tai-lieu/{lid}/xu-ly-lai", null)).StatusCode);

        var row = await WithDb(db => db.TaiLieu.SingleAsync(x => x.LogicalId == lid));
        Assert.Equal(TrangThaiTaiLieu.ChoXuLy, row.TrangThai);
        Assert.Null(row.Loi);
        Assert.Equal(HttpStatusCode.NotFound, (await c.PostAsync($"/api/admin/tai-lieu/{Guid.NewGuid()}/xu-ly-lai", null)).StatusCode);
    }

    [Fact]
    public async Task Delete_removes_draft_document_and_chunks()
    {
        await f.ResetDatabaseAsync();
        var c = await AuthHelper.AdminClientAsync(f);
        var lid = await Upload(c);
        await WithDb(async db =>
        {
            db.TaiLieuChunk.Add(new TaiLieuChunk { TaiLieuLid = lid, ThuTu = 0, NoiDung = "đoạn" });
            await db.SaveChangesAsync();
            return 0;
        });
        var path = Path.Combine(f.StorageRoot, $"{lid}.pdf");
        Assert.True(File.Exists(path));

        Assert.Equal(HttpStatusCode.NoContent, (await c.DeleteAsync($"/api/admin/tai-lieu/{lid}")).StatusCode);

        Assert.Equal(0, await WithDb(db => db.TaiLieu.CountAsync()));
        Assert.Equal(0, await WithDb(db => db.TaiLieuChunk.CountAsync()));
        Assert.False(File.Exists(path));
    }

    [Fact]
    public async Task Delete_keeps_file_when_a_release_still_references_it()
    {
        await f.ResetDatabaseAsync();
        var c = await AuthHelper.AdminClientAsync(f);
        var lid = await Upload(c);
        await WithDb(async db =>
        {
            var t = await db.TaiLieu.SingleAsync(x => x.LogicalId == lid);
            t.TrangThai = TrangThaiTaiLieu.Xong;
            await db.SaveChangesAsync();
            return 0;
        });
        (await c.PostAsJsonAsync("/api/admin/releases", new { ghiChu = "x" })).EnsureSuccessStatusCode();

        Assert.Equal(HttpStatusCode.NoContent, (await c.DeleteAsync($"/api/admin/tai-lieu/{lid}")).StatusCode);

        Assert.True(File.Exists(Path.Combine(f.StorageRoot, $"{lid}.pdf")));
    }

    [Fact]
    public async Task Publish_blocked_while_document_pending()
    {
        await f.ResetDatabaseAsync();
        var c = await AuthHelper.AdminClientAsync(f);
        await Upload(c);
        var res = await c.PostAsJsonAsync("/api/admin/releases", new { ghiChu = "x" });
        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
        Assert.Equal("Còn tài liệu hoặc FAQ đang xử lý, vui lòng chờ hoàn tất rồi xuất bản", (await Json(res)).GetProperty("title").GetString());
    }

    [Fact]
    public async Task Publish_blocked_while_faq_has_no_embedding()
    {
        await f.ResetDatabaseAsync();
        var c = await AuthHelper.AdminClientAsync(f);
        (await c.PostAsJsonAsync("/api/admin/kb/faq", new { cauHoi = "Hỏi", traLoi = "Đáp" })).EnsureSuccessStatusCode();
        var res = await c.PostAsJsonAsync("/api/admin/releases", new { ghiChu = "x" });
        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
        Assert.Equal("Còn tài liệu hoặc FAQ đang xử lý, vui lòng chờ hoàn tất rồi xuất bản", (await Json(res)).GetProperty("title").GetString());
    }

    [Fact]
    public async Task TaiLieu_status_change_is_not_a_content_diff()
    {
        await f.ResetDatabaseAsync();
        var c = await AuthHelper.AdminClientAsync(f);
        var lid = await Upload(c);
        await Mark(lid, TrangThaiTaiLieu.Xong, null);
        (await c.PostAsJsonAsync("/api/admin/releases", new { ghiChu = "x" })).EnsureSuccessStatusCode();

        await Mark(lid, TrangThaiTaiLieu.ThatBai, "lỗi tạm");
        await Mark(lid, TrangThaiTaiLieu.Xong, null);

        var items = (await Json(await c.GetAsync("/api/admin/releases/diff"))).GetProperty("items").EnumerateArray().ToList();
        Assert.Empty(items);
    }

    private Task<int> Mark(Guid lid, TrangThaiTaiLieu s, string? loi) => WithDb(async db =>
    {
        var t = await db.TaiLieu.SingleAsync(x => x.LogicalId == lid && x.ReleaseId == null);
        t.TrangThai = s; t.Loi = loi;
        return await db.SaveChangesAsync();
    });
}
