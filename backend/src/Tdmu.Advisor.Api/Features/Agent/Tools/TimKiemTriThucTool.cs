using System.Text.Json.Nodes;
using Tdmu.Advisor.Api.Features.Agent.Search;

namespace Tdmu.Advisor.Api.Features.Agent.Tools;

public class TimKiemTriThucTool(IKnowledgeSearch search) : IAgentTool
{
    public string Name => "tim_kiem_tri_thuc";

    public const string KhongTrichSoLieu =
        "Đoạn trích chỉ dùng cho thông tin mô tả; không trích điểm chuẩn, chỉ tiêu, học phí hay mốc thời gian từ đây.";

    public JsonObject Declaration { get; } = ToolDeclaration.Create("tim_kiem_tri_thuc",
        "Tìm trong câu hỏi thường gặp và tài liệu tuyển sinh của trường cho thông tin mô tả: quy chế, chính sách, học bổng, ký túc xá, thủ tục, hồ sơ... Không dùng để tra điểm chuẩn, chỉ tiêu, học phí hay lịch tuyển sinh: các số liệu đó chỉ lấy từ tool tra_*.",
        ("cau_hoi", "string", "Câu hỏi của người dùng, viết đầy đủ ý.", true));

    public async Task<ToolResult> InvokeAsync(JsonObject args, ToolContext ctx, CancellationToken ct)
    {
        var r = await search.SearchAsync(ToolArgs.Str(args, "cau_hoi") ?? "", ctx.Scope, ct);
        var nguon = ToolResult.Sources(r.Hits.Select(h => ((string?)h.NguonTen, h.NguonUrl)));
        return r.Kind switch
        {
            "faq" => ToolResult.Ok(new JsonObject { ["tra_loi"] = r.FaqAnswer }, nguon: nguon),
            "tai_lieu" => ToolResult.Ok(new JsonObject
            {
                ["doan"] = new JsonArray(r.Hits.Select(h => (JsonNode)new JsonObject { ["noi_dung"] = h.NoiDung, ["vi_tri"] = h.ViTri }).ToArray()),
            }, nguon: nguon, huongDan: "Chỉ trả lời theo nội dung các đoạn trích; đoạn nào không nói tới điều người dùng hỏi thì coi như chưa có thông tin. " + KhongTrichSoLieu),
            _ => ToolResult.NotFound("thông tin này"),
        };
    }
}
