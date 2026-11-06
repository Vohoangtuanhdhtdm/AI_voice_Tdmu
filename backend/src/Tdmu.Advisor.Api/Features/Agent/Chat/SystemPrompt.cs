namespace Tdmu.Advisor.Api.Features.Agent.Chat;

/// <summary>System prompt có phiên bản (spec 5.3), đọc từ <c>Prompts/system.v1.md</c> cạnh assembly một lần rồi cache.</summary>
public static class SystemPrompt
{
    public const string Truong = "Trường Đại học Thủ Dầu Một";

    private static readonly Lazy<string> Template = new(() =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Features", "Agent", "Chat", "Prompts", "system.v1.md")));

    /// <param name="kenh">"chat" hoặc "live".</param>
    /// <param name="nam">Năm mùa tuyển sinh hiện tại của scope; null nếu chưa xác định.</param>
    public static string Build(string kenh, int? nam) => Template.Value
        .Replace("{{TRUONG}}", Truong)
        .Replace("{{NAM}}", nam?.ToString() ?? "chưa xác định")
        .Replace("{{KENH}}", kenh == "live"
            ? "giọng nói (live). Trả lời ngắn gọn 2–4 câu, câu văn dễ nghe, không dùng bảng hay ký hiệu markdown."
            : "chat. Trả lời đầy đủ, rõ ràng; có thể dùng gạch đầu dòng.");
}
