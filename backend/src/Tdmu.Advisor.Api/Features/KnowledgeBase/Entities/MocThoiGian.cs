namespace Tdmu.Advisor.Api.Features.KnowledgeBase.Entities;

public class MocThoiGian : KbEntity
{
    public int Nam { get; set; }
    public string TenMoc { get; set; } = "";
    public DateTimeOffset BatDau { get; set; }
    public DateTimeOffset? KetThuc { get; set; }
    public Guid? PhuongThucLid { get; set; }
    public string? MoTa { get; set; }

    private sealed record Key(int Nam, string TenMoc, DateTimeOffset BatDau, DateTimeOffset? KetThuc, Guid? PhuongThucLid, string? MoTa, string? NguonTen, string? NguonUrl);
    public override object ContentKey() => new Key(Nam, TenMoc, BatDau, KetThuc, PhuongThucLid, MoTa, NguonTen, NguonUrl);
    public override string Label() => $"Mốc thời gian {TenMoc} {Nam}";
}
