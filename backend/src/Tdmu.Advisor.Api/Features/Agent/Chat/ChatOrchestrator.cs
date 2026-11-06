using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Options;
using Tdmu.Advisor.Api.Features.Agent.Tools;
using Tdmu.Advisor.Api.Features.Conversations;
using Tdmu.Advisor.Api.Features.KnowledgeBase;
using Tdmu.Advisor.Api.Features.KnowledgeBase.Query;
using Tdmu.Advisor.Api.Infrastructure.Gemini;

namespace Tdmu.Advisor.Api.Features.Agent.Chat;

/// <summary>
/// Vòng lặp chat (spec 5.4): stream Gemini, chạy tool, gom nguồn, lưu hội thoại.
/// Không bao giờ để câu trả lời trống; lỗi Gemini chỉ được ghi log, người dùng chỉ thấy câu mặc định.
/// </summary>
public class ChatOrchestrator(
    IGeminiClient gemini, ToolRegistry tools, ConversationStore store, IKnowledgeQuery kb,
    IOptions<AgentOptions> options, ILogger<ChatOrchestrator> logger)
{
    public const string Kenh = "chat";
    public const int HistoryTurns = 10;
    public const string FallbackText = "Xin lỗi, mình chưa tìm được câu trả lời phù hợp. Bạn có muốn để lại thông tin để cán bộ tuyển sinh liên hệ không?";
    public const string BusyText = "Hệ thống đang bận, bạn vui lòng thử lại sau ít phút hoặc để lại thông tin để cán bộ tuyển sinh liên hệ.";

    /// <exception cref="NoActiveReleaseException">Scope Published mà chưa có bản xuất bản (ném ở lần lặp đầu).</exception>
    public async IAsyncEnumerable<ChatEvent> RunAsync(Guid? conversationId, string message, KbScope scope, string? userAgent,
        [EnumeratorCancellation] CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        var releaseId = await kb.ResolveReleaseAsync(scope, ct);
        var nam = await kb.CurrentYearAsync(scope, ct);
        var conv = await store.GetOrCreateAsync(conversationId, Kenh, scope == KbScope.Draft, releaseId, userAgent, ct);

        var contents = (await store.RecentAsync(conv.Id, HistoryTurns, ct))
            .Where(m => !string.IsNullOrWhiteSpace(m.NoiDung))
            .Select(m => TextContent(m.VaiTro == "assistant" ? "model" : "user", m.NoiDung))
            .ToList();
        // Lưu (và commit) tin nhắn người dùng trước khi chạy tool: chuyen_tiep_can_bo ghi ticket có FK tới hội thoại.
        await store.AddAsync(conv.Id, "user", Kenh, message, null, null, false, ct);
        // Mô hình nhận nguyên văn (chưa che) để có thể chuyển tiếp liên hệ khi người dùng đồng ý.
        contents.Add(TextContent("user", message));

        var systemInstruction = SystemPrompt.Build(Kenh, nam);
        var declarations = tools.Declarations();
        var maxRounds = Math.Max(1, options.Value.MaxToolRounds);
        var toolContext = new ToolContext(scope, conv.Id);

        var text = new StringBuilder();
        var toolCalls = new JsonArray();
        var sources = new List<NguonRef>();
        long? firstToken = null;
        var failed = false;
        var saved = false;

        // Chữ của vòng sau nối liền chữ vòng trước thì chèn xuống dòng để hai câu không dính nhau.
        string Separate(string s) =>
            text.Length > 0 && !char.IsWhiteSpace(text[^1]) && !char.IsWhiteSpace(s[0]) ? "\n" + s : s;
        JsonObject Latency() => new() { ["first_token"] = firstToken, ["total"] = sw.ElapsedMilliseconds };

        try
        {
            var needFallback = false;
            for (var round = 0; round < maxRounds; round++)
            {
                var modelParts = new List<JsonObject>();
                var calls = new List<JsonObject>();
                var roundHasText = false;
                var req = new GeminiRequest(systemInstruction, contents.ToList(), declarations, options.Value.ChatModel);

                IAsyncEnumerator<GeminiChunk>? stream = null;
                try
                {
                    while (true)
                    {
                        GeminiChunk chunk;
                        try
                        {
                            stream ??= gemini.StreamGenerateAsync(req, ct).GetAsyncEnumerator(ct);
                            if (!await stream.MoveNextAsync()) break;
                            chunk = stream.Current;
                        }
                        catch (Exception ex) when (ex is GeminiUnavailableException or GeminiNotConfiguredException)
                        {
                            // ex.Message có thể chứa nội dung lỗi của Google: chỉ ghi log.
                            logger.LogWarning(ex, "Gemini lỗi ở vòng {Round} của hội thoại {ConversationId}", round + 1, conv.Id);
                            failed = true;
                            break;
                        }

                        foreach (var part in chunk.Parts)
                        {
                            modelParts.Add(part);
                            if (part["functionCall"] is JsonObject call)
                                calls.Add(call);
                            else if (part["text"] is JsonValue t && t.TryGetValue<string>(out var s) && s.Length > 0
                                     && !(part["thought"] is JsonValue th && th.TryGetValue<bool>(out var isThought) && isThought))
                            {
                                var piece = roundHasText ? s : Separate(s);
                                roundHasText = true;
                                firstToken ??= sw.ElapsedMilliseconds;
                                text.Append(piece);
                                yield return new Delta(piece);
                            }
                        }
                    }
                }
                finally
                {
                    if (stream is not null) await stream.DisposeAsync();
                }

                if (failed || calls.Count == 0) break;
                // Vòng cuối: không còn lượt nào để mô hình đọc kết quả, nên không chạy tool (và không gom nguồn của chúng).
                if (round == maxRounds - 1) { needFallback = true; break; }

                // Giữ nguyên văn lượt của model (kể cả thoughtSignature) như Gemini 3 yêu cầu.
                contents.Add(new JsonObject
                {
                    ["role"] = "model",
                    ["parts"] = new JsonArray(modelParts.Select(p => (JsonNode)p.DeepClone()).ToArray()),
                });
                var responses = new JsonArray();
                foreach (var call in calls)
                {
                    var name = (string?)call["name"] ?? "";
                    var args = call["args"] as JsonObject ?? new JsonObject();
                    yield return new ToolStatus(name, "start");
                    var result = await tools.InvokeAsync(name, (JsonObject)args.DeepClone(), toolContext, ct);
                    yield return new ToolStatus(name, "end");

                    // ConversationStore che SĐT/email trong tool_calls trước khi lưu.
                    toolCalls.Add(new JsonObject { ["name"] = name, ["args"] = args.DeepClone(), ["ket_qua"] = result.KetQua });
                    foreach (var n in result.Nguon)
                        if (!sources.Contains(n)) sources.Add(n);
                    var fr = new JsonObject { ["name"] = name, ["response"] = result.ToJson() };
                    if (call["id"] is { } id) fr["id"] = id.DeepClone();
                    responses.Add(new JsonObject { ["functionResponse"] = fr });
                }
                contents.Add(new JsonObject { ["role"] = "user", ["parts"] = responses });
            }

            string content;
            if (failed)
            {
                yield return new ChatError(BusyText);
                content = text.Length > 0 ? text + "\n\n" + BusyText : BusyText;
            }
            else
            {
                if (text.Length == 0 || needFallback)
                {
                    var piece = Separate(FallbackText);
                    firstToken ??= sw.ElapsedMilliseconds;
                    text.Append(piece);
                    yield return new Delta(piece);
                }
                if (sources.Count > 0) yield return new Citations(sources);
                content = text.ToString();
            }

            // Không hủy được: nếu bị hủy giữa SaveChanges, khối finally sẽ lưu trùng.
            var reply = await store.AddAsync(conv.Id, "assistant", Kenh, content, toolCalls, Latency(), failed, CancellationToken.None);
            saved = true;
            yield return new Done(conv.Id, reply.Id);
        }
        finally
        {
            // Bị hủy (client ngắt kết nối), bên gọi dừng lặp sớm hoặc lỗi bất ngờ: vẫn lưu phần đã có, đánh dấu lỗi.
            if (!saved)
            {
                try
                {
                    await store.AddAsync(conv.Id, "assistant", Kenh, text.ToString(), toolCalls, Latency(), true, CancellationToken.None);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Không lưu được tin nhắn dở dang của hội thoại {ConversationId}", conv.Id);
                }
            }
        }
    }

    private static JsonObject TextContent(string role, string text) => new()
    {
        ["role"] = role,
        ["parts"] = new JsonArray(new JsonObject { ["text"] = text }),
    };
}
