using System.Text.Json.Nodes;
using Tdmu.Advisor.Api.Features.Conversations;

namespace Tdmu.Advisor.Api.Features.Agent.Tools;

/// <summary>Tool duy nhất có ghi: tạo ticket để cán bộ tuyển sinh liên hệ lại.</summary>
public class ChuyenTiepCanBoTool(ConversationStore store) : IAgentTool
{
    public string Name => "chuyen_tiep_can_bo";

    /// <summary>Số ticket tối đa của một hội thoại (chặn mô hình hoặc người dùng tạo ticket hàng loạt).</summary>
    public const int MaxTicketsPerConversation = 3;

    public JsonObject Declaration { get; } = ToolDeclaration.Create("chuyen_tiep_can_bo",
        "Chuyển yêu cầu cho cán bộ tuyển sinh liên hệ lại. Chỉ gọi khi người dùng đã đồng ý và đã cho họ tên cùng số điện thoại hoặc email.",
        ("ly_do", "string", "Tóm tắt điều người dùng cần được tư vấn.", true),
        ("ho_ten", "string", "Họ tên người dùng.", false),
        ("lien_he", "string", "Số điện thoại hoặc email người dùng cung cấp.", false));

    /// <summary>Số điện thoại (ít nhất 9 chữ số) hoặc email (có '@').</summary>
    private static bool LooksLikeContact(string s) => s.Contains('@') || s.Count(char.IsAsciiDigit) >= 9;

    public async Task<ToolResult> InvokeAsync(JsonObject args, ToolContext ctx, CancellationToken ct)
    {
        var hoTen = ToolArgs.Str(args, "ho_ten");
        var lienHe = ToolArgs.Str(args, "lien_he");
        if (hoTen is null || lienHe is null || !LooksLikeContact(lienHe))
            return ToolResult.AskBack(null, "Hãy hỏi họ tên và số điện thoại hoặc email của người dùng trước.");
        var lyDo = ToolArgs.Str(args, "ly_do") ?? "Người dùng cần cán bộ tuyển sinh tư vấn";
        var conversationId = ctx.ConversationId == Guid.Empty ? (Guid?)null : ctx.ConversationId;
        if (conversationId is { } cid && await store.CountTicketsAsync(cid, ct) >= MaxTicketsPerConversation)
            return ToolResult.Ok(new JsonObject
            {
                ["da_co_yeu_cau"] = true,
                ["thong_bao"] = "Cán bộ tuyển sinh sẽ liên hệ lại trong giờ hành chính theo yêu cầu đã gửi.",
            }, huongDan: "Cuộc trò chuyện này đã có yêu cầu liên hệ gửi cán bộ tuyển sinh; không tạo thêm yêu cầu. Hãy báo người dùng là đã có yêu cầu và cán bộ sẽ liên hệ lại.");
        var t = await store.CreateTicketAsync(conversationId, hoTen, lienHe, lyDo, ct);
        return ToolResult.Ok(new JsonObject
        {
            // Mã ngắn để đọc được bằng giọng nói; mã đầy đủ chỉ nằm trong ticket.
            ["ma_yeu_cau"] = t.Id.ToString("N")[..8].ToUpperInvariant(),
            ["thong_bao"] = "Cán bộ tuyển sinh sẽ liên hệ lại trong giờ hành chính.",
        });
    }
}
