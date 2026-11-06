using Tdmu.Advisor.Api.Infrastructure;
using Tdmu.Advisor.Api.Infrastructure.Gemini;

namespace Tdmu.Advisor.Api.Features.Ingestion;

public class IngestionWorker(IServiceScopeFactory scopes, IConfiguration config, ILogger<IngestionWorker> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var delay = TimeSpan.FromSeconds(Math.Max(1, config.GetValue<int?>("Ingestion:PollSeconds") ?? 5));
        try
        {
            using var scope = scopes.CreateScope();
            await scope.ServiceProvider.GetRequiredService<IngestionService>().ResetStuckDocumentsAsync(stoppingToken);
        }
        catch (OperationCanceledException) { return; }
        catch (Exception ex) { log.LogError(ex, "Không đặt lại được tài liệu kẹt khi khởi động"); }

        var notConfiguredLogged = false;
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopes.CreateScope();
                var svc = scope.ServiceProvider.GetRequiredService<IngestionService>();
                await svc.ProcessPendingDocumentsAsync(stoppingToken);
                await svc.EmbedPendingFaqsAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (GeminiNotConfiguredException)
            {
                if (!notConfiguredLogged)
                    log.LogWarning("Chưa cấu hình Gemini API key: bỏ qua việc tạo embedding cho FAQ (chỉ ghi log một lần)");
                notConfiguredLogged = true;
            }
            catch (Exception ex)
            {
                log.LogError(ex, "Vòng xử lý nền thất bại; sẽ thử lại sau {Seconds}s", delay.TotalSeconds);
            }
            try { await Task.Delay(delay, stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }
}
