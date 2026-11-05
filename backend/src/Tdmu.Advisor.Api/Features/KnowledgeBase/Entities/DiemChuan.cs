namespace Tdmu.Advisor.Api.Features.KnowledgeBase.Entities;

public class DiemChuan : KbEntity
{
    public Guid NganhLid { get; set; }
    public int Nam { get; set; }
    public Guid PhuongThucLid { get; set; }
    public decimal Diem { get; set; }
    public string? GhiChu { get; set; }

    private sealed record Key(Guid NganhLid, int Nam, Guid PhuongThucLid, decimal Diem, string? GhiChu, string? NguonTen, string? NguonUrl);
    public override object ContentKey() => new Key(NganhLid, Nam, PhuongThucLid, Diem, GhiChu, NguonTen, NguonUrl);
    public override string Label() => $"Điểm chuẩn {NganhLid} {Nam} – {PhuongThucLid}";
}
