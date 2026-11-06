using System.Text.Json.Nodes;

namespace Tdmu.Advisor.Api.Features.Agent.Tools;

public record NguonRef(string Ten, string? Url);

/// <summary>Kết quả tool (spec 5.1). <see cref="KetQua"/> ∈ ok | khong_tim_thay | can_hoi_lai | loi_he_thong.</summary>
public record ToolResult(string KetQua, JsonNode? DuLieu, string? CachDoc, int? NamApDung, IReadOnlyList<NguonRef> Nguon, string? HuongDan)
{
    public const string KetQuaOk = "ok", KetQuaKhongTimThay = "khong_tim_thay", KetQuaCanHoiLai = "can_hoi_lai", KetQuaLoi = "loi_he_thong";

    public static ToolResult Ok(JsonNode? duLieu, string? cachDoc = null, int? namApDung = null, IEnumerable<NguonRef>? nguon = null, string? huongDan = null) =>
        new(KetQuaOk, duLieu, cachDoc, namApDung, Distinct(nguon), huongDan);

    /// <param name="what">Mô tả thứ không tìm thấy, ví dụ "điểm chuẩn ngành Luật năm 2026".</param>
    public static ToolResult NotFound(string what) =>
        new(KetQuaKhongTimThay, null, null, null, [],
            $"Không có {what} trong cơ sở dữ liệu. Không được suy ra là trường không có; hãy nói mình chưa có thông tin và hỏi người dùng có muốn để lại liên hệ để cán bộ tư vấn không.");

    public static ToolResult AskBack(JsonNode? options, string huongDan) =>
        new(KetQuaCanHoiLai, options, null, null, [], huongDan);

    public static ToolResult Error() =>
        new(KetQuaLoi, null, null, null, [],
            "Hệ thống tra cứu đang gặp sự cố. Không được tự nêu số liệu; hãy xin lỗi, nói mình chưa tra được và mời người dùng thử lại sau hoặc để lại liên hệ để cán bộ tuyển sinh hỗ trợ.");

    /// <summary>Nguồn của các bản ghi, bỏ trùng theo (ten, url) và bỏ bản ghi không ghi tên nguồn.</summary>
    public static IReadOnlyList<NguonRef> Sources(IEnumerable<(string? Ten, string? Url)> records) =>
        Distinct(records.Where(r => !string.IsNullOrWhiteSpace(r.Ten)).Select(r => new NguonRef(r.Ten!, r.Url)));

    private static IReadOnlyList<NguonRef> Distinct(IEnumerable<NguonRef>? nguon) => nguon?.Distinct().ToList() ?? [];

    public JsonObject ToJson() => new()
    {
        ["ket_qua"] = KetQua,
        ["du_lieu"] = DuLieu?.DeepClone(),
        ["cach_doc"] = CachDoc,
        ["nam_ap_dung"] = NamApDung,
        ["nguon"] = new JsonArray(Nguon.Select(n => (JsonNode)new JsonObject { ["ten"] = n.Ten, ["url"] = n.Url }).ToArray()),
        ["huong_dan"] = HuongDan,
    };
}
