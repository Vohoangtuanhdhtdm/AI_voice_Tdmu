namespace Tdmu.Advisor.Api.Infrastructure.Gemini;

/// <summary>Thử lại một lần (sau khoảng chờ) khi Gemini lỗi tạm thời (<see cref="GeminiUnavailableException.IsTransient"/>).</summary>
public static class GeminiRetry
{
    public static async Task<T> OnceAsync<T>(Func<Task<T>> op, TimeSpan delay, ILogger log, string what, CancellationToken ct)
    {
        try { return await op(); }
        catch (GeminiUnavailableException ex) when (ex.IsTransient)
        {
            log.LogWarning("Gemini lỗi tạm thời (status {Status}) khi {What}; thử lại sau {Delay} ms", ex.Status?.ToString() ?? "không có", what, delay.TotalMilliseconds);
            await Task.Delay(delay, ct);
            return await op();
        }
    }
}
