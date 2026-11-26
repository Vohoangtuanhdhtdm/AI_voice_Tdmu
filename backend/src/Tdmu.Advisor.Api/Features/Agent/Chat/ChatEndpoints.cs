using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Tdmu.Advisor.Api.Features.KnowledgeBase;
using Tdmu.Advisor.Api.Features.KnowledgeBase.Query;
using Tdmu.Advisor.Api.Infrastructure.Auth;
using Tdmu.Advisor.Api.Infrastructure.Http;

namespace Tdmu.Advisor.Api.Features.Agent.Chat;

public record ChatRequest(Guid? ConversationId, string? Message, string? Scope);

public static class ChatEndpoints
{
    public const string RateLimitPolicy = "chat";
    public const int MaxMessageLength = 1000;
    public const string NoReleaseText = "Hệ thống chưa có dữ liệu tuyển sinh được phát hành";

    public static void MapChatEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/chat", HandleAsync).AllowAnonymous().RequireRateLimiting(RateLimitPolicy);
    }

    private static async Task<IResult> HandleAsync(ChatRequest req, HttpContext http, ChatOrchestrator orchestrator)
    {
        var draft = string.Equals(req.Scope, "draft", StringComparison.OrdinalIgnoreCase);
        if (draft)
        {
            // Điểm cuối ẩn danh nên middleware không ép xác thực; tự xác thực token (nếu có) tại đây.
            var auth = await http.AuthenticateAsync(JwtBearerDefaults.AuthenticationScheme);
            if (!auth.Succeeded || !auth.Principal!.IsInRole(AdminSeeder.AdminRole))
                return ProblemResults.Problem(StatusCodes.Status403Forbidden, "Chỉ cán bộ quản trị được hỏi thử trên bản nháp");
        }
        var message = req.Message?.Trim() ?? "";
        if (message.Length is 0 or > MaxMessageLength)
            return ProblemResults.Problem(StatusCodes.Status400BadRequest, "Câu hỏi phải có từ 1 đến 1.000 ký tự");

        var scope = draft ? KbScope.Draft : KbScope.Published;
        var userAgent = http.Request.Headers.UserAgent.ToString();
        return new SseResult(orchestrator.RunAsync(req.ConversationId, message, scope, userAgent.Length == 0 ? null : userAgent, http.RequestAborted));
    }

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private sealed class SseResult(IAsyncEnumerable<ChatEvent> events) : IResult
    {
        public async Task ExecuteAsync(HttpContext http)
        {
            var res = http.Response;
            res.ContentType = "text/event-stream";
            res.Headers.CacheControl = "no-cache";
            res.Headers["X-Accel-Buffering"] = "no";
            var ct = http.RequestAborted;
            try
            {
                await using var e = events.GetAsyncEnumerator(ct);
                while (true)
                {
                    ChatEvent ev;
                    try
                    {
                        if (!await e.MoveNextAsync()) break;
                        ev = e.Current;
                    }
                    catch (NoActiveReleaseException)
                    {
                        // Chưa có hội thoại nào được tạo nên không có id để báo về.
                        await WriteAsync(res, "error", new { title = NoReleaseText }, ct);
                        await WriteAsync(res, "done", new { conversationId = (Guid?)null, messageId = (Guid?)null }, ct);
                        break;
                    }
                    await WriteAsync(res, ev, ct);
                }
            }
            catch (OperationCanceledException) { /* client ngắt kết nối */ }
        }

        private static Task WriteAsync(HttpResponse res, ChatEvent ev, CancellationToken ct) => ev switch
        {
            Delta d => WriteAsync(res, "delta", new { text = d.Text }, ct),
            ToolStatus t => WriteAsync(res, "tool_status", new { name = t.Name, state = t.State }, ct),
            Citations c => WriteAsync(res, "citations", new { items = c.Items }, ct),
            Done d => WriteAsync(res, "done", new { conversationId = (Guid?)d.ConversationId, messageId = (Guid?)d.MessageId }, ct),
            ChatError x => WriteAsync(res, "error", new { title = x.Title }, ct),
            _ => Task.CompletedTask,
        };

        private static async Task WriteAsync(HttpResponse res, string name, object data, CancellationToken ct)
        {
            await res.WriteAsync($"event: {name}\ndata: {JsonSerializer.Serialize(data, Json)}\n\n", ct);
            await res.Body.FlushAsync(ct);
        }
    }
}
