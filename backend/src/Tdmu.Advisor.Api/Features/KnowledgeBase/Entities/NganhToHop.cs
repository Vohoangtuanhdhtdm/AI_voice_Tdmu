namespace Tdmu.Advisor.Api.Features.KnowledgeBase.Entities;

public class NganhToHop : KbEntity
{
    public Guid NganhLid { get; set; }
    public int Nam { get; set; }
    public Guid ToHopLid { get; set; }

    private sealed record Key(Guid NganhLid, int Nam, Guid ToHopLid, string? NguonTen, string? NguonUrl);
    public override object ContentKey() => new Key(NganhLid, Nam, ToHopLid, NguonTen, NguonUrl);
    public override string Label(IKbLabelLookup lookup) => $"Tổ hợp {lookup.ToHop(ToHopLid)} – {lookup.Nganh(NganhLid)} {Nam}";
}
