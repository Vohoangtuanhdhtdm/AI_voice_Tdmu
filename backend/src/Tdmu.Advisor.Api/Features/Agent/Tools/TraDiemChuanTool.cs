using System.Text.Json.Nodes;
using Tdmu.Advisor.Api.Features.KnowledgeBase.Query;

namespace Tdmu.Advisor.Api.Features.Agent.Tools;

public class TraDiemChuanTool(IKnowledgeQuery kb) : IAgentTool
{
    public string Name => "tra_diem_chuan";

    public JsonObject Declaration { get; } = ToolDeclaration.Create("tra_diem_chuan",
        "Tra điểm chuẩn (điểm trúng tuyển) của một ngành theo năm và phương thức xét tuyển. Kết quả kèm thang điểm của từng phương thức.",
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
        var rows = await kb.GetDiemChuanAsync(ctx.Scope, n.LogicalId, nam.Value!.Value, pt.Value?.LogicalId, ct);
        if (rows.Count == 0)
            return ToolResult.NotFound($"điểm chuẩn ngành {n.Ten} năm {nam.Value}" + (pt.Value is { } p ? $" theo phương thức {p.Ten}" : ""));

        var data = new JsonArray(rows.Select(r => (JsonNode)new JsonObject
        {
            ["phuong_thuc"] = r.PhuongThucTen, ["diem"] = r.Diem, ["thang_diem"] = r.ThangDiem, ["ghi_chu"] = r.GhiChu,
        }).ToArray());
        var cachDoc = string.Join("; ", rows.Select(r => $"{VietnameseNumberReader.Read(r.Diem)} điểm ({r.PhuongThucTen})"));
        return ToolResult.Ok(data,
            cachDoc, nam.Value, ToolResult.Sources(rows.Select(r => (r.NguonTen, r.NguonUrl))));
    }
}
