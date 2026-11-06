using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NpgsqlTypes;
using Pgvector;
using Tdmu.Advisor.Api.Features.KnowledgeBase.Entities;
using Tdmu.Advisor.Api.Features.KnowledgeBase.Releases;
using Tdmu.Advisor.Api.Infrastructure;
using Tdmu.Advisor.Tests.Infrastructure;

namespace Tdmu.Advisor.Tests;

[Collection("api")]
public class ReleaseTests(ApiFactory f)
{
    private static async Task<JsonElement> Json(HttpResponseMessage r) => await r.Content.ReadFromJsonAsync<JsonElement>();

    private static async Task<Guid> Create(HttpClient c, string slug, object body)
    {
        var res = await c.PostAsJsonAsync($"/api/admin/kb/{slug}", body);
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        return (await Json(res)).GetProperty("logicalId").GetGuid();
    }

    private sealed record Seed(Guid Nganh, Guid Nganh2, Guid PhuongThuc, Guid DiemChuan);

    private static async Task<Seed> SeedDrafts(HttpClient c)
    {
        var n = await Create(c, "nganh", new { maNganh = "7480201", ten = "Công nghệ thông tin" });
        var n2 = await Create(c, "nganh", new { maNganh = "7340101", ten = "Quản trị kinh doanh" });
        var pt = await Create(c, "phuong-thuc", new { ma = "THPT", ten = "Điểm thi THPT", thangDiem = 30 });
        var dc = await Create(c, "diem-chuan", new { nganhLid = n, nam = 2026, phuongThucLid = pt, diem = 24.5 });
        return new(n, n2, pt, dc);
    }

    private async Task<T> WithDb<T>(Func<AdvisorDbContext, Task<T>> fn)
    {
        using var scope = f.Services.CreateScope();
        return await fn(scope.ServiceProvider.GetRequiredService<AdvisorDbContext>());
    }

    private static async Task<Guid> Publish(HttpClient c, string note = "x")
    {
        var res = await c.PostAsJsonAsync("/api/admin/releases", new { ghiChu = note });
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        return (await Json(res)).GetProperty("id").GetGuid();
    }

    [Fact]
    public async Task Diff_with_no_release_lists_all_drafts_as_added()
    {
        await f.ResetDatabaseAsync();
        var c = await AuthHelper.AdminClientAsync(f);
        await SeedDrafts(c);
        var j = await Json(await c.GetAsync("/api/admin/releases/diff"));
        var items = j.GetProperty("items").EnumerateArray().ToList();
        Assert.Equal(4, items.Count);
        Assert.All(items, i => Assert.Equal("Added", i.GetProperty("kind").GetString()));
        Assert.Contains(items, i => i.GetProperty("label").GetString()!.Contains("Công nghệ thông tin"));
    }

    [Fact]
    public async Task Diff_detects_modified_and_removed()
    {
        await f.ResetDatabaseAsync();
        var c = await AuthHelper.AdminClientAsync(f);
        var s = await SeedDrafts(c);
        await Publish(c);
        var put = await c.PutAsJsonAsync($"/api/admin/kb/diem-chuan/{s.DiemChuan}",
            new { nganhLid = s.Nganh, nam = 2026, phuongThucLid = s.PhuongThuc, diem = 25 });
        put.EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.NoContent, (await c.DeleteAsync($"/api/admin/kb/nganh/{s.Nganh2}")).StatusCode);

