namespace Tdmu.Advisor.Api.Features.KnowledgeBase.Entities;

public class MuaTuyenSinh : KbEntity
{
    public int Nam { get; set; }
    public bool LaHienTai { get; set; }

    private sealed record Key(int Nam, bool LaHienTai, string? NguonTen, string? NguonUrl);
    public override object ContentKey() => new Key(Nam, LaHienTai, NguonTen, NguonUrl);
    public override string Label() => $"Mùa tuyển sinh {Nam}";
}
