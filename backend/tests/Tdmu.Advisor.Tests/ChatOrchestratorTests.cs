using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Tdmu.Advisor.Api.Features.Agent.Chat;
using Tdmu.Advisor.Api.Features.Agent.Tools;
using Tdmu.Advisor.Api.Features.Conversations;
using Tdmu.Advisor.Api.Features.KnowledgeBase;
using Tdmu.Advisor.Api.Features.KnowledgeBase.Entities;
using Tdmu.Advisor.Api.Infrastructure;
using Tdmu.Advisor.Api.Infrastructure.Gemini;
using Tdmu.Advisor.Tests.Infrastructure;

namespace Tdmu.Advisor.Tests;

[Collection("api")]
public class ChatOrchestratorTests(ApiFactory f)
{
    private const string Fallback = "Xin lỗi, mình chưa tìm được câu trả lời phù hợp. Bạn có muốn để lại thông tin để cán bộ tuyển sinh liên hệ không?";
    private const string Busy = "Hệ thống đang bận, bạn vui lòng thử lại sau ít phút hoặc để lại thông tin để cán bộ tuyển sinh liên hệ.";

    private static async Task<Guid> Create(HttpClient c, string slug, object body)
    {
        var res = await c.PostAsJsonAsync($"/api/admin/kb/{slug}", body);
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        return (await res.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("logicalId").GetGuid();
    }

    /// <summary>Mùa 2026, ngành CNTT, điểm chuẩn THPT có nguồn; publish nếu được yêu cầu.</summary>
    private async Task SeedAsync(bool publish = true)
    {
        await f.ResetDatabaseAsync();
        var c = await AuthHelper.AdminClientAsync(f);
        await Create(c, "mua-tuyen-sinh", new { nam = 2026, laHienTai = true });
        var cntt = await Create(c, "nganh", new { maNganh = "7480201", ten = "Công nghệ thông tin", tenGoiKhac = new[] { "CNTT" } });
        var thpt = await Create(c, "phuong-thuc", new { ma = "THPT", ten = "Xét điểm thi tốt nghiệp THPT", thangDiem = 30 });
        await Create(c, "diem-chuan", new { nganhLid = cntt, nam = 2026, phuongThucLid = thpt, diem = 21.5, nguonTen = "Thông báo điểm chuẩn 2026", nguonUrl = "https://x.test/dc" });
        if (publish)
        {
            var res = await c.PostAsJsonAsync("/api/admin/releases", new { ghiChu = "x" });
            Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        }
    }

    private async Task<List<ChatEvent>> Run(string message, KbScope scope = KbScope.Published, Guid? conversationId = null)
    {
        using var s = f.Services.CreateScope();
        var orch = s.ServiceProvider.GetRequiredService<ChatOrchestrator>();
        var events = new List<ChatEvent>();
        await foreach (var e in orch.RunAsync(conversationId, message, scope, "ua-test", default)) events.Add(e);
        return events;
    }

    private async Task<List<Message>> Messages(Guid conversationId)
    {
        using var s = f.Services.CreateScope();
        var db = s.ServiceProvider.GetRequiredService<AdvisorDbContext>();
        return await db.Set<Message>().AsNoTracking().Where(m => m.ConversationId == conversationId)
            .OrderBy(m => m.TaoLuc).ThenBy(m => m.VaiTro == "assistant").ToListAsync();
    }

    private async Task<Conversation> Conv(Guid id)
    {
        using var s = f.Services.CreateScope();
        return await s.ServiceProvider.GetRequiredService<AdvisorDbContext>().Set<Conversation>().AsNoTracking().SingleAsync(c => c.Id == id);
    }

    private static JsonObject Text(string t) => new() { ["text"] = t };
    private static GeminiChunk Chunk(params JsonObject[] parts) => new(parts, null);
    private static JsonObject Call(string name, JsonObject args, string? sig = null)
    {
        var p = new JsonObject { ["functionCall"] = new JsonObject { ["name"] = name, ["args"] = args } };
        if (sig is not null) p["thoughtSignature"] = sig;
        return p;
    }

    [Fact]
    public async Task Text_only_answer_streams_deltas_and_persists()
    {
        await SeedAsync();
        f.Gemini.Script.Enqueue([Chunk(Text("Xin ")), new GeminiChunk([Text("chào bạn")], "STOP")]);

        var events = await Run("Chào trợ lý");

        Assert.Collection(events,
            e => Assert.Equal(new Delta("Xin "), e),
            e => Assert.Equal(new Delta("chào bạn"), e),
            e => Assert.IsType<Done>(e));
        var done = (Done)events[^1];

        var req = Assert.Single(f.Gemini.Requests);
        Assert.Equal("gemini-3.5-flash", req.Model);
        Assert.Contains("Trường Đại học Thủ Dầu Một", req.SystemInstruction);
        Assert.Contains("2026", req.SystemInstruction);
        Assert.Equal(8, req.FunctionDeclarations.Count);
        var last = Assert.Single(req.Contents);
        Assert.Equal("user", (string)last["role"]!);
        Assert.Equal("Chào trợ lý", (string)last["parts"]![0]!["text"]!);

        var conv = await Conv(done.ConversationId);
        Assert.Equal("chat", conv.KenhDauTien);
        Assert.False(conv.LaHoiThu);
        Assert.NotNull(conv.ReleaseId);
        Assert.Equal("ua-test", conv.UserAgent);

        var msgs = await Messages(done.ConversationId);
        Assert.Equal(2, msgs.Count);
        Assert.Equal(("user", "Chào trợ lý"), (msgs[0].VaiTro, msgs[0].NoiDung));
        Assert.Equal(("assistant", "Xin chào bạn"), (msgs[1].VaiTro, msgs[1].NoiDung));
        Assert.Equal(done.MessageId, msgs[1].Id);
        Assert.False(msgs[1].CoLoi);
        Assert.Empty(msgs[1].ToolCalls!.AsArray());
        Assert.NotNull(msgs[1].LatencyMs!["first_token"]);
        Assert.True((long)msgs[1].LatencyMs!["total"]! >= (long)msgs[1].LatencyMs!["first_token"]!);
    }

    [Fact]
    public async Task Tool_round_trip_appends_model_parts_verbatim_and_function_response()
    {
        await SeedAsync();
        f.Gemini.Script.Enqueue([Chunk(Call("tra_diem_chuan", new JsonObject { ["nganh"] = "CNTT" }, "sig1"))]);
        f.Gemini.Script.Enqueue([Chunk(Text("Điểm chuẩn CNTT năm 2026 là 21,5 điểm."))]);

        var events = await Run("Điểm chuẩn CNTT?");

        Assert.Collection(events,
            e => Assert.Equal(new ToolStatus("tra_diem_chuan", "start"), e),
            e => Assert.Equal(new ToolStatus("tra_diem_chuan", "end"), e),
            e => Assert.Equal(new Delta("Điểm chuẩn CNTT năm 2026 là 21,5 điểm."), e),
            e => Assert.Equal(new[] { new NguonRef("Thông báo điểm chuẩn 2026", "https://x.test/dc") }, Assert.IsType<Citations>(e).Items),
            e => Assert.IsType<Done>(e));

        var reqs = f.Gemini.Requests;
        Assert.Equal(2, reqs.Count);
        var contents = reqs[1].Contents;
        Assert.Equal(3, contents.Count);
        Assert.Equal("model", (string)contents[1]["role"]!);
        var modelPart = contents[1]["parts"]![0]!;
        Assert.Equal("sig1", (string)modelPart["thoughtSignature"]!);
        Assert.Equal("tra_diem_chuan", (string)modelPart["functionCall"]!["name"]!);
        Assert.Equal("user", (string)contents[2]["role"]!);
        var fr = contents[2]["parts"]![0]!["functionResponse"]!;
        Assert.Equal("tra_diem_chuan", (string)fr["name"]!);
        Assert.Equal("ok", (string)fr["response"]!["ket_qua"]!);

        var msgs = await Messages(((Done)events[^1]).ConversationId);
        var tc = Assert.Single(msgs[1].ToolCalls!.AsArray())!;
        Assert.Equal("tra_diem_chuan", (string)tc["name"]!);
        Assert.Equal("CNTT", (string)tc["args"]!["nganh"]!);
        Assert.Equal("ok", (string)tc["ket_qua"]!);
    }

    [Fact]
    public async Task Max_rounds_exceeded_emits_fallback_text()
    {
        await SeedAsync();
        for (var i = 0; i < 6; i++)
            f.Gemini.Script.Enqueue([Chunk(Call("tra_diem_chuan", new JsonObject { ["nganh"] = "CNTT" }))]);

        var events = await Run("Điểm chuẩn?");

        Assert.Equal(5, f.Gemini.Requests.Count);
        Assert.Equal(10, events.OfType<ToolStatus>().Count());
        Assert.Equal(new Delta(Fallback), Assert.Single(events.OfType<Delta>()));
        Assert.IsType<Done>(events[^1]);
        var msgs = await Messages(((Done)events[^1]).ConversationId);
        Assert.Equal(Fallback, msgs[1].NoiDung);
        Assert.Equal(5, msgs[1].ToolCalls!.AsArray().Count);
    }

    [Fact]
    public async Task Empty_model_answer_emits_fallback_text()
    {
        await SeedAsync();
        f.Gemini.Script.Enqueue([new GeminiChunk([], "SAFETY")]);

        var events = await Run("?");

        Assert.Collection(events,
            e => Assert.Equal(new Delta(Fallback), e),
            e => Assert.IsType<Done>(e));
    }

    [Fact]
    public async Task Gemini_failure_mid_stream_emits_error_and_saves_flagged_message()
    {
        await SeedAsync();
        f.Gemini.Script.Enqueue([Chunk(Text("Điểm chuẩn ")), new ThrowChunk(new GeminiUnavailableException(503, new HttpRequestException("google secret detail")))]);

        var events = await Run("Điểm chuẩn CNTT?");

        Assert.Collection(events,
            e => Assert.Equal(new Delta("Điểm chuẩn "), e),
            e => Assert.Equal(new ChatError(Busy), e),
            e => Assert.IsType<Done>(e));
        var msgs = await Messages(((Done)events[^1]).ConversationId);
        Assert.True(msgs[1].CoLoi);
        Assert.StartsWith("Điểm chuẩn ", msgs[1].NoiDung);
        Assert.EndsWith(Busy, msgs[1].NoiDung);
        Assert.DoesNotContain("google", msgs[1].NoiDung);
    }

    [Fact]
    public async Task Gemini_not_configured_in_second_round_emits_error()
    {
        await SeedAsync();
        f.Gemini.Script.Enqueue([Chunk(Call("tra_diem_chuan", new JsonObject { ["nganh"] = "CNTT" }))]);
        f.Gemini.Script.Enqueue([new ThrowChunk(new GeminiNotConfiguredException())]);

        var events = await Run("Điểm chuẩn CNTT?");

        Assert.Collection(events,
            e => Assert.IsType<ToolStatus>(e),
            e => Assert.IsType<ToolStatus>(e),
            e => Assert.Equal(new ChatError(Busy), e),
            e => Assert.IsType<Done>(e));
        var msgs = await Messages(((Done)events[^1]).ConversationId);
        Assert.True(msgs[1].CoLoi);
        Assert.Equal(Busy, msgs[1].NoiDung);
        Assert.Single(msgs[1].ToolCalls!.AsArray());
    }

    [Fact]
    public async Task History_includes_last_10_turns()
    {
        await SeedAsync();
        Guid convId;
        using (var s = f.Services.CreateScope())
        {
            var store = s.ServiceProvider.GetRequiredService<ConversationStore>();
            var c = await store.GetOrCreateAsync(null, "chat", false, null, null, default);
            convId = c.Id;
            for (var i = 1; i <= 12; i++)
            {
                await store.AddAsync(c.Id, "user", "chat", $"hỏi {i}", null, null, false, default);
                await store.AddAsync(c.Id, "assistant", "chat", $"đáp {i}", null, null, false, default);
            }
        }
        f.Gemini.Script.Enqueue([Chunk(Text("ok"))]);

        var events = await Run("hỏi 13", conversationId: convId);

        Assert.Equal(convId, ((Done)events[^1]).ConversationId);
        var contents = Assert.Single(f.Gemini.Requests).Contents;
        Assert.Equal(21, contents.Count);
        Assert.Equal(("user", "hỏi 3"), ((string)contents[0]["role"]!, (string)contents[0]["parts"]![0]!["text"]!));
        Assert.Equal(("model", "đáp 12"), ((string)contents[19]["role"]!, (string)contents[19]["parts"]![0]!["text"]!));
        Assert.Equal(("user", "hỏi 13"), ((string)contents[20]["role"]!, (string)contents[20]["parts"]![0]!["text"]!));
        Assert.Equal(26, (await Messages(convId)).Count);
    }

    [Fact]
    public async Task User_message_pii_masked_in_db()
    {
        await SeedAsync();
        f.Gemini.Script.Enqueue([Chunk(Text("Mình đã ghi nhận."))]);

        var events = await Run("Em tên An, SĐT 0912345678, mail an@test.vn");

        var msgs = await Messages(((Done)events[^1]).ConversationId);
        Assert.Equal("Em tên An, SĐT [SĐT], mail [EMAIL]", msgs[0].NoiDung);
        // Mô hình vẫn nhận nguyên văn để có thể chuyển tiếp cán bộ khi người dùng đồng ý.
        Assert.Contains("0912345678", (string)f.Gemini.Requests[0].Contents[^1]["parts"]![0]!["text"]!);
    }

    [Fact]
    public async Task Draft_scope_conversation_marked_hoi_thu()
    {
        await SeedAsync(publish: false);
        f.Gemini.Script.Enqueue([Chunk(Call("tra_diem_chuan", new JsonObject { ["nganh"] = "CNTT" }))]);
        f.Gemini.Script.Enqueue([Chunk(Text("21,5 điểm"))]);

        var events = await Run("Điểm chuẩn CNTT?", KbScope.Draft);

        var conv = await Conv(((Done)events[^1]).ConversationId);
        Assert.True(conv.LaHoiThu);
        Assert.Null(conv.ReleaseId);
        Assert.Contains("2026", f.Gemini.Requests[0].SystemInstruction);
        // Tool đọc bản nháp: trả ok dù chưa có bản xuất bản.
        Assert.Equal("ok", (string)f.Gemini.Requests[1].Contents[^1]["parts"]![0]!["functionResponse"]!["response"]!["ket_qua"]!);
    }
}
