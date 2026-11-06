using System.Text.Json.Nodes;
using Tdmu.Advisor.Api.Features.Agent.Search;

namespace Tdmu.Advisor.Api.Features.Agent.Tools;

public class TimKiemTriThucTool(IKnowledgeSearch search) : IAgentTool
{
    public string Name => "tim_kiem_tri_thuc";

    public JsonObject Declaration { get; } = ToolDeclaration.Create("tim_kiem_tri_thuc",
        "Tìm trong câu hỏi thường gặp và tài liệu tuyển sinh của trường (quy chế, đề án, học bổng, ký túc xá, thủ tục...) cho những câu không tra được bằng các tool số liệu khác.",
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
            }, nguon: nguon, huongDan: "Chỉ trả lời theo nội dung các đoạn trích; đoạn nào không nói tới điều người dùng hỏi thì coi như chưa có thông tin."),
            _ => ToolResult.NotFound("thông tin này"),
        };
    }
}
