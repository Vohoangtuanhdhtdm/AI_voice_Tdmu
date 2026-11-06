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

public record NganhMatch(Guid LogicalId, string MaNganh, string Ten, bool ConTuyen);
public record PhuongThucRef(Guid LogicalId, string Ma, string Ten, decimal ThangDiem);
public record ToHopRef(Guid LogicalId, string Ma, IReadOnlyList<string> Mon, string? CachDoc, string? NguonTen, string? NguonUrl);
public record NganhOfToHopView(string MaNganh, string Ten, bool ConTuyen, string? NguonTen, string? NguonUrl);
public record ChiTieuRow(string? PhuongThucMa, string? PhuongThucTen, int SoLuong, string? NguonTen, string? NguonUrl);
/// <summary>Thời điểm giữ nguyên UTC; tool tự đổi sang giờ Việt Nam.</summary>
public record MocThoiGianView(string TenMoc, DateTimeOffset BatDau, DateTimeOffset? KetThuc, string? PhuongThucTen, string? MoTa, string? NguonTen, string? NguonUrl);

public interface IKnowledgeQuery
{
    Task<Guid?> ResolveReleaseAsync(KbScope scope, CancellationToken ct);
    Task<int?> CurrentYearAsync(KbScope scope, CancellationToken ct);
    Task<IReadOnlyList<NganhView>> ListNganhAsync(KbScope scope, CancellationToken ct);
    Task<NganhDetailView?> GetNganhAsync(KbScope scope, string maNganh, CancellationToken ct);

    // Các truy vấn cho tool của Agent (Task 6), đều lọc theo scope.

    /// <summary>
    /// Tìm ngành theo mã, tên hoặc tên gọi khác (không phân biệt hoa thường, bỏ dấu). Trùng khớp hoàn toàn được ưu tiên,
    /// sau đó là chứa từ khóa, cuối cùng là so khớp gần đúng pg_trgm (similarity ≥ 0,3, tối đa 5).
    /// </summary>
    Task<IReadOnlyList<NganhMatch>> FindNganhAsync(KbScope scope, string tuKhoa, CancellationToken ct);
    Task<IReadOnlyList<PhuongThucRef>> ListPhuongThucAsync(KbScope scope, CancellationToken ct);
    Task<IReadOnlyList<DiemChuanView>> GetDiemChuanAsync(KbScope scope, Guid nganhLid, int nam, Guid? phuongThucLid, CancellationToken ct);
    Task<IReadOnlyList<ChiTieuRow>> GetChiTieuAsync(KbScope scope, Guid nganhLid, int nam, Guid? phuongThucLid, CancellationToken ct);
    /// <summary>nganhLid = null: chỉ mức áp dụng chung; ngược lại gồm mức riêng của ngành và mức chung.</summary>
    Task<IReadOnlyList<HocPhiView>> GetHocPhiAsync(KbScope scope, Guid? nganhLid, int nam, CancellationToken ct);
    /// <summary>phuongThucLid khác null: gồm mốc của phương thức đó và các mốc chung.</summary>
    Task<IReadOnlyList<MocThoiGianView>> GetMocThoiGianAsync(KbScope scope, int nam, Guid? phuongThucLid, CancellationToken ct);
    Task<IReadOnlyList<ToHopView>> GetToHopOfNganhAsync(KbScope scope, Guid nganhLid, int nam, CancellationToken ct);
    Task<ToHopRef?> FindToHopAsync(KbScope scope, string ma, CancellationToken ct);
    Task<IReadOnlyList<NganhOfToHopView>> GetNganhOfToHopAsync(KbScope scope, Guid toHopLid, int nam, CancellationToken ct);
}
