using System.Collections.Concurrent;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Tdmu.Advisor.Tests.Infrastructure;

/// <summary>
/// ApiFactory dùng GeminiRestClient thật, key lấy từ biến môi trường <c>GEMINI_API_KEY</c>.
/// Chỉ dùng cho smoke test <c>GeminiLive</c>; không thuộc collection "api" dùng chung.
/// <c>GEMINI_CHAT_MODEL</c> (tùy chọn) ghi đè <c>Agent:ChatModel</c> khi model mặc định không dùng được với key.
/// </summary>
public sealed class RealGeminiApiFactory : ApiFactory
{
    public const string ApiKeyVariable = "GEMINI_API_KEY";
    public const string ChatModelVariable = "GEMINI_CHAT_MODEL";

    public static string? ApiKey => Environment.GetEnvironmentVariable(ApiKeyVariable) is { Length: > 0 } k ? k : null;
    public static string? ChatModelOverride => Environment.GetEnvironmentVariable(ChatModelVariable) is { Length: > 0 } m ? m : null;

    public override bool UseRealGemini => true;

    /// <summary>Log mức Warning trở lên của app (lỗi Gemini chỉ được ghi log), để test in ra khi thất bại.</summary>
    public ConcurrentQueue<string> Warnings { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.UseSetting("Gemini:ApiKey", ApiKey ?? "");
        if (ChatModelOverride is { } model) builder.UseSetting("Agent:ChatModel", model);
        builder.ConfigureTestServices(s => s.AddSingleton<ILoggerProvider>(new CapturingLoggerProvider(Warnings)));
    }

    private sealed class CapturingLoggerProvider(ConcurrentQueue<string> sink) : ILoggerProvider
    {
        public ILogger CreateLogger(string categoryName) => new Logger(categoryName, sink);
        public void Dispose() { }

        private sealed class Logger(string category, ConcurrentQueue<string> sink) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
            public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Warning;
            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            {
                if (!IsEnabled(logLevel)) return;
                sink.Enqueue($"[{logLevel}] {category}: {formatter(state, exception)}{(exception is null ? "" : " | " + exception.GetType().Name + ": " + exception.Message)}");
            }
        }
    }
}

/// <summary>Fact chỉ chạy khi có <c>GEMINI_API_KEY</c>; thiếu thì xUnit báo Skipped.</summary>
public sealed class GeminiLiveFactAttribute : FactAttribute
{
    public GeminiLiveFactAttribute()
    {
        if (RealGeminiApiFactory.ApiKey is null)
            Skip = $"Bỏ qua: chưa đặt biến môi trường {RealGeminiApiFactory.ApiKeyVariable} (smoke test gọi Gemini thật).";
    }
}
