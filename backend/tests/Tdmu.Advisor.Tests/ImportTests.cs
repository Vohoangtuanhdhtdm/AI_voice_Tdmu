using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Tdmu.Advisor.Api.Infrastructure;
using Tdmu.Advisor.Tests.Infrastructure;

namespace Tdmu.Advisor.Tests;

[Collection("api")]
public class ImportTests(ApiFactory f)
{
    private static async Task<JsonElement> Json(HttpResponseMessage r) => await r.Content.ReadFromJsonAsync<JsonElement>();

    private static async Task<Guid> Create(HttpClient c, string slug, object body)
    {
        var res = await c.PostAsJsonAsync($"/api/admin/kb/{slug}", body);
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        return (await Json(res)).GetProperty("logicalId").GetGuid();
    }

    private async Task<HttpClient> Seeded()
    {
        await f.ResetDatabaseAsync();
        var c = await AuthHelper.AdminClientAsync(f);
        await Create(c, "nganh", new { maNganh = "7480201", ten = "Công nghệ thông tin" });
        await Create(c, "nganh", new { maNganh = "7340101", ten = "Quản trị kinh doanh" });
        await Create(c, "phuong-thuc", new { ma = "thi-thpt", ten = "Thi THPT", thangDiem = 30 });
        await Create(c, "phuong-thuc", new { ma = "dgnl", ten = "ĐGNL", thangDiem = 1200 });
        await Create(c, "to-hop", new { ma = "A00" });
        return c;
    }

    private static MultipartFormDataContent Form(byte[] bytes, string name)
    {
        var form = new MultipartFormDataContent();
        form.Add(new ByteArrayContent(bytes), "file", name);
        return form;
    }

    private static MultipartFormDataContent Csv(string text, string name = "data.csv") =>
        Form(Encoding.UTF8.GetBytes(text), name);

    private static Task<HttpResponseMessage> Post(HttpClient c, string kind, string action, MultipartFormDataContent form) =>
        c.PostAsync($"/api/admin/import/{kind}/{action}", form);

    [Fact]
    public async Task Preview_semicolon_csv_with_bom_and_decimal_comma()
    {
        var c = await Seeded();
        var res = await Post(c, "diem-chuan", "preview",
            Csv("﻿ma_nganh;nam;ma_phuong_thuc;diem\n7480201;2026;thi-thpt;21,5"));
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var j = await Json(res);
        Assert.Equal(1, j.GetProperty("validCount").GetInt32());
        Assert.Equal(0, j.GetProperty("errorCount").GetInt32());
        var row = j.GetProperty("rows")[0];
        Assert.Equal(2, row.GetProperty("rowNumber").GetInt32());
        Assert.Equal(21.5m, decimal.Parse(row.GetProperty("values").GetProperty("diem").GetString()!, System.Globalization.CultureInfo.InvariantCulture));
    }

    [Fact]
    public async Task Preview_comma_csv_with_bom_via_utf8_preamble_and_crlf()
    {
        var c = await Seeded();
        var bytes = new byte[] { 0xEF, 0xBB, 0xBF }.Concat(Encoding.UTF8.GetBytes("MA_NGANH, nam ,ma_phuong_thuc,diem,ghi_chu\r\n7480201,2026,thi-thpt,21.5,\"có, dấu phẩy\"\r\n")).ToArray();
        var res = await Post(c, "diem-chuan", "preview", Form(bytes, "x.csv"));
        var j = await Json(res);
        Assert.Equal(1, j.GetProperty("validCount").GetInt32());
        Assert.Equal("có, dấu phẩy", j.GetProperty("rows")[0].GetProperty("values").GetProperty("ghi_chu").GetString());
    }

