namespace Tdmu.Advisor.Api.Features.KnowledgeBase.Entities;

public class ToHop : KbEntity
{
    public string Ma { get; set; } = "";
    public List<string> Mon { get; set; } = [];
    public string? CachDoc { get; set; }

    private sealed record Key(string Ma, string Mon, string? CachDoc, string? NguonTen, string? NguonUrl);
    public override object ContentKey() => new Key(Ma, Join(Mon), CachDoc, NguonTen, NguonUrl);
    public override string Label() => $"Tổ hợp {Ma}";
}
