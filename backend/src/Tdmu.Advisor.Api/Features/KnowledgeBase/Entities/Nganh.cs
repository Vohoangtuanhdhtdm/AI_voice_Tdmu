namespace Tdmu.Advisor.Api.Features.KnowledgeBase.Entities;

public class Nganh : KbEntity
{
    public string MaNganh { get; set; } = "";
    public string Ten { get; set; } = "";
    public List<string> TenGoiKhac { get; set; } = [];
    public string? DonVi { get; set; }
    public string? MoTaNgan { get; set; }
    public bool ConTuyen { get; set; } = true;

    private sealed record Key(string MaNganh, string Ten, string TenGoiKhac, string? DonVi, string? MoTaNgan, bool ConTuyen, string? NguonTen, string? NguonUrl);
    public override object ContentKey() => new Key(MaNganh, Ten, Join(TenGoiKhac), DonVi, MoTaNgan, ConTuyen, NguonTen, NguonUrl);
    public override string Label() => $"Ngành {Ten} ({MaNganh})";
}
