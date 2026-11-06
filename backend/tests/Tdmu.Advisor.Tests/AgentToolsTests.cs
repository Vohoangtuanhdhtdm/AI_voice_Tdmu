using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Pgvector;
using Tdmu.Advisor.Api.Features.Agent.Tools;
using Tdmu.Advisor.Api.Features.Conversations;
using Tdmu.Advisor.Api.Features.KnowledgeBase;
using Tdmu.Advisor.Api.Features.KnowledgeBase.Entities;
using Tdmu.Advisor.Api.Infrastructure;
using Tdmu.Advisor.Tests.Infrastructure;

namespace Tdmu.Advisor.Tests;

[Collection("api")]
public class AgentToolsTests(ApiFactory f)
{
    private static async Task<Guid> Create(HttpClient c, string slug, object body)
    {
        var res = await c.PostAsJsonAsync($"/api/admin/kb/{slug}", body);
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        return (await res.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("logicalId").GetGuid();
    }

    private static async Task Publish(HttpClient c)
    {
        var res = await c.PostAsJsonAsync("/api/admin/releases", new { ghiChu = "x" });
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
    }

    private async Task<JsonObject> Invoke(string tool, object args, KbScope scope = KbScope.Published, Guid conversationId = default)
    {
        using var s = f.Services.CreateScope();
        var reg = s.ServiceProvider.GetRequiredService<ToolRegistry>();
        var a = JsonSerializer.SerializeToNode(args)!.AsObject();
        return (await reg.InvokeAsync(tool, a, new ToolContext(scope, conversationId), default)).ToJson();
    }

    private static string KetQua(JsonObject r) => (string)r["ket_qua"]!;

    /// <summary>Mùa 2026 hiện tại, ngành CNTT (alias CNTT), Công nghệ thực phẩm (gây nhiễu cho so khớp gần đúng), phương thức THPT + ĐGNL.</summary>
    private async Task<(HttpClient C, Guid Cntt, Guid Thpt, Guid Dgnl)> SeedAsync()
    {
        await f.ResetDatabaseAsync();
        var c = await AuthHelper.AdminClientAsync(f);
        await Create(c, "mua-tuyen-sinh", new { nam = 2026, laHienTai = true });
        var cntt = await Create(c, "nganh", new { maNganh = "7480201", ten = "Công nghệ thông tin", tenGoiKhac = new[] { "CNTT", "IT" } });
        await Create(c, "nganh", new { maNganh = "7540101", ten = "Công nghệ thực phẩm" });
        var thpt = await Create(c, "phuong-thuc", new { ma = "THPT", ten = "Xét điểm thi tốt nghiệp THPT", thangDiem = 30 });
        var dgnl = await Create(c, "phuong-thuc", new { ma = "DGNL", ten = "Đánh giá năng lực", thangDiem = 1200 });
        return (c, cntt, thpt, dgnl);
    }

    [Theory]
    [InlineData("cong nghe thong tin")]
    [InlineData("CNTT")]
    [InlineData("cntt")]
    [InlineData("cong nghe thong tinn")]
    [InlineData("7480201")]
    public async Task Tim_nganh_matches_unaccented_alias_and_typo(string tuKhoa)
    {
        var (c, _, _, _) = await SeedAsync();
        await Publish(c);
        var r = await Invoke("tim_nganh", new { tu_khoa = tuKhoa });
        Assert.Equal("ok", KetQua(r));
        Assert.Equal("7480201", (string)r["du_lieu"]!["ma_nganh"]!);
        Assert.Equal("Công nghệ thông tin", (string)r["du_lieu"]!["ten"]!);
        Assert.True((bool)r["du_lieu"]!["con_tuyen"]!);
        // Chỉ khớp gần đúng (trigram) mới yêu cầu xác nhận lại ngành.
        if (tuKhoa == "cong nghe thong tinn")
            Assert.Equal("Đây là ngành gần đúng nhất với từ người dùng nói ('cong nghe thong tinn'): Công nghệ thông tin. Hãy xác nhận đúng ngành với người dùng trước khi đọc số liệu.", (string)r["huong_dan"]!);
        else
            Assert.Null((string?)r["huong_dan"]);
    }

    [Fact]
    public async Task Similarity_match_echoes_nganh_and_asks_to_confirm()
    {
        await f.ResetDatabaseAsync();
        var c = await AuthHelper.AdminClientAsync(f);
        await Create(c, "mua-tuyen-sinh", new { nam = 2026, laHienTai = true });
        var cntt = await Create(c, "nganh", new { maNganh = "7480201", ten = "Công nghệ thông tin" });
        var thpt = await Create(c, "phuong-thuc", new { ma = "THPT", ten = "thi THPT", thangDiem = 30 });
        await Create(c, "diem-chuan", new { nganhLid = cntt, nam = 2026, phuongThucLid = thpt, diem = 21.5 });
        await Publish(c);

        var r = await Invoke("tra_diem_chuan", new { nganh = "cong nghe sinh hoc" });
        if (KetQua(r) == "khong_tim_thay") return; // dưới ngưỡng similarity: cũng chấp nhận được
        Assert.Equal("ok", KetQua(r));
        Assert.Equal("Công nghệ thông tin", (string)r["du_lieu"]!["nganh"]!["ten"]!);
        Assert.StartsWith("Ngành Công nghệ thông tin: ", (string)r["cach_doc"]!);
        Assert.Equal("Đây là ngành gần đúng nhất với từ người dùng nói ('cong nghe sinh hoc'): Công nghệ thông tin. Hãy xác nhận đúng ngành với người dùng trước khi đọc số liệu.", (string)r["huong_dan"]!);
    }

    [Fact]
    public async Task Tim_nganh_ambiguous_returns_can_hoi_lai()
    {
        await f.ResetDatabaseAsync();
        var c = await AuthHelper.AdminClientAsync(f);
        await Create(c, "nganh", new { maNganh = "7480103", ten = "Kỹ thuật phần mềm" });
        await Create(c, "nganh", new { maNganh = "7520201", ten = "Kỹ thuật điện" });
        await Create(c, "nganh", new { maNganh = "7380101", ten = "Luật" });
        await Publish(c);
        var r = await Invoke("tim_nganh", new { tu_khoa = "ky thuat" });
        Assert.Equal("can_hoi_lai", KetQua(r));
        var options = r["du_lieu"]!["lua_chon"]!.AsArray();
        Assert.Equal(2, options.Count);
        Assert.Contains(options, o => (string)o!["ten"]! == "Kỹ thuật phần mềm");
        Assert.Contains(options, o => (string)o!["ten"]! == "Kỹ thuật điện");
        Assert.False(string.IsNullOrWhiteSpace((string?)r["huong_dan"]));

        // Tên trùng khớp hoàn toàn được ưu tiên hơn các ngành chỉ chứa từ khóa.
        Assert.Equal("ok", KetQua(await Invoke("tim_nganh", new { tu_khoa = "Kỹ thuật điện" })));
    }

    [Fact]
    public async Task Tra_diem_chuan_returns_reading_and_sources()
    {
        var (c, cntt, thpt, dgnl) = await SeedAsync();
        await Create(c, "diem-chuan", new { nganhLid = cntt, nam = 2026, phuongThucLid = thpt, diem = 21.5, nguonTen = "Thông báo điểm chuẩn 2026", nguonUrl = "https://x.test/dc" });
        await Create(c, "diem-chuan", new { nganhLid = cntt, nam = 2026, phuongThucLid = dgnl, diem = 750, nguonTen = "Thông báo điểm chuẩn 2026", nguonUrl = "https://x.test/dc" });
        await Create(c, "diem-chuan", new { nganhLid = cntt, nam = 2025, phuongThucLid = thpt, diem = 20 });
        await Publish(c);

        var r = await Invoke("tra_diem_chuan", new { nganh = "cntt" });
        Assert.Equal("ok", KetQua(r));
        Assert.Equal(2026, (int)r["nam_ap_dung"]!);
        Assert.Equal("7480201", (string)r["du_lieu"]!["nganh"]!["ma_nganh"]!);
        Assert.Equal("Công nghệ thông tin", (string)r["du_lieu"]!["nganh"]!["ten"]!);
        Assert.Equal(2, r["du_lieu"]!["diem_chuan"]!.AsArray().Count);
        Assert.StartsWith("Ngành Công nghệ thông tin: ", (string)r["cach_doc"]!);
        Assert.Contains("hai mươi mốt phẩy năm điểm", (string)r["cach_doc"]!);
        Assert.Contains("bảy trăm năm mươi điểm", (string)r["cach_doc"]!);
        var nguon = r["nguon"]!.AsArray();
        Assert.Single(nguon); // bỏ trùng theo (ten, url)
        Assert.Equal("Thông báo điểm chuẩn 2026", (string)nguon[0]!["ten"]!);
        Assert.Equal("https://x.test/dc", (string)nguon[0]!["url"]!);

        // Lọc theo phương thức (không dấu, theo tên) và năm truyền dạng chuỗi.
        var thptOnly = await Invoke("tra_diem_chuan", new { nganh = "cong nghe thong tin", phuong_thuc = "thi tot nghiep thpt" });
        var row = Assert.Single(thptOnly["du_lieu"]!["diem_chuan"]!.AsArray())!;
        Assert.Equal(21.5m, (decimal)row["diem"]!);
        Assert.Equal(30m, (decimal)row["thang_diem"]!);
        var y2025 = await Invoke("tra_diem_chuan", new { nganh = "7480201", nam = "2025" });
        Assert.Equal(2025, (int)y2025["nam_ap_dung"]!);
        Assert.Equal(20m, (decimal)Assert.Single(y2025["du_lieu"]!["diem_chuan"]!.AsArray())!["diem"]!);

        // Phương thức không có trong CSDL: hỏi lại kèm danh sách phương thức.
        var unknownPt = await Invoke("tra_diem_chuan", new { nganh = "cntt", phuong_thuc = "học bạ" });
        Assert.Equal("can_hoi_lai", KetQua(unknownPt));
        Assert.Equal(2, unknownPt["du_lieu"]!["lua_chon"]!.AsArray().Count);
    }

    [Fact]
    public async Task Not_found_has_huong_dan()
    {
        var (c, _, _, _) = await SeedAsync();
        await Publish(c);
        foreach (var (tool, args) in new (string, object)[]
                 {
                     ("tim_nganh", new { tu_khoa = "thiên văn học vũ trụ" }),
                     ("tra_diem_chuan", new { nganh = "cntt" }),
                     ("tra_chi_tieu", new { nganh = "cntt" }),
                     ("tra_hoc_phi", new { nganh = "cntt" }),
                     ("tra_lich_tuyen_sinh", new { }),
                     ("tra_to_hop", new { nganh = "cntt" }),
                 })
        {
            var r = await Invoke(tool, args);
            Assert.True(KetQua(r) == "khong_tim_thay", $"{tool}: {r.ToJsonString()}");
            var hd = (string)r["huong_dan"]!;
            Assert.Contains("trong cơ sở dữ liệu", hd);
            Assert.Contains("Không được suy ra là trường không có", hd);
        }
    }

    [Fact]
    public async Task Draft_scope_reads_draft()
    {
        var (c, _, _, _) = await SeedAsync();
        await Publish(c);
        await Create(c, "nganh", new { maNganh = "7340101", ten = "Quản trị kinh doanh" });
        Assert.Equal("ok", KetQua(await Invoke("tim_nganh", new { tu_khoa = "quan tri kinh doanh" }, KbScope.Draft)));
        Assert.Equal("khong_tim_thay", KetQua(await Invoke("tim_nganh", new { tu_khoa = "quan tri kinh doanh" })));
    }

    [Fact]
    public async Task Published_scope_without_release_is_not_found()
    {
        await f.ResetDatabaseAsync();
        Assert.Equal("khong_tim_thay", KetQua(await Invoke("tim_nganh", new { tu_khoa = "cntt" })));
    }

    private sealed class SlowTool : IAgentTool
    {
        public string Name => "cham";
        public JsonObject Declaration => new() { ["name"] = Name, ["description"] = "x", ["parameters"] = new JsonObject { ["type"] = "object" } };
        public async Task<ToolResult> InvokeAsync(JsonObject args, ToolContext ctx, CancellationToken ct)
        {
            await Task.Delay(TimeSpan.FromSeconds(5), ct);
            return ToolResult.Ok(new JsonObject());
        }
    }

    private sealed class StubbornTool : IAgentTool
    {
        public string Name => "lo_token";
        public JsonObject Declaration => new() { ["name"] = Name, ["description"] = "x", ["parameters"] = new JsonObject { ["type"] = "object" } };
        public async Task<ToolResult> InvokeAsync(JsonObject args, ToolContext ctx, CancellationToken ct)
        {
            await Task.Delay(TimeSpan.FromSeconds(5), CancellationToken.None); // bỏ qua token
            return ToolResult.Ok(new JsonObject());
        }
    }

    private sealed class ThrowingTool : IAgentTool
    {
        public string Name => "loi";
        public JsonObject Declaration => new() { ["name"] = Name, ["description"] = "x", ["parameters"] = new JsonObject { ["type"] = "object" } };
        public Task<ToolResult> InvokeAsync(JsonObject args, ToolContext ctx, CancellationToken ct) => throw new InvalidOperationException("boom");
    }

    [Theory]
    [InlineData("cham")]
    [InlineData("lo_token")]
    public async Task Tool_timeout_returns_loi_he_thong(string name)
    {
        await using var app = f.WithWebHostBuilder(b => b.ConfigureTestServices(s =>
        {
            s.AddScoped<IAgentTool, SlowTool>();
            s.AddScoped<IAgentTool, StubbornTool>();
        }));
        using var scope = app.Services.CreateScope();
        var reg = scope.ServiceProvider.GetRequiredService<ToolRegistry>();
        var sw = Stopwatch.StartNew();
        var r = await reg.InvokeAsync(name, new JsonObject(), new ToolContext(KbScope.Draft, Guid.Empty), default);
        sw.Stop();
        Assert.Equal("loi_he_thong", r.KetQua);
        Assert.True(sw.Elapsed < TimeSpan.FromSeconds(3.5), $"took {sw.Elapsed}");
        Assert.True(sw.Elapsed >= TimeSpan.FromSeconds(2.9), $"took {sw.Elapsed}");
        Assert.False(string.IsNullOrWhiteSpace(r.HuongDan));
    }

    /// <summary>Bỏ qua token, đang chạy truy vấn trên DbContext của nó khi registry đã trả lỗi, rồi còn ghi tiếp.</summary>
    private sealed class StubbornDbTool(AdvisorDbContext db) : IAgentTool
    {
        public static TaskCompletionSource Done = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public string Name => "db_cham";
        public JsonObject Declaration => new() { ["name"] = Name, ["description"] = "x", ["parameters"] = new JsonObject { ["type"] = "object" } };
        public async Task<ToolResult> InvokeAsync(JsonObject args, ToolContext ctx, CancellationToken ct)
        {
            try
            {
                await db.Database.ExecuteSqlRawAsync("SELECT pg_sleep(4)", CancellationToken.None);
                db.Set<Conversation>().Add(new Conversation { Id = Guid.NewGuid(), KenhDauTien = "tool", BatDau = DateTime.UtcNow });
                await db.SaveChangesAsync(CancellationToken.None);
                return ToolResult.Ok(new JsonObject());
            }
            finally { Done.TrySetResult(); }
        }
    }

    /// <summary>Như chuyen_tiep_can_bo bị hết giờ giữa Add và SaveChanges.</summary>
    private sealed class TicketAddThenSlowTool(AdvisorDbContext db) : IAgentTool
    {
        public string Name => "ticket_cham";
        public JsonObject Declaration => new() { ["name"] = Name, ["description"] = "x", ["parameters"] = new JsonObject { ["type"] = "object" } };
        public async Task<ToolResult> InvokeAsync(JsonObject args, ToolContext ctx, CancellationToken ct)
        {
            db.Set<Ticket>().Add(new Ticket { Id = Guid.NewGuid(), HoTen = "Ma", LienHe = "0912345678", LyDo = "x" });
            await Task.Delay(TimeSpan.FromSeconds(5), ct);
            await db.SaveChangesAsync(ct);
            return ToolResult.Ok(new JsonObject());
        }
    }

    [Fact]
    public async Task Timed_out_tool_does_not_share_the_request_db_context()
    {
        await f.ResetDatabaseAsync();
        StubbornDbTool.Done = new(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var app = f.WithWebHostBuilder(b => b.ConfigureTestServices(s => s.AddScoped<IAgentTool, StubbornDbTool>()));
        using var scope = app.Services.CreateScope();
        var reg = scope.ServiceProvider.GetRequiredService<ToolRegistry>();
        var db = scope.ServiceProvider.GetRequiredService<AdvisorDbContext>();

        var r = await reg.InvokeAsync("db_cham", new JsonObject(), new ToolContext(KbScope.Draft, Guid.Empty), default);
        Assert.Equal("loi_he_thong", r.KetQua);
        // Tool vẫn đang chạy truy vấn; request dùng DbContext của mình ngay sau đó không được lỗi "second operation".
        try
        {
            db.Set<Conversation>().Add(new Conversation { Id = Guid.NewGuid(), KenhDauTien = "chat", BatDau = DateTime.UtcNow });
            await db.SaveChangesAsync();
            Assert.False(StubbornDbTool.Done.Task.IsCompleted);
        }
        finally { await StubbornDbTool.Done.Task.WaitAsync(TimeSpan.FromSeconds(10)); }
    }

    [Fact]
    public async Task Timed_out_ticket_is_not_saved_by_a_later_request_save()
    {
        await f.ResetDatabaseAsync();
        await using var app = f.WithWebHostBuilder(b => b.ConfigureTestServices(s => s.AddScoped<IAgentTool, TicketAddThenSlowTool>()));
        using var scope = app.Services.CreateScope();
        var reg = scope.ServiceProvider.GetRequiredService<ToolRegistry>();
        var db = scope.ServiceProvider.GetRequiredService<AdvisorDbContext>();

        var r = await reg.InvokeAsync("ticket_cham", new JsonObject(), new ToolContext(KbScope.Draft, Guid.Empty), default);
        Assert.Equal("loi_he_thong", r.KetQua);
        db.Set<Conversation>().Add(new Conversation { Id = Guid.NewGuid(), KenhDauTien = "chat", BatDau = DateTime.UtcNow });
        await db.SaveChangesAsync();

        using var check = f.Services.CreateScope();
        Assert.Empty(check.ServiceProvider.GetRequiredService<AdvisorDbContext>().Set<Ticket>().ToList());
    }

    [Fact]
    public async Task Unknown_tool_and_exception_return_loi_he_thong()
    {
        await using var app = f.WithWebHostBuilder(b => b.ConfigureTestServices(s => s.AddScoped<IAgentTool, ThrowingTool>()));
        using var scope = app.Services.CreateScope();
        var reg = scope.ServiceProvider.GetRequiredService<ToolRegistry>();
        var ctx = new ToolContext(KbScope.Draft, Guid.Empty);
        Assert.Equal("loi_he_thong", (await reg.InvokeAsync("khong_ton_tai", new JsonObject(), ctx, default)).KetQua);
        Assert.Equal("loi_he_thong", (await reg.InvokeAsync("loi", new JsonObject(), ctx, default)).KetQua);
    }

    [Fact]
    public async Task Chuyen_tiep_requires_contact_then_creates_ticket()
    {
        await f.ResetDatabaseAsync();
        Guid convId;
        using (var s = f.Services.CreateScope())
            convId = (await s.ServiceProvider.GetRequiredService<ConversationStore>().GetOrCreateAsync(null, "chat", false, null, null, default)).Id;

        var missing = await Invoke("chuyen_tiep_can_bo", new { ly_do = "Hỏi học bổng", ho_ten = "Nguyễn Văn An" }, conversationId: convId);
        Assert.Equal("can_hoi_lai", KetQua(missing));
        Assert.Equal("Hãy hỏi họ tên và số điện thoại hoặc email của người dùng trước.", (string)missing["huong_dan"]!);

        // Liên hệ không giống số điện thoại (≥ 9 chữ số) hay email: hỏi lại.
        foreach (var bad in new[] { "abc", "0912 34" })
        {
            var r = await Invoke("chuyen_tiep_can_bo", new { ly_do = "x", ho_ten = "An", lien_he = bad }, conversationId: convId);
            Assert.Equal("can_hoi_lai", KetQua(r));
            Assert.Equal("Hãy hỏi họ tên và số điện thoại hoặc email của người dùng trước.", (string)r["huong_dan"]!);
        }
        Assert.Equal("ok", KetQua(await Invoke("chuyen_tiep_can_bo", new { ly_do = "x", ho_ten = "An", lien_he = "an@x.vn" }, conversationId: convId)));

        var ok = await Invoke("chuyen_tiep_can_bo", new { ly_do = "Hỏi học bổng", ho_ten = "Nguyễn Văn An", lien_he = "0912345678" }, conversationId: convId);
        Assert.Equal("ok", KetQua(ok));
        Assert.Equal("Cán bộ tuyển sinh sẽ liên hệ lại trong giờ hành chính.", (string)ok["du_lieu"]!["thong_bao"]!);
        var ma = (string)ok["du_lieu"]!["ma_yeu_cau"]!;
        Assert.Matches("^[0-9A-F]{8}$", ma);

        using var scope = f.Services.CreateScope();
        var t = scope.ServiceProvider.GetRequiredService<AdvisorDbContext>().Set<Ticket>().Single(x => x.LienHe == "0912345678");
        Assert.Equal(t.Id.ToString("N")[..8].ToUpperInvariant(), ma);
        Assert.Equal(convId, t.ConversationId);
        Assert.Equal("Nguyễn Văn An", t.HoTen);
        Assert.Equal("0912345678", t.LienHe);
        Assert.Equal("Hỏi học bổng", t.LyDo);
    }

    [Fact]
    public void Declarations_have_all_8_tools_with_object_parameters()
    {
        using var s = f.Services.CreateScope();
        var decls = s.ServiceProvider.GetRequiredService<ToolRegistry>().Declarations();
        var names = decls.Select(d => (string)d!["name"]!).OrderBy(x => x).ToList();
        Assert.Equal(new[] { "chuyen_tiep_can_bo", "tim_kiem_tri_thuc", "tim_nganh", "tra_chi_tieu", "tra_diem_chuan", "tra_hoc_phi", "tra_lich_tuyen_sinh", "tra_to_hop" }, names);
        foreach (var d in decls)
        {
            Assert.False(string.IsNullOrWhiteSpace((string?)d!["description"]));
            var p = d["parameters"]!.AsObject();
            Assert.Equal("object", (string)p["type"]!);
            var props = p["properties"]!.AsObject();
            Assert.NotEmpty(props);
            foreach (var req in p["required"]?.AsArray() ?? [])
                Assert.True(props.ContainsKey((string)req!), $"{d["name"]}: required {req} không có trong properties");
        }
        var diem = decls.Single(d => (string)d!["name"]! == "tra_diem_chuan")!["parameters"]!;
        Assert.Equal(["nganh"], diem["required"]!.AsArray().Select(x => (string)x!));
        var chuyen = decls.Single(d => (string)d!["name"]! == "chuyen_tiep_can_bo")!["parameters"]!;
        Assert.Equal(["ly_do"], chuyen["required"]!.AsArray().Select(x => (string)x!));
        Assert.True(diem["properties"]!.AsObject().ContainsKey("nam"));
        Assert.True(diem["properties"]!.AsObject().ContainsKey("phuong_thuc"));
    }

    [Fact]
    public async Task Tra_chi_tieu_and_hoc_phi_read_numbers()
    {
        var (c, cntt, thpt, _) = await SeedAsync();
        await Create(c, "chi-tieu", new { nganhLid = cntt, nam = 2026, phuongThucLid = thpt, soLuong = 120, nguonTen = "Đề án 2026" });
        await Create(c, "chi-tieu", new { nganhLid = cntt, nam = 2026, soLuong = 300, nguonTen = "Đề án 2026" });
        await Create(c, "hoc-phi", new { nganhLid = cntt, nam = 2026, muc = 1200000, donVi = "TinChi", nguonTen = "Quyết định học phí" });
        await Create(c, "hoc-phi", new { nam = 2026, muc = 25000000, donVi = "Nam", nguonTen = "Quyết định học phí" });
        await Publish(c);

        var ct = await Invoke("tra_chi_tieu", new { nganh = "cntt" });
        Assert.Equal("ok", KetQua(ct));
        Assert.Equal("Công nghệ thông tin", (string)ct["du_lieu"]!["nganh"]!["ten"]!);
        Assert.Equal(2, ct["du_lieu"]!["chi_tieu"]!.AsArray().Count);
        Assert.StartsWith("Ngành Công nghệ thông tin: ", (string)ct["cach_doc"]!);
        Assert.Contains("một trăm hai mươi", (string)ct["cach_doc"]!);
        Assert.Contains("ba trăm", (string)ct["cach_doc"]!);
        Assert.Equal("Đề án 2026", (string)ct["nguon"]![0]!["ten"]!);
        var ctThpt = await Invoke("tra_chi_tieu", new { nganh = "cntt", phuong_thuc = "THPT" });
        Assert.Equal(120, (int)Assert.Single(ctThpt["du_lieu"]!["chi_tieu"]!.AsArray())!["so_luong"]!);

        var hp = await Invoke("tra_hoc_phi", new { nganh = "cntt", nam = 2026 });
        Assert.Equal("ok", KetQua(hp));
        Assert.Equal(2026, (int)hp["nam_ap_dung"]!);
        Assert.Equal("Công nghệ thông tin", (string)hp["du_lieu"]!["nganh"]!["ten"]!);
        Assert.Equal(2, hp["du_lieu"]!["hoc_phi"]!.AsArray().Count);
        Assert.StartsWith("Ngành Công nghệ thông tin: ", (string)hp["cach_doc"]!);
        Assert.Contains("một triệu hai trăm nghìn đồng", (string)hp["cach_doc"]!);
        Assert.Contains("hai mươi lăm triệu đồng", (string)hp["cach_doc"]!);
        var general = await Invoke("tra_hoc_phi", new { });
        Assert.Equal(25000000m, (decimal)Assert.Single(general["du_lieu"]!["hoc_phi"]!.AsArray())!["muc"]!);
    }

    [Fact]
    public async Task Tra_lich_converts_to_vietnam_time()
    {
        var (c, _, thpt, dgnl) = await SeedAsync();
        await Create(c, "moc-thoi-gian", new { nam = 2026, tenMoc = "Đăng ký xét tuyển", batDau = "2026-07-01T01:00:00Z", ketThuc = "2026-07-15T10:00:00Z", nguonTen = "Kế hoạch tuyển sinh" });
        await Create(c, "moc-thoi-gian", new { nam = 2026, tenMoc = "Thi ĐGNL", batDau = "2026-05-10T00:30:00Z", phuongThucLid = dgnl });
        await Create(c, "moc-thoi-gian", new { nam = 2025, tenMoc = "Cũ", batDau = "2025-05-10T00:30:00Z" });
        await Publish(c);

        var r = await Invoke("tra_lich_tuyen_sinh", new { });
        Assert.Equal("ok", KetQua(r));
        var rows = r["du_lieu"]!.AsArray();
        Assert.Equal(2, rows.Count);
        var dk = rows.Single(x => (string)x!["ten_moc"]! == "Đăng ký xét tuyển")!;
        Assert.Equal("01/07/2026 08:00", (string)dk["bat_dau"]!);
        Assert.Equal("15/07/2026 17:00", (string)dk["ket_thuc"]!);
        Assert.Contains("01/07/2026 08:00", (string)r["cach_doc"]!);

        // Lọc phương thức THPT: giữ mốc chung, bỏ mốc riêng của ĐGNL.
        var thptOnly = await Invoke("tra_lich_tuyen_sinh", new { phuong_thuc = "THPT" });
        Assert.Equal("Đăng ký xét tuyển", (string)Assert.Single(thptOnly["du_lieu"]!.AsArray())!["ten_moc"]!);
        _ = thpt;
    }

    [Fact]
    public async Task Tra_to_hop_both_directions()
    {
        var (c, cntt, _, _) = await SeedAsync();
        var a00 = await Create(c, "to-hop", new { ma = "A00", mon = new[] { "Toán", "Vật lý", "Hóa học" }, nguonTen = "Đề án 2026" });
        var a01 = await Create(c, "to-hop", new { ma = "A01", mon = new[] { "Toán", "Vật lý", "Tiếng Anh" } });
        await Create(c, "to-hop", new { ma = "D07", mon = new[] { "Toán", "Hóa học", "Tiếng Anh" } });
        await Create(c, "nganh-to-hop", new { nganhLid = cntt, nam = 2026, toHopLid = a00 });
        await Create(c, "nganh-to-hop", new { nganhLid = cntt, nam = 2026, toHopLid = a01 });
        await Publish(c);

        var byNganh = await Invoke("tra_to_hop", new { nganh = "cntt" });
        Assert.Equal("ok", KetQua(byNganh));
        Assert.Equal(2, byNganh["du_lieu"]!["to_hop"]!.AsArray().Count);
        Assert.Equal("Công nghệ thông tin", (string)byNganh["du_lieu"]!["nganh"]!["ten"]!);
        Assert.StartsWith("Ngành Công nghệ thông tin: ", (string)byNganh["cach_doc"]!);
        Assert.Contains("A không không", (string)byNganh["cach_doc"]!);

        var byToHop = await Invoke("tra_to_hop", new { to_hop = "a00" });
        Assert.Equal("ok", KetQua(byToHop));
        Assert.Equal("7480201", (string)Assert.Single(byToHop["du_lieu"]!["nganh"]!.AsArray())!["ma_nganh"]!);
        Assert.Equal("A không không", (string)byToHop["cach_doc"]!);

        var both = await Invoke("tra_to_hop", new { nganh = "cntt", to_hop = "D07" });
        Assert.Equal("ok", KetQua(both));
        Assert.False((bool)both["du_lieu"]!["co_xet_to_hop"]!);

        Assert.Equal("can_hoi_lai", KetQua(await Invoke("tra_to_hop", new { })));
        Assert.Equal("khong_tim_thay", KetQua(await Invoke("tra_to_hop", new { to_hop = "D07" })));
    }

    [Fact]
    public async Task Tim_kiem_tri_thuc_maps_search_kinds()
    {
        await f.ResetDatabaseAsync();
        var v = new float[FakeGeminiClient.Dimensions]; v[0] = 1f;
        using (var s = f.Services.CreateScope())
        {
            var db = s.ServiceProvider.GetRequiredService<AdvisorDbContext>();
            db.Faq.Add(new Faq { CauHoi = "Có ký túc xá không?", TraLoi = "Có, 2.000 chỗ", Embedding = new Vector(v), NguonTen = "Sổ tay sinh viên" });
            await db.SaveChangesAsync();
        }
        f.Gemini.EmbedOverride = _ => v;
        var faq = await Invoke("tim_kiem_tri_thuc", new { cau_hoi = "ký túc xá" }, KbScope.Draft);
        Assert.Equal("ok", KetQua(faq));
        Assert.Equal("Có, 2.000 chỗ", (string)faq["du_lieu"]!["tra_loi"]!);
        Assert.Equal("Sổ tay sinh viên", (string)faq["nguon"]![0]!["ten"]!);

        var none = await Invoke("tim_kiem_tri_thuc", new { cau_hoi = "   " }, KbScope.Draft);
        Assert.Equal("khong_tim_thay", KetQua(none));
        Assert.Contains("thông tin này", (string)none["huong_dan"]!);
    }
}
