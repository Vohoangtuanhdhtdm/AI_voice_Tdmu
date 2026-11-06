using System.Text.Json.Nodes;
using Tdmu.Advisor.Api.Features.KnowledgeBase.Entities;
using Tdmu.Advisor.Api.Features.KnowledgeBase.Query;

namespace Tdmu.Advisor.Api.Features.Agent.Tools;

public class TraHocPhiTool(IKnowledgeQuery kb) : IAgentTool
{
    public string Name => "tra_hoc_phi";

    public JsonObject Declaration { get; } = ToolDeclaration.Create("tra_hoc_phi",
        "Tra mức học phí theo năm: mức riêng của một ngành (nếu có) cùng mức áp dụng chung. Bỏ trống ngành để chỉ lấy mức chung.",
        ("nganh", "string", ToolDeclaration.NganhDescription, false),
        ("nam", "integer", ToolDeclaration.NamDescription, false));

    private static string DonVi(DonViHocPhi d) => d switch
    {
        DonViHocPhi.TinChi => "tín chỉ",
        DonViHocPhi.Nam => "năm",
        DonViHocPhi.HocKy => "học kỳ",
        _ => d.ToString(),
    };

    public async Task<ToolResult> InvokeAsync(JsonObject args, ToolContext ctx, CancellationToken ct)
    {
        NganhMatch? n = null;
        if (ToolArgs.Str(args, "nganh") is { } nganhArg)
        {
            var nganh = await ToolLookup.NganhAsync(kb, ctx, nganhArg, ct);
            if (nganh.Stop is not null) return nganh.Stop;
            n = nganh.Value;
        }
        var nam = await ToolLookup.NamAsync(kb, ctx, args, ct);
        if (nam.Stop is not null) return nam.Stop;

        var rows = await kb.GetHocPhiAsync(ctx.Scope, n?.LogicalId, nam.Value!.Value, ct);
        if (rows.Count == 0)
            return ToolResult.NotFound($"học phí {(n is null ? "chung" : "ngành " + n.Ten)} năm {nam.Value}");

        string Scope(HocPhiView r) => r.ApDungChung ? "áp dụng chung" : $"ngành {n!.Ten}";
        var data = new JsonArray(rows.Select(r => (JsonNode)new JsonObject
        {
            ["muc"] = r.Muc, ["don_vi"] = DonVi(r.DonVi), ["ap_dung"] = Scope(r), ["ghi_chu"] = r.GhiChu,
        }).ToArray());
        var cachDoc = string.Join("; ", rows.Select(r => $"{VietnameseNumberReader.ReadMoney(r.Muc)} một {DonVi(r.DonVi)} ({Scope(r)})"));
        return ToolResult.Ok(data, cachDoc, nam.Value, ToolResult.Sources(rows.Select(r => (r.NguonTen, r.NguonUrl))));
    }
}
