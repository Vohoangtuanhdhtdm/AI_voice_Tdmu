using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Tdmu.Advisor.Tests.Infrastructure;

namespace Tdmu.Advisor.Tests;

[Collection("api")]
public class KbCrudTests(ApiFactory f)
{
    private static async Task<JsonElement> Json(HttpResponseMessage r) => await r.Content.ReadFromJsonAsync<JsonElement>();

    private static async Task<Guid> Create(HttpClient c, string slug, object body)
    {
        var res = await c.PostAsJsonAsync($"/api/admin/kb/{slug}", body);
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        return (await Json(res)).GetProperty("logicalId").GetGuid();
    }

    private static Task<Guid> Nganh(HttpClient c, string ten = "Công nghệ thông tin", string ma = "7480201") =>
        Create(c, "nganh", new { maNganh = ma, ten, tenGoiKhac = new[] { "CNTT", "IT" } });

    private static Task<Guid> PhuongThuc(HttpClient c, string ma, string ten, decimal thang) =>
        Create(c, "phuong-thuc", new { ma, ten, thangDiem = thang });

    [Fact]
    public async Task Create_and_get_nganh()
    {
        await f.ResetDatabaseAsync();
        var c = await AuthHelper.AdminClientAsync(f);
        var lid = await Nganh(c);
        var res = await c.GetAsync($"/api/admin/kb/nganh/{lid}");
        res.EnsureSuccessStatusCode();
        var j = await Json(res);
        Assert.Equal("Công nghệ thông tin", j.GetProperty("ten").GetString());
        Assert.Equal(["CNTT", "IT"], j.GetProperty("tenGoiKhac").EnumerateArray().Select(x => x.GetString()).ToArray());
    }

    [Fact]
    public async Task Get_unknown_returns_404()
    {
        await f.ResetDatabaseAsync();
        var c = await AuthHelper.AdminClientAsync(f);
        Assert.Equal(HttpStatusCode.NotFound, (await c.GetAsync($"/api/admin/kb/nganh/{Guid.NewGuid()}")).StatusCode);
    }

