using System.Text.Json.Nodes;
using Tdmu.Advisor.Api.Features.KnowledgeBase.Query;

namespace Tdmu.Advisor.Api.Features.Agent.Tools;

public class TraLichTuyenSinhTool(IKnowledgeQuery kb) : IAgentTool
{
    public string Name => "tra_lich_tuyen_sinh";

    public JsonObject Declaration { get; } = ToolDeclaration.Create("tra_lich_tuyen_sinh",
        "Tra lịch tuyển sinh (các mốc thời gian: đăng ký, nộp hồ sơ, công bố kết quả...) theo năm, có thể lọc theo phương thức xét tuyển. Giờ đã đổi sang giờ Việt Nam.",
        ("nam", "integer", ToolDeclaration.NamDescription, false),
        ("phuong_thuc", "string", ToolDeclaration.PhuongThucDescription, false));

    public async Task<ToolResult> InvokeAsync(JsonObject args, ToolContext ctx, CancellationToken ct)
    {
        var pt = await ToolLookup.PhuongThucAsync(kb, ctx, ToolArgs.Str(args, "phuong_thuc"), ct);
        if (pt.Stop is not null) return pt.Stop;
        var nam = await ToolLookup.NamAsync(kb, ctx, args, ct);
        if (nam.Stop is not null) return nam.Stop;

        var rows = await kb.GetMocThoiGianAsync(ctx.Scope, nam.Value!.Value, pt.Value?.LogicalId, ct);
        if (rows.Count == 0)
            return ToolResult.NotFound($"lịch tuyển sinh năm {nam.Value}" + (pt.Value is { } p ? $" cho phương thức {p.Ten}" : ""));

        var data = new JsonArray(rows.Select(r => (JsonNode)new JsonObject
        {
            ["ten_moc"] = r.TenMoc,
            ["bat_dau"] = ToolText.Date(r.BatDau),
            ["ket_thuc"] = r.KetThuc is { } kt ? ToolText.Date(kt) : null,
            ["phuong_thuc"] = r.PhuongThucTen ?? "chung",
            ["mo_ta"] = r.MoTa,
        }).ToArray());
        var cachDoc = string.Join("; ", rows.Select(r => r.KetThuc is { } kt
            ? $"{r.TenMoc}: từ {ToolText.Date(r.BatDau)} đến {ToolText.Date(kt)}"
            : $"{r.TenMoc}: {ToolText.Date(r.BatDau)}"));
        return ToolResult.Ok(data, cachDoc, nam.Value, ToolResult.Sources(rows.Select(r => (r.NguonTen, r.NguonUrl))));
    }
}
