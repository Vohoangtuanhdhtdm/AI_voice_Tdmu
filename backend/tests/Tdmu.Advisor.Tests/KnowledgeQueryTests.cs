using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Tdmu.Advisor.Api.Features.KnowledgeBase;
using Tdmu.Advisor.Api.Features.KnowledgeBase.Query;
using Tdmu.Advisor.Tests.Infrastructure;

namespace Tdmu.Advisor.Tests;

[Collection("api")]
public class KnowledgeQueryTests(ApiFactory f)
{
    private static async Task<JsonElement> Json(HttpResponseMessage r) => await r.Content.ReadFromJsonAsync<JsonElement>();

    private static async Task<Guid> Create(HttpClient c, string slug, object body)
    {
        var res = await c.PostAsJsonAsync($"/api/admin/kb/{slug}", body);
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        return (await Json(res)).GetProperty("logicalId").GetGuid();
    }

    private static async Task Publish(HttpClient c)
    {
        var res = await c.PostAsJsonAsync("/api/admin/releases", new { ghiChu = "x" });
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
    }

    [Fact]
    public async Task Public_list_is_empty_without_release()
    {
        await f.ResetDatabaseAsync();
        var c = await AuthHelper.AdminClientAsync(f);
        await Create(c, "nganh", new { maNganh = "A1", ten = "Ngành A" });
        var res = await f.CreateClient().GetAsync("/api/public/nganh");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        Assert.Empty((await Json(res)).EnumerateArray());
        Assert.Equal(HttpStatusCode.NotFound, (await f.CreateClient().GetAsync("/api/public/nganh/A1")).StatusCode);
    }

    [Fact]
    public async Task Public_sees_only_published()
    {
        await f.ResetDatabaseAsync();
        var c = await AuthHelper.AdminClientAsync(f);
        await Create(c, "nganh", new { maNganh = "A1", ten = "Ngành A" });
        await Publish(c);
        await Create(c, "nganh", new { maNganh = "B1", ten = "Ngành B" });
        var list = (await Json(await f.CreateClient().GetAsync("/api/public/nganh"))).EnumerateArray().ToList();
        Assert.Single(list);
        Assert.Equal("A1", list[0].GetProperty("maNganh").GetString());
        Assert.Equal(HttpStatusCode.NotFound, (await f.CreateClient().GetAsync("/api/public/nganh/B1")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await f.CreateClient().GetAsync("/api/public/nganh/A1")).StatusCode);
    }

    [Fact]
    public async Task Draft_scope_sees_draft_changes()
    {
        await f.ResetDatabaseAsync();
        var c = await AuthHelper.AdminClientAsync(f);
        await Create(c, "nganh", new { maNganh = "A1", ten = "Ngành A" });
        await Publish(c);
        await Create(c, "nganh", new { maNganh = "B1", ten = "Ngành B" });
        using var scope = f.Services.CreateScope();
        var q = scope.ServiceProvider.GetRequiredService<IKnowledgeQuery>();
        Assert.Equal(2, (await q.ListNganhAsync(KbScope.Draft, default)).Count);
        Assert.Single(await q.ListNganhAsync(KbScope.Published, default));
    }

    [Fact]
    public async Task Published_scope_without_release_throws()
    {
        await f.ResetDatabaseAsync();
        using var scope = f.Services.CreateScope();
        var q = scope.ServiceProvider.GetRequiredService<IKnowledgeQuery>();
        await Assert.ThrowsAsync<NoActiveReleaseException>(() => q.ResolveReleaseAsync(KbScope.Published, default));
        Assert.Null(await q.ResolveReleaseAsync(KbScope.Draft, default));
    }

    [Fact]
    public async Task Nganh_detail_returns_current_year_data_with_sources()
    {
        await f.ResetDatabaseAsync();
        var c = await AuthHelper.AdminClientAsync(f);
        await Create(c, "mua-tuyen-sinh", new { nam = 2025, laHienTai = false });
        await Create(c, "mua-tuyen-sinh", new { nam = 2026, laHienTai = true });
        var n = await Create(c, "nganh", new { maNganh = "A1", ten = "Ngành A" });
        var n2 = await Create(c, "nganh", new { maNganh = "B1", ten = "Ngành B" });
        var pt = await Create(c, "phuong-thuc", new { ma = "THPT", ten = "thi THPT", thangDiem = 30 });
        var th = await Create(c, "to-hop", new { ma = "A00", mon = new[] { "Toán", "Lý", "Hóa" } });
        await Create(c, "nganh-to-hop", new { nganhLid = n, nam = 2026, toHopLid = th });
        await Create(c, "nganh-to-hop", new { nganhLid = n, nam = 2025, toHopLid = th });
        await Create(c, "diem-chuan", new { nganhLid = n, nam = 2026, phuongThucLid = pt, diem = 24.5, nguonTen = "Đề án 2026", nguonUrl = "https://x.test/da" });
        await Create(c, "diem-chuan", new { nganhLid = n, nam = 2025, phuongThucLid = pt, diem = 23 });
        await Create(c, "chi-tieu", new { nganhLid = n, nam = 2026, soLuong = 100 });
        await Create(c, "hoc-phi", new { nganhLid = n, nam = 2026, muc = 500000, donVi = "TinChi" });
        await Create(c, "hoc-phi", new { nam = 2026, muc = 400000, donVi = "TinChi" });
        await Create(c, "hoc-phi", new { nganhLid = n2, nam = 2026, muc = 999, donVi = "TinChi" });
        await Publish(c);

        var j = await Json(await f.CreateClient().GetAsync("/api/public/nganh/A1"));
        Assert.Equal(2026, j.GetProperty("nam").GetInt32());
        Assert.Equal("Ngành A", j.GetProperty("nganh").GetProperty("ten").GetString());
        Assert.Single(j.GetProperty("toHop").EnumerateArray());
        var dc = j.GetProperty("diemChuan").EnumerateArray().Single();
        Assert.Equal(24.5m, dc.GetProperty("diem").GetDecimal());
        Assert.Equal("Đề án 2026", dc.GetProperty("nguonTen").GetString());
        Assert.Equal("https://x.test/da", dc.GetProperty("nguonUrl").GetString());
        Assert.Equal(100, j.GetProperty("chiTieu").EnumerateArray().Single().GetProperty("soLuong").GetInt32());
        Assert.Equal(2, j.GetProperty("hocPhi").GetArrayLength());
    }

    [Fact]
    public async Task Nganh_detail_without_current_year_has_null_year_and_empty_lists()
    {
        await f.ResetDatabaseAsync();
        var c = await AuthHelper.AdminClientAsync(f);
        var n = await Create(c, "nganh", new { maNganh = "A1", ten = "Ngành A" });
        await Create(c, "chi-tieu", new { nganhLid = n, nam = 2026, soLuong = 100 });
        using var scope = f.Services.CreateScope();
        var q = scope.ServiceProvider.GetRequiredService<IKnowledgeQuery>();
        var d = await q.GetNganhAsync(KbScope.Draft, "A1", default);
        Assert.NotNull(d);
        Assert.Null(d.Nam);
        Assert.Empty(d.ChiTieu);
    }
}
