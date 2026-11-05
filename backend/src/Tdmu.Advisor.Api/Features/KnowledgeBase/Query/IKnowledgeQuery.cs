using Tdmu.Advisor.Api.Features.KnowledgeBase.Entities;

namespace Tdmu.Advisor.Api.Features.KnowledgeBase.Query;

public class NoActiveReleaseException() : Exception("Chưa có bản xuất bản nào");

/// <summary>ConTuyen = false: ngành đã ngừng tuyển (vẫn trả về để trợ lý nói rõ, không lọc bỏ).</summary>
public record NganhView(string MaNganh, string Ten, string? DonVi, string? MoTaNgan, bool ConTuyen);
public record ToHopView(string Ma, IReadOnlyList<string> Mon, string? CachDoc, string? NguonTen, string? NguonUrl);
public record DiemChuanView(string PhuongThucMa, string PhuongThucTen, decimal ThangDiem, decimal Diem, string? GhiChu, string? NguonTen, string? NguonUrl);
public record ChiTieuView(string? PhuongThucTen, int SoLuong, string? NguonTen, string? NguonUrl);
public record HocPhiView(decimal Muc, DonViHocPhi DonVi, string? GhiChu, bool ApDungChung, string? NguonTen, string? NguonUrl);
public record NganhDetailView(
    NganhView Nganh, int? Nam,
    IReadOnlyList<ToHopView> ToHop, IReadOnlyList<DiemChuanView> DiemChuan,
    IReadOnlyList<ChiTieuView> ChiTieu, IReadOnlyList<HocPhiView> HocPhi);

public interface IKnowledgeQuery
{
    Task<Guid?> ResolveReleaseAsync(KbScope scope, CancellationToken ct);
    Task<int?> CurrentYearAsync(KbScope scope, CancellationToken ct);
    Task<IReadOnlyList<NganhView>> ListNganhAsync(KbScope scope, CancellationToken ct);
    Task<NganhDetailView?> GetNganhAsync(KbScope scope, string maNganh, CancellationToken ct);
}
