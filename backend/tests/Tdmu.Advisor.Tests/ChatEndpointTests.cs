using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Tdmu.Advisor.Api.Features.Agent.Tools;
using Tdmu.Advisor.Api.Features.Conversations;
using Tdmu.Advisor.Api.Infrastructure;
using Tdmu.Advisor.Api.Infrastructure.Gemini;
using Tdmu.Advisor.Tests.Infrastructure;

namespace Tdmu.Advisor.Tests;

[Collection("api")]
public class ChatEndpointTests(ApiFactory f)
{
    private const string Busy = "Hệ thống đang bận, bạn vui lòng thử lại sau ít phút hoặc để lại thông tin để cán bộ tuyển sinh liên hệ.";

    /// <summary>Đọc luồng SSE: mỗi khối `event:`/`data:` cách nhau một dòng trống.</summary>
    public static async Task<List<(string Event, JsonElement Data)>> ReadSseAsync(HttpResponseMessage res)
    {
        var body = await res.Content.ReadAsStringAsync();
        var list = new List<(string, JsonElement)>();
        foreach (var block in body.Replace("\r\n", "\n").Split("\n\n", StringSplitOptions.RemoveEmptyEntries))
        {
            string? ev = null, data = null;
            foreach (var line in block.Split('\n'))
            {
                if (line.StartsWith("event: ")) ev = line[7..];
                else if (line.StartsWith("data: ")) data = line[6..];
            }
            if (ev != null && data != null) list.Add((ev, JsonDocument.Parse(data).RootElement.Clone()));
        }
        return list;
    }