    [Fact]
    public async Task Preview_reports_row_errors()
    {
        var c = await Seeded();
        var res = await Post(c, "diem-chuan", "preview", Csv(
            "ma_nganh;nam;ma_phuong_thuc;diem\n7480201;2026;thi-thpt;21,5\n9999999;2026;thi-thpt;20\n7480201;2026;thi-thpt;850\n7340101;2026;thi-thpt;abc"));
        var j = await Json(res);
        Assert.Equal(3, j.GetProperty("errorCount").GetInt32());
        Assert.Equal(1, j.GetProperty("validCount").GetInt32());
        var rows = j.GetProperty("rows");
        Assert.Equal(0, rows[0].GetProperty("errors").GetArrayLength());
        Assert.Equal(3, rows[1].GetProperty("rowNumber").GetInt32());
        Assert.Contains("9999999", rows[1].GetProperty("errors")[0].GetString());
        Assert.Equal(4, rows[2].GetProperty("rowNumber").GetInt32());
        Assert.Equal(5, rows[3].GetProperty("rowNumber").GetInt32());
        Assert.True(rows[3].GetProperty("errors").GetArrayLength() > 0);
    }

    [Fact]
    public async Task Preview_flags_duplicate_keys_in_file()
    {
        var c = await Seeded();
        var res = await Post(c, "diem-chuan", "preview", Csv(
            "ma_nganh,nam,ma_phuong_thuc,diem\n7480201,2026,thi-thpt,21\n7480201,2026,thi-thpt,22"));
        var j = await Json(res);
        Assert.Equal(1, j.GetProperty("errorCount").GetInt32());
        Assert.Equal(3, j.GetProperty("rows")[1].GetProperty("rowNumber").GetInt32());
        Assert.Equal(1, j.GetProperty("rows")[1].GetProperty("errors").GetArrayLength());
    }

