namespace Tdmu.Advisor.Api.Features.KnowledgeBase.Entities;

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
    public override string Label() => $"Tài liệu {TieuDe}";
}
