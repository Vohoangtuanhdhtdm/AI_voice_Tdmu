using System.Text.Json.Nodes;
using Tdmu.Advisor.Api.Features.KnowledgeBase.Query;

namespace Tdmu.Advisor.Api.Features.Agent.Tools;

public class TimNganhTool(IKnowledgeQuery kb) : IAgentTool
{
    public string Name => "tim_nganh";

    public JsonObject Declaration { get; } = ToolDeclaration.Create("tim_nganh",
        "Tìm ngành đào tạo của trường theo tên, tên gọi khác, viết tắt hoặc mã ngành (chấp nhận không dấu, sai chính tả nhẹ). Dùng để xác định đúng ngành người dùng hỏi.",
        ("tu_khoa", "string", "Từ khóa tên ngành hoặc mã ngành như người dùng nói.", true));

    public async Task<ToolResult> InvokeAsync(JsonObject args, ToolContext ctx, CancellationToken ct)
    {
        var tuKhoa = ToolArgs.Str(args, "tu_khoa");
        if (tuKhoa is null) return ToolResult.AskBack(null, "Hãy hỏi người dùng muốn tìm ngành nào.");
        var found = await kb.FindNganhAsync(ctx.Scope, tuKhoa, ct);
        if (found.Count == 0) return ToolResult.NotFound("ngành này");
        if (found.Count > 1) return ToolResult.AskBack(ToolLookup.LuaChonNganh(found), ToolLookup.ChonNganhHuongDan);
        var n = found[0];
        return ToolResult.Ok(new JsonObject { ["ma_nganh"] = n.MaNganh, ["ten"] = n.Ten, ["con_tuyen"] = n.ConTuyen },
            huongDan: n.ConTuyen ? null : "Ngành này hiện đã ngừng tuyển sinh; phải nói rõ điều này với người dùng.");
    }
}
