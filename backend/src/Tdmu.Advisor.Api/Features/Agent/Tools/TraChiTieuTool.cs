using System.Text.Json.Nodes;
using Tdmu.Advisor.Api.Features.KnowledgeBase.Query;

namespace Tdmu.Advisor.Api.Features.Agent.Tools;

public class TraChiTieuTool(IKnowledgeQuery kb) : IAgentTool
{
    public string Name => "tra_chi_tieu";

    public JsonObject Declaration { get; } = ToolDeclaration.Create("tra_chi_tieu",
        "Tra chỉ tiêu tuyển sinh (số lượng tuyển) của một ngành theo năm, có thể theo từng phương thức xét tuyển.",
        ("nganh", "string", ToolDeclaration.NganhDescription, true),
        ("nam", "integer", ToolDeclaration.NamDescription, false),
        ("phuong_thuc", "string", ToolDeclaration.PhuongThucDescription, false));

    public async Task<ToolResult> InvokeAsync(JsonObject args, ToolContext ctx, CancellationToken ct)
    {
        var nganh = await ToolLookup.NganhAsync(kb, ctx, ToolArgs.Str(args, "nganh"), ct);
        if (nganh.Stop is not null) return nganh.Stop;
        var pt = await ToolLookup.PhuongThucAsync(kb, ctx, ToolArgs.Str(args, "phuong_thuc"), ct);
        if (pt.Stop is not null) return pt.Stop;
        var nam = await ToolLookup.NamAsync(kb, ctx, args, ct);
        if (nam.Stop is not null) return nam.Stop;

        var n = nganh.Value!;
        var rows = await kb.GetChiTieuAsync(ctx.Scope, n.LogicalId, nam.Value!.Value, pt.Value?.LogicalId, ct);
        if (rows.Count == 0)
            return ToolResult.NotFound($"chỉ tiêu ngành {n.Ten} năm {nam.Value}" + (pt.Value is { } p ? $" theo phương thức {p.Ten}" : ""));

        // Bản ghi không gắn phương thức là chỉ tiêu chung của ngành.
        static string Label(ChiTieuRow r) => r.PhuongThucTen ?? "chỉ tiêu chung của ngành";
        var data = new JsonArray(rows.Select(r => (JsonNode)new JsonObject { ["phuong_thuc"] = Label(r), ["so_luong"] = r.SoLuong }).ToArray());
        var cachDoc = $"Ngành {n.Ten}: " + string.Join("; ", rows.Select(r => $"{VietnameseNumberReader.Read(r.SoLuong)} chỉ tiêu ({Label(r)})"));
        return ToolResult.Ok(new JsonObject { ["nganh"] = ToolLookup.NganhJson(n), ["chi_tieu"] = data },
            cachDoc, nam.Value, ToolResult.Sources(rows.Select(r => (r.NguonTen, r.NguonUrl))),
            ToolLookup.XacNhanNganh(n, ToolArgs.Str(args, "nganh")!));
    }
}
