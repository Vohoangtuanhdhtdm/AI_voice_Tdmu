namespace Tdmu.Advisor.Api.Infrastructure.Gemini;

public class GeminiNotConfiguredException()
    : Exception("Chưa cấu hình Gemini:ApiKey.");

/// <summary>429, 5xx, lỗi mạng hoặc timeout. <see cref="Status"/> là null khi không có phản hồi HTTP.</summary>
public class GeminiUnavailableException(int? status, Exception? inner = null)
    : Exception($"Gemini không khả dụng (status {status?.ToString() ?? "không có"}).", inner)
{
    public int? Status { get; } = status;
}
