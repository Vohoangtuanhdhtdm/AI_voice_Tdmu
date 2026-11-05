namespace Tdmu.Advisor.Api.Features.KnowledgeBase.Entities;

public class PhuongThuc : KbEntity
{
    public string Ma { get; set; } = "";
    public string Ten { get; set; } = "";
    public string? MoTa { get; set; }
    /// <summary>Thang điểm tối đa của phương thức (30 cho thi THPT, tới 1200 cho ĐGNL).</summary>
    public decimal ThangDiem { get; set; } = 30m;

    private sealed record Key(string Ma, string Ten, string? MoTa, decimal ThangDiem, string? NguonTen, string? NguonUrl);
    public override object ContentKey() => new Key(Ma, Ten, MoTa, ThangDiem, NguonTen, NguonUrl);
    public override string Label(IKbLabelLookup lookup) => $"Phương thức {Ten}";
}
