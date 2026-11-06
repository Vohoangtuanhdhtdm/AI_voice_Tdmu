using System.Diagnostics;
using System.Text.Json.Nodes;
using Tdmu.Advisor.Api.Features.KnowledgeBase.Query;

namespace Tdmu.Advisor.Api.Features.Agent.Tools;

/// <summary>
/// Gom các <see cref="IAgentTool"/> đăng ký trong DI (scoped). Mỗi lần gọi chạy tool trong một DI scope riêng
/// (DbContext riêng), giới hạn 3 giây. Tool hết giờ có thể còn chạy tiếp, nên nó không được dùng chung DbContext
/// với request; scope của nó chỉ được giải phóng khi chính tool kết thúc.
/// </summary>
public class ToolRegistry(IServiceScopeFactory scopes, ILogger<ToolRegistry> logger)
{
    public static readonly TimeSpan Timeout = TimeSpan.FromSeconds(3);
    /// <summary>Thời gian chờ thêm sau khi hủy để tool kịp dừng trước khi bỏ mặc nó.</summary>
    private static readonly TimeSpan CancelGrace = TimeSpan.FromMilliseconds(250);

    private JsonArray? _declarations;

    // Đăng ký sau ghi đè đăng ký trước cùng tên.
    private static IAgentTool? Find(IServiceProvider sp, string name) =>
        sp.GetServices<IAgentTool>().LastOrDefault(t => t.Name == name);

    public JsonArray Declarations()
    {
        if (_declarations is null)
        {
            using var scope = scopes.CreateScope();
            _declarations = new JsonArray(scope.ServiceProvider.GetServices<IAgentTool>()
                .GroupBy(t => t.Name).Select(g => (JsonNode)g.Last().Declaration.DeepClone()).ToArray());
        }
        return (JsonArray)_declarations.DeepClone();
    }

    public async Task<ToolResult> InvokeAsync(string name, JsonObject args, ToolContext ctx, CancellationToken ct)
    {
        var scope = scopes.CreateAsyncScope();
        var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        Task<ToolResult> task;
        try
        {
            var tool = Find(scope.ServiceProvider, name);
            if (tool is null)
            {
                logger.LogWarning("Mô hình gọi tool không tồn tại: {Tool}", name);
                task = Task.FromResult(ToolResult.Error());
            }
            else
            {
                cts.CancelAfter(Timeout);
                task = tool.InvokeAsync(args, ctx, cts.Token);
            }
        }
        catch (Exception ex)
        {
            task = Task.FromException<ToolResult>(ex);
        }
        // Giải phóng scope khi tool thực sự xong, không phải khi registry thôi chờ.
        _ = task.ContinueWith(async t => { _ = t.Exception; cts.Dispose(); await scope.DisposeAsync(); },
            CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default).Unwrap();

        var sw = Stopwatch.StartNew();
        try
        {
            // WaitAsync bảo đảm giới hạn thời gian kể cả khi tool không tôn trọng token.
            return await task.WaitAsync(Timeout + CancelGrace, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw; // người gọi hủy (ví dụ client ngắt kết nối): không nuốt.
        }
        catch (Exception ex) when (ex is OperationCanceledException or TimeoutException)
        {
            logger.LogWarning("Tool {Tool} quá thời gian {Timeout}s (đã chờ {Elapsed} ms)", name, Timeout.TotalSeconds, sw.ElapsedMilliseconds);
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