        var items = (await Json(await c.GetAsync("/api/admin/releases/diff"))).GetProperty("items").EnumerateArray().ToList();
        Assert.Equal(2, items.Count);
        var mod = items.Single(i => i.GetProperty("kind").GetString() == "Modified");
        Assert.Equal("DiemChuan", mod.GetProperty("entity").GetString());
        Assert.Equal(s.DiemChuan, mod.GetProperty("logicalId").GetGuid());
        var rem = items.Single(i => i.GetProperty("kind").GetString() == "Removed");
        Assert.Equal("Nganh", rem.GetProperty("entity").GetString());
        Assert.Contains("Quản trị kinh doanh", rem.GetProperty("label").GetString());
    }

    [Fact]
    public async Task Publish_copies_drafts_and_activates()
    {
        await f.ResetDatabaseAsync();
        var c = await AuthHelper.AdminClientAsync(f);
        var s = await SeedDrafts(c);
        var vec = new Vector(Enumerable.Range(0, 768).Select(i => i / 768f).ToArray());
        var docLid = Guid.NewGuid();
        await WithDb(async db =>
        {
            db.Faq.Add(new Faq { CauHoi = "Hỏi", TraLoi = "Đáp", Embedding = vec });
            db.TaiLieu.Add(new TaiLieu { LogicalId = docLid, TieuDe = "Đề án", DuongDanFile = "a.pdf", TrangThai = TrangThaiTaiLieu.Xong });
            db.TaiLieuChunk.Add(new TaiLieuChunk { TaiLieuLid = docLid, ThuTu = 1, NoiDung = "abc", Embedding = vec, Tsv = NpgsqlTsVector.Parse("'abc':1") });
            await db.SaveChangesAsync();
            return 0;
        });

        var id = await Publish(c, "Bản đầu");

        await WithDb(async db =>
        {
            var rels = await db.Release.ToListAsync();
            var r = Assert.Single(rels);
            Assert.True(r.IsActive);
            Assert.Equal(1, r.SoHieu);
            Assert.Equal(id, r.Id);
            Assert.Equal("Bản đầu", r.GhiChu);
            Assert.Equal(await db.Nganh.CountAsync(x => x.ReleaseId == null), await db.Nganh.CountAsync(x => x.ReleaseId == r.Id));
            Assert.Equal(2, await db.Nganh.CountAsync(x => x.ReleaseId == r.Id));
            Assert.Equal(1, await db.DiemChuan.CountAsync(x => x.ReleaseId == r.Id));
            var draft = await db.Nganh.Where(x => x.ReleaseId == null).Select(x => x.LogicalId).OrderBy(x => x).ToListAsync();
            var rel = await db.Nganh.Where(x => x.ReleaseId == r.Id).Select(x => x.LogicalId).OrderBy(x => x).ToListAsync();
            Assert.Equal(draft, rel);
            var faq = await db.Faq.SingleAsync(x => x.ReleaseId == r.Id);
            Assert.Equal(vec.ToArray(), faq.Embedding!.ToArray());
            var chunk = await db.TaiLieuChunk.SingleAsync(x => x.ReleaseId == r.Id);
            Assert.Equal(vec.ToArray(), chunk.Embedding!.ToArray());
            Assert.Equal("'abc':1", chunk.Tsv!.ToString());
            Assert.Equal(1, await db.TaiLieu.CountAsync(x => x.ReleaseId == r.Id));
            Assert.Equal(1, await db.AuditLog.CountAsync(a => a.Action == "Published" && a.EntityLogicalId == r.Id));
            return 0;
        });
    }

    [Fact]
    public async Task Publish_without_changes_returns_409()
    {
        await f.ResetDatabaseAsync();
        var c = await AuthHelper.AdminClientAsync(f);
        var empty = await c.PostAsJsonAsync("/api/admin/releases", new { ghiChu = "x" });
        Assert.Equal(HttpStatusCode.Conflict, empty.StatusCode);
        Assert.Equal("Không có thay đổi để xuất bản", (await Json(empty)).GetProperty("title").GetString());

        await SeedDrafts(c);
        await Publish(c);
        var again = await c.PostAsJsonAsync("/api/admin/releases", new { ghiChu = "y" });
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
        Assert.Equal(1, await WithDb(db => db.Release.CountAsync()));
    }

    [Fact]
    public async Task Concurrent_publish_creates_single_release()
    {
        await f.ResetDatabaseAsync();
        var c = await AuthHelper.AdminClientAsync(f);
        await SeedDrafts(c);
        var results = await Task.WhenAll(
            c.PostAsJsonAsync("/api/admin/releases", new { ghiChu = "a" }),
            c.PostAsJsonAsync("/api/admin/releases", new { ghiChu = "b" }));
        Assert.Equal(1, results.Count(r => r.StatusCode == HttpStatusCode.Created));
        Assert.Equal(1, results.Count(r => r.StatusCode == HttpStatusCode.Conflict));
        await WithDb(async db =>
        {
            Assert.Equal(1, await db.Release.CountAsync());
            Assert.Equal(1, await db.Release.CountAsync(r => r.IsActive));
            Assert.Equal(2, await db.Nganh.CountAsync(x => x.ReleaseId != null));
            return 0;
        });
    }

    [Fact]
    public async Task Activate_older_release_switches_active()
    {
        await f.ResetDatabaseAsync();
        var c = await AuthHelper.AdminClientAsync(f);
        var s = await SeedDrafts(c);
        var r1 = await Publish(c);
        (await c.PutAsJsonAsync($"/api/admin/kb/diem-chuan/{s.DiemChuan}",
            new { nganhLid = s.Nganh, nam = 2026, phuongThucLid = s.PhuongThuc, diem = 26 })).EnsureSuccessStatusCode();
        var r2 = await Publish(c);

        var list = (await Json(await c.GetAsync("/api/admin/releases"))).EnumerateArray().ToList();
        Assert.Equal([r2, r1], list.Select(x => x.GetProperty("id").GetGuid()));

        Assert.Equal(HttpStatusCode.NoContent, (await c.PostAsync($"/api/admin/releases/{r1}/activate", null)).StatusCode);
        await WithDb(async db =>
        {
            Assert.Equal(r1, (await db.Release.SingleAsync(r => r.IsActive)).Id);
            Assert.Equal(1, await db.AuditLog.CountAsync(a => a.Action == "Activated" && a.EntityLogicalId == r1));
            return 0;
        });
        Assert.Equal(HttpStatusCode.NotFound, (await c.PostAsync($"/api/admin/releases/{Guid.NewGuid()}/activate", null)).StatusCode);
    }

    [Fact]
    public async Task Restore_to_draft_replaces_draft_content()
    {
        await f.ResetDatabaseAsync();
        var c = await AuthHelper.AdminClientAsync(f);
        var s = await SeedDrafts(c);
        var r1 = await Publish(c);
        (await c.PutAsJsonAsync($"/api/admin/kb/diem-chuan/{s.DiemChuan}",
            new { nganhLid = s.Nganh, nam = 2026, phuongThucLid = s.PhuongThuc, diem = 29 })).EnsureSuccessStatusCode();
        await c.DeleteAsync($"/api/admin/kb/nganh/{s.Nganh2}");
        await Create(c, "faq", new { cauHoi = "Mới thêm", traLoi = "ok" });
        var auditBefore = await WithDb(db => db.AuditLog.CountAsync());

        Assert.Equal(HttpStatusCode.NoContent, (await c.PostAsync($"/api/admin/releases/{r1}/restore-to-draft", null)).StatusCode);

        await WithDb(async db =>
        {
            Assert.Equal(2, await db.Nganh.CountAsync(x => x.ReleaseId == null));
            Assert.Equal(0, await db.Faq.CountAsync(x => x.ReleaseId == null));
            Assert.Equal(24.5m, (await db.DiemChuan.SingleAsync(x => x.ReleaseId == null)).Diem);
            Assert.Equal(s.DiemChuan, (await db.DiemChuan.SingleAsync(x => x.ReleaseId == null)).LogicalId);
            // Release không đổi
            Assert.Equal(2, await db.Nganh.CountAsync(x => x.ReleaseId == r1));
            Assert.Equal(24.5m, (await db.DiemChuan.SingleAsync(x => x.ReleaseId == r1)).Diem);
            // Chỉ một dòng audit mới
            var newAudit = await db.AuditLog.OrderBy(a => a.Id).Skip(auditBefore).ToListAsync();
            Assert.Equal("RestoredToDraft", Assert.Single(newAudit).Action);
            return 0;
        });
        // Sau khôi phục, bản nháp trùng release đang active => không có thay đổi
        Assert.Empty((await Json(await c.GetAsync("/api/admin/releases/diff"))).GetProperty("items").EnumerateArray());
        // Audit tiếp tục hoạt động sau khi hết phạm vi tắt
        await Create(c, "faq", new { cauHoi = "Sau", traLoi = "ok" });
        Assert.Equal(auditBefore + 2, await WithDb(db => db.AuditLog.CountAsync()));
    }

    [Fact]
    public async Task Release_endpoints_require_admin()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await f.CreateClient().GetAsync("/api/admin/releases")).StatusCode);
    }

    /// <summary>Giữ transaction xuất bản/khôi phục mở ngay sau khi khóa bảng, để kiểm tra CRUD phải chờ.</summary>
    private static (TaskCompletionSource Locked, TaskCompletionSource Go) Hold(ReleaseService svc)
    {
        var locked = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var go = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        svc.AfterTablesLocked = async _ => { locked.SetResult(); await go.Task; };
        return (locked, go);
    }

    [Fact]
    public async Task Crud_write_during_publish_waits_until_publish_commits()
    {
        await f.ResetDatabaseAsync();
        var c = await AuthHelper.AdminClientAsync(f);
        var s = await SeedDrafts(c);
        using var scope = f.Services.CreateScope();
        var svc = scope.ServiceProvider.GetRequiredService<ReleaseService>();
        var (locked, go) = Hold(svc);
        var publish = svc.PublishAsync("v1", "test", default);
        await locked.Task.WaitAsync(TimeSpan.FromSeconds(30));

        var put = c.PutAsJsonAsync($"/api/admin/kb/nganh/{s.Nganh}", new { maNganh = "7480201", ten = "Đổi tên giữa chừng" });
        Assert.NotSame(put, await Task.WhenAny(put, Task.Delay(TimeSpan.FromSeconds(2))));

        go.SetResult();
        var release = await publish.WaitAsync(TimeSpan.FromSeconds(30));
        Assert.Equal(HttpStatusCode.OK, (await put.WaitAsync(TimeSpan.FromSeconds(30))).StatusCode);
        await WithDb(async db =>
        {
            Assert.Equal("Công nghệ thông tin", (await db.Nganh.SingleAsync(x => x.ReleaseId == release.Id && x.LogicalId == s.Nganh)).Ten);
            Assert.Equal("Đổi tên giữa chừng", (await db.Nganh.SingleAsync(x => x.ReleaseId == null && x.LogicalId == s.Nganh)).Ten);
            return 0;
        });
    }

    [Fact]
    public async Task Crud_write_during_restore_waits_until_restore_commits()
    {
        await f.ResetDatabaseAsync();
        var c = await AuthHelper.AdminClientAsync(f);
        await SeedDrafts(c);
        var r1 = await Publish(c);
        using var scope = f.Services.CreateScope();
        var svc = scope.ServiceProvider.GetRequiredService<ReleaseService>();
        var (locked, go) = Hold(svc);
        var restore = svc.RestoreToDraftAsync(r1, "test", default);
        await locked.Task.WaitAsync(TimeSpan.FromSeconds(30));

        var post = c.PostAsJsonAsync("/api/admin/kb/faq", new { cauHoi = "Thêm giữa chừng", traLoi = "ok" });
        Assert.NotSame(post, await Task.WhenAny(post, Task.Delay(TimeSpan.FromSeconds(2))));

        go.SetResult();
        await restore.WaitAsync(TimeSpan.FromSeconds(30));
        Assert.Equal(HttpStatusCode.Created, (await post.WaitAsync(TimeSpan.FromSeconds(30))).StatusCode);
        Assert.Equal(1, await WithDb(db => db.Faq.CountAsync(x => x.ReleaseId == null)));
    }
}
