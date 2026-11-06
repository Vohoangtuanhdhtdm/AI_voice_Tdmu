using System.Diagnostics;
using System.Text.Json.Nodes;
using Tdmu.Advisor.Api.Features.KnowledgeBase.Query;

namespace Tdmu.Advisor.Api.Features.Agent.Tools;

/// <summary>Gom các <see cref="IAgentTool"/> đăng ký trong DI (scoped). Mỗi lần gọi bị giới hạn 3 giây.</summary>
public class ToolRegistry(IEnumerable<IAgentTool> tools, ILogger<ToolRegistry> logger)
{
    public static readonly TimeSpan Timeout = TimeSpan.FromSeconds(3);
    /// <summary>Thời gian chờ thêm sau khi hủy để tool kịp dừng (truy vấn DB đang chạy) trước khi bỏ mặc nó.</summary>
    private static readonly TimeSpan CancelGrace = TimeSpan.FromMilliseconds(250);

    // Đăng ký sau ghi đè đăng ký trước cùng tên.
    private readonly Dictionary<string, IAgentTool> _tools = tools.GroupBy(t => t.Name).ToDictionary(g => g.Key, g => g.Last());

    public JsonArray Declarations() => new(_tools.Values.Select(t => (JsonNode)t.Declaration.DeepClone()).ToArray());

    public async Task<ToolResult> InvokeAsync(string name, JsonObject args, ToolContext ctx, CancellationToken ct)
    {
        if (!_tools.TryGetValue(name, out var tool))
        {
            logger.LogWarning("Mô hình gọi tool không tồn tại: {Tool}", name);
            return ToolResult.Error();
        }
        var sw = Stopwatch.StartNew();
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(Timeout);
        try
        {
            // WaitAsync bảo đảm giới hạn thời gian kể cả khi tool không tôn trọng token.
            return await tool.InvokeAsync(args, ctx, cts.Token).WaitAsync(Timeout + CancelGrace, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw; // người gọi hủy (ví dụ client ngắt kết nối): không nuốt.
        }
        catch (Exception ex) when (ex is OperationCanceledException or TimeoutException)
        {
            logger.LogWarning("Tool {Tool} quá thời gian {Timeout}s (đã chạy {Elapsed} ms)", name, Timeout.TotalSeconds, sw.ElapsedMilliseconds);
            return ToolResult.Error();
        }
        catch (NoActiveReleaseException)
        {
            return ToolResult.NotFound("dữ liệu tuyển sinh đã xuất bản");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Tool {Tool} lỗi", name);
            return ToolResult.Error();
        }
    }
}
