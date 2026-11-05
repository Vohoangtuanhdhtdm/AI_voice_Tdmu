namespace Tdmu.Advisor.Api.Features.KnowledgeBase.Entities;

public class ChiTieu : KbEntity
{
    public Guid NganhLid { get; set; }
    public int Nam { get; set; }
    public Guid? PhuongThucLid { get; set; }
    public int SoLuong { get; set; }

    private sealed record Key(Guid NganhLid, int Nam, Guid? PhuongThucLid, int SoLuong, string? NguonTen, string? NguonUrl);
    public override object ContentKey() => new Key(NganhLid, Nam, PhuongThucLid, SoLuong, NguonTen, NguonUrl);
    public override string Label() => $"Chỉ tiêu {NganhLid} {Nam}";
}
