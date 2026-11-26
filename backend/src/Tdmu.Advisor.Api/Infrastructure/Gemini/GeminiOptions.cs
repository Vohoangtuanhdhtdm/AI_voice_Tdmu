namespace Tdmu.Advisor.Api.Infrastructure.Gemini;

public class GeminiOptions
{
    public const string Section = "Gemini";
    public string ApiKey { get; set; } = "";
    /// <summary>Thời gian tối đa chờ dữ liệu mới khi đang đọc luồng stream; quá hạn thì coi Gemini không khả dụng.</summary>
    public int StreamIdleTimeoutSeconds { get; set; } = 30;
    /// <summary>Hạn chờ mỗi request (với stream: tới lúc nhận header). HttpClient.Timeout đặt vô hạn, hạn này thay thế.</summary>
    public int RequestTimeoutSeconds { get; set; } = 60;
    /// <summary>Hạn chờ riêng cho chuyển PDF dạng ảnh sang Markdown (tài liệu dài mất vài phút).</summary>
    public int PdfTimeoutSeconds { get; set; } = 180;
}

public class EmbeddingOptions
{
    public const string Section = "Embedding";
    public string Model { get; set; } = "gemini-embedding-001";
    public int Dimensions { get; set; } = 768;
}

public class AgentOptions
{
    public const string Section = "Agent";
    public string ChatModel { get; set; } = "gemini-3.5-flash";
    /// <summary>Mô hình dự phòng khi <see cref="ChatModel"/> quá tải (429/503/lỗi mạng) hai lần liền trước khi có chữ; rỗng = không dự phòng.</summary>
    public string FallbackChatModel { get; set; } = "gemini-3-flash-preview";
    public int MaxToolRounds { get; set; } = 5;
}

public class RagOptions
{
    public const string Section = "Rag";
    public double FaqThreshold { get; set; } = 0.85;
    public int TopK { get; set; } = 5;
}
