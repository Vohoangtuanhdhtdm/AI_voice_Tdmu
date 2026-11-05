namespace Tdmu.Advisor.Api.Features.KnowledgeBase.Entities;

/// <summary>Tra tên hiển thị của thực thể được tham chiếu theo logical id, dùng cho <see cref="KbEntity.Label"/>.</summary>
public interface IKbLabelLookup
{
    string Nganh(Guid lid);
    string PhuongThuc(Guid lid);
    string ToHop(Guid lid);
    string TaiLieu(Guid lid);
}

public sealed class DictionaryKbLabelLookup(
    IReadOnlyDictionary<Guid, string>? nganh = null,
    IReadOnlyDictionary<Guid, string>? phuongThuc = null,
    IReadOnlyDictionary<Guid, string>? toHop = null,
    IReadOnlyDictionary<Guid, string>? taiLieu = null) : IKbLabelLookup
{
    public string Nganh(Guid lid) => Find(nganh, lid);
    public string PhuongThuc(Guid lid) => Find(phuongThuc, lid);
    public string ToHop(Guid lid) => Find(toHop, lid);
    public string TaiLieu(Guid lid) => Find(taiLieu, lid);

    private static string Find(IReadOnlyDictionary<Guid, string>? map, Guid lid) =>
        map is not null && map.TryGetValue(lid, out var s) ? s : "#" + lid.ToString("N")[..8];
}
