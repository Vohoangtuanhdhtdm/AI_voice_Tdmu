using Pgvector;

namespace Tdmu.Advisor.Api.Features.KnowledgeBase.Entities;

public class Faq : KbEntity
{
    public string CauHoi { get; set; } = "";
    public List<string> CachHoiKhac { get; set; } = [];
    public string TraLoi { get; set; } = "";
    public string? ChuDe { get; set; }
    public Vector? Embedding { get; set; }

    // Embedding được suy ra từ nội dung nên không nằm trong khóa nội dung.
    private sealed record Key(string CauHoi, string CachHoiKhac, string TraLoi, string? ChuDe, string? NguonTen, string? NguonUrl);
    public override object ContentKey() => new Key(CauHoi, Join(CachHoiKhac), TraLoi, ChuDe, NguonTen, NguonUrl);
    public override string Label() => $"Câu hỏi thường gặp: {CauHoi}";
}
