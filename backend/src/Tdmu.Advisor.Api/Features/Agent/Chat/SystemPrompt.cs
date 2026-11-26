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
        .Replace("{{CACH_VIET_SO}}", kenh == "live"
            ? "Đọc số và mã tổ hợp theo `cach_doc`."
            // Kênh chat hiển thị chữ: số đọc bằng chữ (cach_doc) khó đọc và dễ sai khi chép lại.
            : "Kênh chat: viết số bằng chữ số theo định dạng Việt Nam, lấy từ `du_lieu` (ví dụ \"21,5 điểm\", \"1.200.000 đồng\", ngày dd/MM/yyyy); không viết số bằng chữ. Trường `cach_doc` chỉ dành cho kênh giọng nói.")
        .Replace("{{KENH}}", kenh == "live"
            ? "giọng nói (live). Trả lời ngắn gọn 2–4 câu, câu văn dễ nghe, không dùng bảng hay ký hiệu markdown."
            : "chat. Trả lời đầy đủ, rõ ràng; có thể dùng gạch đầu dòng.");
}
