using System.Text.Json.Nodes;
using Tdmu.Advisor.Api.Features.KnowledgeBase.Query;

namespace Tdmu.Advisor.Api.Features.Agent.Tools;

public class TraToHopTool(IKnowledgeQuery kb) : IAgentTool
{
    public string Name => "tra_to_hop";

    public JsonObject Declaration { get; } = ToolDeclaration.Create("tra_to_hop",
        "Tra tổ hợp môn xét tuyển của mùa tuyển sinh hiện tại: truyền ngành để biết ngành xét những tổ hợp nào; truyền tổ hợp để biết những ngành nào xét tổ hợp đó; truyền cả hai để kiểm tra ngành có xét tổ hợp đó không.",
        ("nganh", "string", ToolDeclaration.NganhDescription, false),
        ("to_hop", "string", "Mã tổ hợp môn, ví dụ A00, D01.", false));

    private static JsonObject ToHopJson(string ma, IReadOnlyList<string> mon, string? cachDoc) => new()
    {
        ["ma"] = ma, ["mon"] = new JsonArray(mon.Select(m => (JsonNode)m).ToArray()), ["cach_doc"] = cachDoc,
    };

    public async Task<ToolResult> InvokeAsync(JsonObject args, ToolContext ctx, CancellationToken ct)
    {
        var nganhArg = ToolArgs.Str(args, "nganh");
        var toHopArg = ToolArgs.Str(args, "to_hop");
        if (nganhArg is null && toHopArg is null)
            return ToolResult.AskBack(null, "Hãy hỏi người dùng muốn biết tổ hợp của ngành nào, hay các ngành xét một tổ hợp cụ thể.");
        var nam = await ToolLookup.NamAsync(kb, ctx, args, ct);
        if (nam.Stop is not null) return nam.Stop;
        var y = nam.Value!.Value;

        if (nganhArg is null)
        {
            var th = await kb.FindToHopAsync(ctx.Scope, toHopArg!, ct);
            if (th is null) return ToolResult.NotFound($"tổ hợp {toHopArg}");
            var nganhs = await kb.GetNganhOfToHopAsync(ctx.Scope, th.LogicalId, y, ct);
            if (nganhs.Count == 0) return ToolResult.NotFound($"ngành xét tổ hợp {th.Ma} năm {y}");
            var data = new JsonObject
            {
                ["to_hop"] = ToHopJson(th.Ma, th.Mon, th.CachDoc),
                ["nganh"] = new JsonArray(nganhs.Select(n => (JsonNode)new JsonObject { ["ma_nganh"] = n.MaNganh, ["ten"] = n.Ten, ["con_tuyen"] = n.ConTuyen }).ToArray()),
            };
            return ToolResult.Ok(data, th.CachDoc, y,
                ToolResult.Sources(nganhs.Select(n => (n.NguonTen, n.NguonUrl)).Prepend((th.NguonTen, th.NguonUrl))));
        }

        var nganh = await ToolLookup.NganhAsync(kb, ctx, nganhArg, ct);
        if (nganh.Stop is not null) return nganh.Stop;
        var ng = nganh.Value!;
        var list = await kb.GetToHopOfNganhAsync(ctx.Scope, ng.LogicalId, y, ct);
        if (list.Count == 0) return ToolResult.NotFound($"tổ hợp xét tuyển của ngành {ng.Ten} năm {y}");

        var result = new JsonObject
        {
            ["nganh"] = ToolLookup.NganhJson(ng),
            ["to_hop"] = new JsonArray(list.Select(t => (JsonNode)ToHopJson(t.Ma, t.Mon, t.CachDoc)).ToArray()),
        };
        var cachDoc = $"Ngành {ng.Ten}: " + string.Join(", ", list.Select(t => t.CachDoc ?? t.Ma));
        if (toHopArg is not null)
        {
            // Danh sách tổ hợp của ngành đã có đủ, nên "không có trong danh sách" là câu trả lời xác định.
            var key = toHopArg.Replace(" ", "").ToUpperInvariant();
            result["co_xet_to_hop"] = list.Any(t => t.Ma.ToUpperInvariant() == key);
        }
        return ToolResult.Ok(result, cachDoc, y, ToolResult.Sources(list.Select(t => (t.NguonTen, t.NguonUrl))),
            ToolLookup.XacNhanNganh(ng, nganhArg));
    }
}
