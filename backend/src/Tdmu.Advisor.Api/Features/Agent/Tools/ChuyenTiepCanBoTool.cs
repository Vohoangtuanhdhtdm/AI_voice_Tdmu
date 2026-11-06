using System.Text.Json.Nodes;
using Tdmu.Advisor.Api.Features.Conversations;

namespace Tdmu.Advisor.Api.Features.Agent.Tools;

/// <summary>Tool duy nhất có ghi: tạo ticket để cán bộ tuyển sinh liên hệ lại.</summary>
public class ChuyenTiepCanBoTool(ConversationStore store) : IAgentTool
{
    public string Name => "chuyen_tiep_can_bo";

    public JsonObject Declaration { get; } = ToolDeclaration.Create("chuyen_tiep_can_bo",
        "Chuyển yêu cầu cho cán bộ tuyển sinh liên hệ lại. Chỉ gọi khi người dùng đã đồng ý và đã cho họ tên cùng số điện thoại hoặc email.",
        ("ly_do", "string", "Tóm tắt điều người dùng cần được tư vấn.", true),
        ("ho_ten", "string", "Họ tên người dùng.", true),
        ("lien_he", "string", "Số điện thoại hoặc email người dùng cung cấp.", true));

    public async Task<ToolResult> InvokeAsync(JsonObject args, ToolContext ctx, CancellationToken ct)
    {
        var hoTen = ToolArgs.Str(args, "ho_ten");
        var lienHe = ToolArgs.Str(args, "lien_he");
        if (hoTen is null || lienHe is null)
            return ToolResult.AskBack(null, "Hãy hỏi họ tên và số điện thoại hoặc email của người dùng trước.");
        var lyDo = ToolArgs.Str(args, "ly_do") ?? "Người dùng cần cán bộ tuyển sinh tư vấn";
        var conversationId = ctx.ConversationId == Guid.Empty ? (Guid?)null : ctx.ConversationId;
        var t = await store.CreateTicketAsync(conversationId, hoTen, lienHe, lyDo, ct);
        return ToolResult.Ok(new JsonObject
        {
            ["ma_yeu_cau"] = t.Id.ToString(),
            ["thong_bao"] = "Cán bộ tuyển sinh sẽ liên hệ lại trong giờ hành chính.",
        });
    }
}
