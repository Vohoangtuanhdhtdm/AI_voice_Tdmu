namespace Tdmu.Advisor.Api.Infrastructure.Gemini;

public class GeminiNotConfiguredException(string message = "Chưa cấu hình Gemini:ApiKey.")
    : Exception(message);

/// <summary>429, 5xx, lỗi mạng, timeout hoặc phản hồi lỗi khác. <see cref="Status"/> là null khi không có phản hồi HTTP.
/// Với lỗi 4xx khác, <see cref="Exception.InnerException"/> chứa đoạn đầu (tối đa 500 ký tự) của nội dung phản hồi.</summary>
public class GeminiUnavailableException(int? status, Exception? inner = null)
    : Exception($"Gemini không khả dụng (status {status?.ToString() ?? "không có"}){(inner is HttpRequestException { Message.Length: > 0 } ? ": " + inner.Message : "")}.", inner)
{
    public int? Status { get; } = status;
}