    private static async Task<Guid> Create(HttpClient c, string slug, object body)
    {
        var res = await c.PostAsJsonAsync($"/api/admin/kb/{slug}", body);
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        return (await res.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("logicalId").GetGuid();
    }

    private async Task<HttpClient> SeedAsync(bool publish = true)
    {
        await f.ResetDatabaseAsync();
        var c = await AuthHelper.AdminClientAsync(f);
        await Create(c, "mua-tuyen-sinh", new { nam = 2026, laHienTai = true });
        var cntt = await Create(c, "nganh", new { maNganh = "7480201", ten = "Công nghệ thông tin", tenGoiKhac = new[] { "CNTT" } });
        var thpt = await Create(c, "phuong-thuc", new { ma = "THPT", ten = "Xét điểm thi tốt nghiệp THPT", thangDiem = 30 });
        await Create(c, "diem-chuan", new { nganhLid = cntt, nam = 2026, phuongThucLid = thpt, diem = 21.5, nguonTen = "Thông báo điểm chuẩn 2026", nguonUrl = "https://x.test/dc" });
        if (publish)
            Assert.Equal(HttpStatusCode.Created, (await c.PostAsJsonAsync("/api/admin/releases", new { ghiChu = "x" })).StatusCode);
        return c;
    }

    private static JsonObject Text(string t) => new() { ["text"] = t };
    private static GeminiChunk Chunk(params JsonObject[] parts) => new(parts, null);
    private static JsonObject Call(string name, JsonObject args) =>
        new() { ["functionCall"] = new JsonObject { ["name"] = name, ["args"] = args } };

    [Fact]
    public async Task Streams_events_in_order_for_published_scope()
    {
        await SeedAsync();
        f.Gemini.Script.Enqueue([Chunk(Call("tra_diem_chuan", new JsonObject { ["nganh"] = "CNTT" }))]);
        f.Gemini.Script.Enqueue([Chunk(Text("Điểm chuẩn là 21.5."))]);

        var res = await f.CreateClient().PostAsJsonAsync("/api/chat", new { message = "Điểm chuẩn CNTT?" });

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        Assert.Equal("text/event-stream", res.Content.Headers.ContentType!.MediaType);
        Assert.Contains("no-cache", res.Headers.CacheControl!.ToString());
        Assert.Equal("no", res.Headers.GetValues("X-Accel-Buffering").Single());
        var events = await ReadSseAsync(res);
        Assert.Equal(["tool_status", "tool_status", "delta", "citations", "done"], events.Select(e => e.Event));
        Assert.Equal("tra_diem_chuan", events[0].Data.GetProperty("name").GetString());
        Assert.Equal("start", events[0].Data.GetProperty("state").GetString());
        Assert.Equal("end", events[1].Data.GetProperty("state").GetString());
        Assert.Equal("Điểm chuẩn là 21.5.", events[2].Data.GetProperty("text").GetString());
        Assert.Equal("Thông báo điểm chuẩn 2026", events[3].Data.GetProperty("items")[0].GetProperty("ten").GetString());
        Assert.Equal("https://x.test/dc", events[3].Data.GetProperty("items")[0].GetProperty("url").GetString());
        Assert.NotEqual(Guid.Empty, events[4].Data.GetProperty("conversationId").GetGuid());
        Assert.NotEqual(Guid.Empty, events[4].Data.GetProperty("messageId").GetGuid());
    }

    [Fact]
    public async Task Draft_scope_without_admin_returns_403()
    {
        await SeedAsync();

        var anon = await f.CreateClient().PostAsJsonAsync("/api/chat", new { message = "hi", scope = "draft" });

        Assert.Equal(HttpStatusCode.Forbidden, anon.StatusCode);
        Assert.Equal("Chỉ cán bộ quản trị được hỏi thử trên bản nháp", (await anon.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("title").GetString());
    }

    [Fact]
    public async Task Draft_scope_with_admin_streams()
    {
        var admin = await SeedAsync(publish: false);
        f.Gemini.Script.Enqueue([Chunk(Text("Bản nháp ok"))]);

        var res = await admin.PostAsJsonAsync("/api/chat", new { message = "hi", scope = "draft" });

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var events = await ReadSseAsync(res);
        Assert.Equal(["delta", "done"], events.Select(e => e.Event));
        var convId = events[^1].Data.GetProperty("conversationId").GetGuid();
        using var s = f.Services.CreateScope();
        var conv = await s.ServiceProvider.GetRequiredService<AdvisorDbContext>().Set<Conversation>().AsNoTracking().SingleAsync(c => c.Id == convId);
        Assert.True(conv.LaHoiThu);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Empty_message_returns_400(string message)
    {
        await SeedAsync();
        var res = await f.CreateClient().PostAsJsonAsync("/api/chat", new { message });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task Message_over_1000_chars_returns_400()
    {
        await SeedAsync();

        var ok = f.Gemini.Script;
        ok.Enqueue([Chunk(Text("ok"))]);
        var exactly = await f.CreateClient().PostAsJsonAsync("/api/chat", new { message = new string('a', 1000) });
        var over = await f.CreateClient().PostAsJsonAsync("/api/chat", new { message = new string('a', 1001) });

        Assert.Equal(HttpStatusCode.OK, exactly.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, over.StatusCode);
        Assert.Equal("Câu hỏi phải có từ 1 đến 1.000 ký tự", (await over.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("title").GetString());
    }

    [Fact]
    public async Task No_active_release_streams_error_then_done()
    {
        await SeedAsync(publish: false);

        var res = await f.CreateClient().PostAsJsonAsync("/api/chat", new { message = "hi" });

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var events = await ReadSseAsync(res);
        Assert.Equal(["error", "done"], events.Select(e => e.Event));
        Assert.Equal("Hệ thống chưa có dữ liệu tuyển sinh được phát hành", events[0].Data.GetProperty("title").GetString());
        Assert.Equal(JsonValueKind.Null, events[1].Data.GetProperty("conversationId").ValueKind);
        Assert.Equal(JsonValueKind.Null, events[1].Data.GetProperty("messageId").ValueKind);
    }

    [Fact]
    public async Task Gemini_error_streams_error_event_not_500()
    {
        await SeedAsync();
        f.Gemini.Script.Enqueue([Chunk(Text("Điểm chuẩn ")), new ThrowChunk(new GeminiUnavailableException(503, new HttpRequestException("secret")))]);

        var res = await f.CreateClient().PostAsJsonAsync("/api/chat", new { message = "Điểm chuẩn?" });

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var events = await ReadSseAsync(res);
        Assert.Equal(["delta", "error", "done"], events.Select(e => e.Event));
        Assert.Equal(Busy, events[1].Data.GetProperty("title").GetString());
    }

    [Fact]
    public async Task Conversation_id_continues_history()
    {
        await SeedAsync();
        f.Gemini.Script.Enqueue([Chunk(Text("Câu trả lời một"))]);
        var c = f.CreateClient();
        var first = await ReadSseAsync(await c.PostAsJsonAsync("/api/chat", new { message = "Câu hỏi một" }));
        var convId = first[^1].Data.GetProperty("conversationId").GetGuid();

        f.Gemini.Script.Enqueue([Chunk(Text("Câu trả lời hai"))]);
        var second = await ReadSseAsync(await c.PostAsJsonAsync("/api/chat", new { conversationId = convId, message = "Câu hỏi hai" }));

        Assert.Equal(convId, second[^1].Data.GetProperty("conversationId").GetGuid());
        var texts = f.Gemini.Requests[^1].Contents.SelectMany(x => x["parts"]!.AsArray()).Select(p => (string?)p!["text"]).ToList();
        Assert.Contains("Câu hỏi một", texts);
        Assert.Contains("Câu trả lời một", texts);
        Assert.Contains("Câu hỏi hai", texts);
    }

    [Fact]
    public async Task Conversation_id_from_other_scope_starts_new_conversation()
    {
        var admin = await SeedAsync();
        f.Gemini.Script.Enqueue([Chunk(Text("nháp"))]);
        var draft = await ReadSseAsync(await admin.PostAsJsonAsync("/api/chat", new { message = "hi", scope = "draft" }));
        var draftId = draft[^1].Data.GetProperty("conversationId").GetGuid();

        f.Gemini.Script.Enqueue([Chunk(Text("công khai"))]);
        var pub = await ReadSseAsync(await f.CreateClient().PostAsJsonAsync("/api/chat", new { conversationId = draftId, message = "hi" }));

        Assert.NotEqual(draftId, pub[^1].Data.GetProperty("conversationId").GetGuid());
    }
}
