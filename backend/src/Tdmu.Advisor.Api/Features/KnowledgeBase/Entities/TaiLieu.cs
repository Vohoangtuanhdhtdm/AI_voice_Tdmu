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

    private sealed record Key(string TieuDe, string DuongDanFile, int? NamApDung, TrangThaiTaiLieu TrangThai, string? Loi, string? NguonTen, string? NguonUrl);
    public override object ContentKey() => new Key(TieuDe, DuongDanFile, NamApDung, TrangThai, Loi, NguonTen, NguonUrl);
    public override string Label(IKbLabelLookup lookup) => $"Tài liệu {TieuDe}";
}
