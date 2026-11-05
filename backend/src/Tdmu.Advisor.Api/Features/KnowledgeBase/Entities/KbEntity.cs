namespace Tdmu.Advisor.Api.Features.KnowledgeBase.Entities;

/// <summary>Bản ghi tri thức. Nháp có <see cref="ReleaseId"/> = null; mỗi release giữ một bản sao với cùng <see cref="LogicalId"/>.</summary>
public abstract class KbEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid LogicalId { get; set; } = Guid.NewGuid();
    public Guid? ReleaseId { get; set; }
    public string? NguonTen { get; set; }
    public string? NguonUrl { get; set; }
    public DateTimeOffset CapNhatLuc { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Record chứa mọi trường nội dung (gồm *Lid, không gồm Id, ReleaseId, CapNhatLuc). Hai khóa bằng nhau nghĩa là bản ghi không đổi.</summary>
    public abstract object ContentKey();

    /// <summary>Chuỗi hiển thị trong danh sách thay đổi.</summary>
    public abstract string Label(IKbLabelLookup lookup);

    protected static string Join(IEnumerable<string> items) => string.Join('\u001f', items);
}
