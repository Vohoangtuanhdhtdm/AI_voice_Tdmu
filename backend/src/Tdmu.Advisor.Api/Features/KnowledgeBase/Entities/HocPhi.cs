namespace Tdmu.Advisor.Api.Features.KnowledgeBase.Entities;

/// <summary>Luôn tuần tự hóa JSON thành tên (ví dụ "TinChi"), không phải số, ở mọi API.</summary>
[System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter))]
public enum DonViHocPhi { TinChi, Nam, HocKy }

public class HocPhi : KbEntity
{
    public Guid? NganhLid { get; set; }
    public int Nam { get; set; }
    public decimal Muc { get; set; }
    public DonViHocPhi DonVi { get; set; }
    public string? GhiChu { get; set; }

    private sealed record Key(Guid? NganhLid, int Nam, decimal Muc, DonViHocPhi DonVi, string? GhiChu, string? NguonTen, string? NguonUrl);
    public override object ContentKey() => new Key(NganhLid, Nam, Muc, DonVi, GhiChu, NguonTen, NguonUrl);
    public override string Label(IKbLabelLookup lookup) => $"Học phí {(NganhLid is { } n ? lookup.Nganh(n) : "chung")} {Nam}";
}
