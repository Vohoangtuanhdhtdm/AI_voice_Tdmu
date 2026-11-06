namespace Tdmu.Advisor.Api.Features.KnowledgeBase.Entities;

/// <summary>Luôn tuần tự hóa JSON thành tên (ví dụ "TinChi"), không phải số, ở mọi API.</summary>
[System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter))]
public enum TrangThaiTaiLieu { ChoXuLy, DangXuLy, Xong, ThatBai }

public class TaiLieu : KbEntity
{
    public string TieuDe { get; set; } = "";
    public string DuongDanFile { get; set; } = "";
    public int? NamApDung { get; set; }
    public TrangThaiTaiLieu TrangThai { get; set; } = TrangThaiTaiLieu.ChoXuLy;
    public string? Loi { get; set; }
    public string TenFileGoc { get; set; } = "";
    public string LoaiNoiDung { get; set; } = "";
    public long KichThuoc { get; set; }

    // Trạng thái xử lý và lỗi do worker đặt nên không phải thay đổi nội dung.
    private sealed record Key(string TieuDe, string DuongDanFile, int? NamApDung, string TenFileGoc, string LoaiNoiDung, long KichThuoc, string? NguonTen, string? NguonUrl);
    public override object ContentKey() => new Key(TieuDe, DuongDanFile, NamApDung, TenFileGoc, LoaiNoiDung, KichThuoc, NguonTen, NguonUrl);
    public override string Label(IKbLabelLookup lookup) => $"Tài liệu {TieuDe}";
}