    [Fact]
    public async Task Update_and_delete_nganh()
    {
        await f.ResetDatabaseAsync();
        var c = await AuthHelper.AdminClientAsync(f);
        var lid = await Nganh(c);
        var put = await c.PutAsJsonAsync($"/api/admin/kb/nganh/{lid}", new { maNganh = "7480201", ten = "Khoa học máy tính" });
        Assert.Equal(HttpStatusCode.OK, put.StatusCode);
        var list = await Json(await c.GetAsync("/api/admin/kb/nganh"));
        Assert.Equal("Khoa học máy tính", list[0].GetProperty("ten").GetString());
        Assert.Equal(HttpStatusCode.NoContent, (await c.DeleteAsync($"/api/admin/kb/nganh/{lid}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await c.DeleteAsync($"/api/admin/kb/nganh/{lid}")).StatusCode);
    }

    [Fact]
    public async Task Diem_over_scale_is_rejected()
    {
        await f.ResetDatabaseAsync();
        var c = await AuthHelper.AdminClientAsync(f);
        var n = await Nganh(c);
        var pt = await PhuongThuc(c, "thi-thpt", "thi THPT", 30);
        var res = await c.PostAsJsonAsync("/api/admin/kb/diem-chuan", new { nganhLid = n, nam = 2026, phuongThucLid = pt, diem = 850 });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.True((await Json(res)).GetProperty("errors").TryGetProperty("diem", out _));
    }

    [Fact]
    public async Task Dgnl_score_within_scale_is_accepted()
    {
        await f.ResetDatabaseAsync();
        var c = await AuthHelper.AdminClientAsync(f);
        var n = await Nganh(c);
        var pt = await PhuongThuc(c, "dgnl", "ĐGNL", 1200);
        var res = await c.PostAsJsonAsync("/api/admin/kb/diem-chuan", new { nganhLid = n, nam = 2026, phuongThucLid = pt, diem = 850 });
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
    }

    [Fact]
    public async Task Dangling_reference_and_bad_year_are_rejected()
    {
        await f.ResetDatabaseAsync();
        var c = await AuthHelper.AdminClientAsync(f);
        var pt = await PhuongThuc(c, "thi-thpt", "thi THPT", 30);
        var res = await c.PostAsJsonAsync("/api/admin/kb/diem-chuan", new { nganhLid = Guid.NewGuid(), nam = 1999, phuongThucLid = pt, diem = 20 });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        var errors = (await Json(res)).GetProperty("errors");
        Assert.True(errors.TryGetProperty("nganhLid", out _));
        Assert.True(errors.TryGetProperty("nam", out _));
    }

    [Fact]
    public async Task To_hop_validates_ma_and_generates_cach_doc()
    {
        await f.ResetDatabaseAsync();
        var c = await AuthHelper.AdminClientAsync(f);
        var bad = await c.PostAsJsonAsync("/api/admin/kb/to-hop", new { ma = "a0", mon = new[] { "Toán" } });
        Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);
        var ok = await c.PostAsJsonAsync("/api/admin/kb/to-hop", new { ma = "A00", mon = new[] { "Toán", "Lý", "Hóa" } });
        Assert.Equal(HttpStatusCode.Created, ok.StatusCode);
        Assert.Equal("A không không", (await Json(ok)).GetProperty("cachDoc").GetString());
    }

    [Fact]
    public async Task Empty_required_text_and_non_positive_numbers_are_rejected()
    {
        await f.ResetDatabaseAsync();
        var c = await AuthHelper.AdminClientAsync(f);
        var faq = await c.PostAsJsonAsync("/api/admin/kb/faq", new { cauHoi = " ", traLoi = "" });
        Assert.Equal(HttpStatusCode.BadRequest, faq.StatusCode);
        var errors = (await Json(faq)).GetProperty("errors");
        Assert.True(errors.TryGetProperty("cauHoi", out _));
        Assert.True(errors.TryGetProperty("traLoi", out _));

        var n = await Nganh(c);
        var ct = await c.PostAsJsonAsync("/api/admin/kb/chi-tieu", new { nganhLid = n, nam = 2026, soLuong = 0 });
        Assert.Equal(HttpStatusCode.BadRequest, ct.StatusCode);
        var hp = await c.PostAsJsonAsync("/api/admin/kb/hoc-phi", new { nam = 2026, muc = 0, donVi = "TinChi" });
        Assert.Equal(HttpStatusCode.BadRequest, hp.StatusCode);
    }

    [Fact]
    public async Task Duplicate_diem_chuan_returns_409()
    {
        await f.ResetDatabaseAsync();
        var c = await AuthHelper.AdminClientAsync(f);
        var n = await Nganh(c);
        var pt = await PhuongThuc(c, "thi-thpt", "thi THPT", 30);
        var body = new { nganhLid = n, nam = 2026, phuongThucLid = pt, diem = 24.5 };
        Assert.Equal(HttpStatusCode.Created, (await c.PostAsJsonAsync("/api/admin/kb/diem-chuan", body)).StatusCode);
        var dup = await c.PostAsJsonAsync("/api/admin/kb/diem-chuan", body);
        Assert.Equal(HttpStatusCode.Conflict, dup.StatusCode);
        Assert.Equal("Bản ghi đã tồn tại", (await Json(dup)).GetProperty("title").GetString());
    }

    [Fact]
    public async Task Delete_referenced_nganh_returns_409_with_references()
    {
        await f.ResetDatabaseAsync();
        var c = await AuthHelper.AdminClientAsync(f);
        var n = await Nganh(c);
        var pt = await PhuongThuc(c, "thi-thpt", "thi THPT", 30);
        await Create(c, "diem-chuan", new { nganhLid = n, nam = 2026, phuongThucLid = pt, diem = 24.5 });
        var res = await c.DeleteAsync($"/api/admin/kb/nganh/{n}");
        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
        var refs = (await Json(res)).GetProperty("thamChieu").EnumerateArray().Select(x => x.GetString()).ToArray();
        Assert.Contains("Điểm chuẩn Công nghệ thông tin 2026 – thi THPT", refs);
        Assert.Equal(HttpStatusCode.OK, (await c.GetAsync($"/api/admin/kb/nganh/{n}")).StatusCode);
    }

    [Fact]
    public async Task Setting_current_season_unsets_others()
    {
        await f.ResetDatabaseAsync();
        var c = await AuthHelper.AdminClientAsync(f);
        var a = await Create(c, "mua-tuyen-sinh", new { nam = 2025, laHienTai = true });
        var b = await Create(c, "mua-tuyen-sinh", new { nam = 2026, laHienTai = true });
        Assert.False((await Json(await c.GetAsync($"/api/admin/kb/mua-tuyen-sinh/{a}"))).GetProperty("laHienTai").GetBoolean());
        Assert.True((await Json(await c.GetAsync($"/api/admin/kb/mua-tuyen-sinh/{b}"))).GetProperty("laHienTai").GetBoolean());
        var put = await c.PutAsJsonAsync($"/api/admin/kb/mua-tuyen-sinh/{a}", new { nam = 2025, laHienTai = true });
        Assert.Equal(HttpStatusCode.OK, put.StatusCode);
        Assert.False((await Json(await c.GetAsync($"/api/admin/kb/mua-tuyen-sinh/{b}"))).GetProperty("laHienTai").GetBoolean());
    }

    [Fact]
    public async Task List_filters_by_nam()
    {
        await f.ResetDatabaseAsync();
        var c = await AuthHelper.AdminClientAsync(f);
        await Create(c, "mua-tuyen-sinh", new { nam = 2025, laHienTai = false });
        await Create(c, "mua-tuyen-sinh", new { nam = 2026, laHienTai = false });
        var list = await Json(await c.GetAsync("/api/admin/kb/mua-tuyen-sinh?nam=2026"));
        Assert.Equal(1, list.GetArrayLength());
    }

    [Fact]
    public async Task Crud_requires_admin()
    {
        await f.ResetDatabaseAsync();
        var res = await f.CreateClient().GetAsync("/api/admin/kb/nganh");
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }
}
