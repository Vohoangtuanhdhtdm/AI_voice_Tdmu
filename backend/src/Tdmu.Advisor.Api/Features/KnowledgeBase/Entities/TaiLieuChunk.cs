using NpgsqlTypes;
using Pgvector;

namespace Tdmu.Advisor.Api.Features.KnowledgeBase.Entities;

public class TaiLieuChunk : KbEntity
{
    public Guid TaiLieuLid { get; set; }
    public int ThuTu { get; set; }
    public string NoiDung { get; set; } = "";
    public string? ViTri { get; set; }
    public Vector? Embedding { get; set; }
    public NpgsqlTsVector? Tsv { get; set; }

    // Embedding và tsv được suy ra từ nội dung nên không nằm trong khóa nội dung.
    private sealed record Key(Guid TaiLieuLid, int ThuTu, string NoiDung, string? ViTri, string? NguonTen, string? NguonUrl);
    public override object ContentKey() => new Key(TaiLieuLid, ThuTu, NoiDung, ViTri, NguonTen, NguonUrl);
    public override string Label() => $"Đoạn {ThuTu} của tài liệu {TaiLieuLid}";
}