    [Fact]
    public async Task Missing_required_column_is_file_level_400()
    {
        var c = await Seeded();
        var res = await Post(c, "diem-chuan", "preview", Csv("ma_nganh;nam;diem\n7480201;2026;21"));
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Contains("ma_phuong_thuc", await res.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Unknown_kind_404_and_bad_extension_400_and_too_large_rejected()
    {
        var c = await Seeded();
        Assert.Equal(HttpStatusCode.NotFound, (await Post(c, "faq", "preview", Csv("a"))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Post(c, "diem-chuan", "preview", Csv("a", "x.txt"))).StatusCode);
        var big = new string('a', 5 * 1024 * 1024 + 10);
        var res = await Post(c, "diem-chuan", "preview", Csv(big));
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, res.StatusCode);
    }

    [Fact]
    public async Task Import_requires_auth()
    {
        await f.ResetDatabaseAsync();
        var res = await f.CreateClient().GetAsync("/api/admin/import/diem-chuan/template");
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task Commit_upserts_into_draft()
    {
        var c = await Seeded();
        var csv1 = "ma_nganh;nam;ma_phuong_thuc;diem;nguon_ten\n7480201;2026;thi-thpt;21,5;Đề án\n7340101;2026;dgnl;800;Đề án";
        var r1 = await Json(await Post(c, "diem-chuan", "commit", Csv(csv1)));
        Assert.Equal(2, r1.GetProperty("inserted").GetInt32());
        Assert.Equal(0, r1.GetProperty("updated").GetInt32());

        var csv2 = "ma_nganh;nam;ma_phuong_thuc;diem;nguon_ten\n7480201;2026;thi-thpt;22;Đề án\n7340101;2026;dgnl;800;Đề án";
        var r2 = await Json(await Post(c, "diem-chuan", "commit", Csv(csv2)));
        Assert.Equal(1, r2.GetProperty("updated").GetInt32());
        Assert.Equal(0, r2.GetProperty("inserted").GetInt32());

        var list = await Json(await c.GetAsync("/api/admin/kb/diem-chuan"));
        Assert.Equal(2, list.GetArrayLength());
        Assert.Contains(list.EnumerateArray(), x => x.GetProperty("diem").GetDecimal() == 22m && x.GetProperty("nguonTen").GetString() == "Đề án");

        using var scope = f.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AdvisorDbContext>();
        Assert.True(await db.AuditLog.AnyAsync(a => a.Entity == "DiemChuan"));
    }

    [Fact]
    public async Task Commit_with_errors_returns_400_with_preview_and_writes_nothing()
    {
        var c = await Seeded();
        var res = await Post(c, "diem-chuan", "commit", Csv(
            "ma_nganh;nam;ma_phuong_thuc;diem\n7480201;2026;thi-thpt;21,5\n9999999;2026;thi-thpt;20"));
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        var j = await Json(res);
        Assert.Equal(1, j.GetProperty("preview").GetProperty("errorCount").GetInt32());
        Assert.Equal(0, (await Json(await c.GetAsync("/api/admin/kb/diem-chuan"))).GetArrayLength());
    }

    [Fact]
    public async Task Chi_tieu_with_optional_phuong_thuc_upserts()
    {
        var c = await Seeded();
        var csv = "ma_nganh,nam,ma_phuong_thuc,so_luong\n7480201,2026,,120\n7480201,2026,thi-thpt,60";
        var r1 = await Json(await Post(c, "chi-tieu", "commit", Csv(csv)));
        Assert.Equal(2, r1.GetProperty("inserted").GetInt32());
        var r2 = await Json(await Post(c, "chi-tieu", "commit", Csv(csv.Replace("120", "130"))));
        Assert.Equal(1, r2.GetProperty("updated").GetInt32());
        Assert.Equal(0, r2.GetProperty("inserted").GetInt32());
    }

    [Fact]
    public async Task Hoc_phi_maps_don_vi_and_upserts_by_nganh_nam_don_vi()
    {
        var c = await Seeded();
        var csv = "ma_nganh;nam;muc;don_vi\n;2026;\"450,5\";tin_chi\n7480201;2026;15000000;nam";
        var bad = await Post(c, "hoc-phi", "commit", Csv(csv));
        Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode); // học phí không có phần thập phân
        var vn = await Json(await Post(c, "hoc-phi", "preview", Csv("ma_nganh;nam;muc;don_vi\n;2026;1.200.000;tin_chi")));
        Assert.Equal("1200000", vn.GetProperty("rows")[0].GetProperty("values").GetProperty("muc").GetString());

        var ok = "ma_nganh;nam;muc;don_vi\n;2026;1200000;tin_chi\n7480201;2026;15000000;nam";
        var r1 = await Json(await Post(c, "hoc-phi", "commit", Csv(ok)));
        Assert.Equal(2, r1.GetProperty("inserted").GetInt32());
        var r2 = await Json(await Post(c, "hoc-phi", "commit", Csv(ok.Replace("1200000", "1300000"))));
        Assert.Equal(1, r2.GetProperty("updated").GetInt32());
        var list = await Json(await c.GetAsync("/api/admin/kb/hoc-phi"));
        Assert.Contains(list.EnumerateArray(), x => x.GetProperty("donVi").GetString() == "TinChi" && x.GetProperty("muc").GetDecimal() == 1300000m);

        var invalidUnit = await Post(c, "hoc-phi", "preview", Csv("ma_nganh;nam;muc;don_vi\n;2026;1;thang"));
        Assert.Equal(1, (await Json(invalidUnit)).GetProperty("errorCount").GetInt32());
    }

    [Fact]
    public async Task Nganh_to_hop_is_insert_only()
    {
        var c = await Seeded();
        var csv = "ma_nganh;nam;ma_to_hop\n7480201;2026;A00";
        var r1 = await Json(await Post(c, "nganh-to-hop", "commit", Csv(csv)));
        Assert.Equal(1, r1.GetProperty("inserted").GetInt32());
        var r2 = await Json(await Post(c, "nganh-to-hop", "commit", Csv(csv)));
        Assert.Equal(0, r2.GetProperty("inserted").GetInt32());
        Assert.Equal(0, r2.GetProperty("updated").GetInt32());
        var bad = await Json(await Post(c, "nganh-to-hop", "preview", Csv("ma_nganh;nam;ma_to_hop\n7480201;2026;Z99")));
        Assert.Equal(1, bad.GetProperty("errorCount").GetInt32());
    }

    [Fact]
    public async Task Xlsx_template_roundtrip()
    {
        var c = await Seeded();
        var tpl = await c.GetAsync("/api/admin/import/diem-chuan/template");
        Assert.Equal(HttpStatusCode.OK, tpl.StatusCode);
        var bytes = await tpl.Content.ReadAsByteArrayAsync();
        using var ms = new MemoryStream(bytes);
        using var wb = new XLWorkbook(ms);
        var ws = wb.Worksheet(1);
        Assert.Equal("ma_nganh", ws.Cell(1, 1).GetString());
        ws.Cell(2, 1).Value = 7480201;       // số thay vì chuỗi
        ws.Cell(2, 2).Value = 2026;
        ws.Cell(2, 3).Value = "thi-thpt";
        ws.Cell(2, 4).Value = 21.5;
        using var outMs = new MemoryStream();
        wb.SaveAs(outMs);
        var res = await Post(c, "diem-chuan", "preview", Form(outMs.ToArray(), "diem.xlsx"));
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var j = await Json(res);
        Assert.Equal(1, j.GetProperty("validCount").GetInt32());
        Assert.Equal(0, j.GetProperty("errorCount").GetInt32());
        Assert.Equal("21.5", j.GetProperty("rows")[0].GetProperty("values").GetProperty("diem").GetString());
    }

    [Theory]
    [InlineData("diem-chuan")]
    [InlineData("chi-tieu")]
    [InlineData("hoc-phi")]
    [InlineData("nganh-to-hop")]
    public async Task Templates_available_for_every_kind(string kind)
    {
        var c = await Seeded();
        var res = await c.GetAsync($"/api/admin/import/{kind}/template");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        Assert.Contains("spreadsheetml", res.Content.Headers.ContentType!.MediaType);
    }

    [Fact]
    public async Task Reimport_without_optional_columns_keeps_existing_values()
    {
        var c = await Seeded();
        await Post(c, "diem-chuan", "commit", Csv("ma_nganh,nam,ma_phuong_thuc,diem,ghi_chu,nguon_ten\n7480201,2026,thi-thpt,21,Ghi chú gốc,Đề án"));
        var r = await Json(await Post(c, "diem-chuan", "commit", Csv("ma_nganh,nam,ma_phuong_thuc,diem\n7480201,2026,thi-thpt,23")));
        Assert.Equal(1, r.GetProperty("updated").GetInt32());
        var row = (await Json(await c.GetAsync("/api/admin/kb/diem-chuan")))[0];
        Assert.Equal(23m, row.GetProperty("diem").GetDecimal());
        Assert.Equal("Ghi chú gốc", row.GetProperty("ghiChu").GetString());
        Assert.Equal("Đề án", row.GetProperty("nguonTen").GetString());

        // cột có mặt nhưng ô trống thì xóa giá trị
        await Post(c, "diem-chuan", "commit", Csv("ma_nganh,nam,ma_phuong_thuc,diem,ghi_chu\n7480201,2026,thi-thpt,23,"));
        row = (await Json(await c.GetAsync("/api/admin/kb/diem-chuan")))[0];
        Assert.Equal(JsonValueKind.Null, row.GetProperty("ghiChu").ValueKind);
        Assert.Equal("Đề án", row.GetProperty("nguonTen").GetString());
    }

    [Fact]
    public async Task Import_never_touches_released_rows()
    {
        var c = await Seeded();
        await Post(c, "diem-chuan", "commit", Csv("ma_nganh,nam,ma_phuong_thuc,diem\n7480201,2026,thi-thpt,21"));
        var rel = await c.PostAsJsonAsync("/api/admin/releases", new { ghiChu = "v1" });
        Assert.Equal(HttpStatusCode.Created, rel.StatusCode);
        var r = await Json(await Post(c, "diem-chuan", "commit", Csv("ma_nganh,nam,ma_phuong_thuc,diem\n7480201,2026,thi-thpt,25")));
        Assert.Equal(1, r.GetProperty("updated").GetInt32());
        Assert.Equal(0, r.GetProperty("inserted").GetInt32());

        using var scope = f.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AdvisorDbContext>();
        var released = await db.DiemChuan.AsNoTracking().Where(x => x.ReleaseId != null).ToListAsync();
        var draft = await db.DiemChuan.AsNoTracking().Where(x => x.ReleaseId == null).ToListAsync();
        Assert.Equal(21m, Assert.Single(released).Diem);
        Assert.Equal(25m, Assert.Single(draft).Diem);
    }
}
