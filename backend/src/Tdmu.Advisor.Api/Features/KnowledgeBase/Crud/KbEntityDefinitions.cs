using System.Linq.Expressions;
using System.Text.Json.Serialization;
using Tdmu.Advisor.Api.Features.KnowledgeBase.Entities;
using Tdmu.Advisor.Api.Features.KnowledgeBase.Import;
using Tdmu.Advisor.Api.Infrastructure;

namespace Tdmu.Advisor.Api.Features.KnowledgeBase.Crud;

public record MuaTuyenSinhDto(Guid? LogicalId, int Nam, bool LaHienTai, string? NguonTen, string? NguonUrl);
public record NganhDto(Guid? LogicalId, string? MaNganh, string? Ten, List<string>? TenGoiKhac, string? DonVi, string? MoTaNgan, bool? ConTuyen, string? NguonTen, string? NguonUrl);
public record PhuongThucDto(Guid? LogicalId, string? Ma, string? Ten, string? MoTa, decimal? ThangDiem, string? NguonTen, string? NguonUrl);
public record ToHopDto(Guid? LogicalId, string? Ma, List<string>? Mon, string? CachDoc, string? NguonTen, string? NguonUrl);
public record NganhToHopDto(Guid? LogicalId, Guid? NganhLid, int Nam, Guid? ToHopLid, string? NguonTen, string? NguonUrl);
public record DiemChuanDto(Guid? LogicalId, Guid? NganhLid, int Nam, Guid? PhuongThucLid, decimal Diem, string? GhiChu, string? NguonTen, string? NguonUrl);
public record ChiTieuDto(Guid? LogicalId, Guid? NganhLid, int Nam, Guid? PhuongThucLid, int SoLuong, string? NguonTen, string? NguonUrl);
public record HocPhiDto(Guid? LogicalId, Guid? NganhLid, int Nam, decimal Muc, [property: JsonConverter(typeof(JsonStringEnumConverter))] DonViHocPhi DonVi, string? GhiChu, string? NguonTen, string? NguonUrl);
public record MocThoiGianDto(Guid? LogicalId, int Nam, string? TenMoc, DateTimeOffset BatDau, DateTimeOffset? KetThuc, Guid? PhuongThucLid, string? MoTa, string? NguonTen, string? NguonUrl);
public record FaqDto(Guid? LogicalId, string? CauHoi, List<string>? CachHoiKhac, string? TraLoi, string? ChuDe, string? NguonTen, string? NguonUrl);

/// <summary>Phần không phụ thuộc kiểu của một định nghĩa, để Task 7 (release) duyệt qua mọi loại tri thức.</summary>
public interface IKbEntityDefinition
{
    string Slug { get; }
    Type EntityType { get; }
    /// <summary>Mọi bản ghi của một scope (releaseId = null là bản nháp).</summary>
    IQueryable<KbEntity> Query(AdvisorDbContext db, Guid? releaseId);
    void Map(RouteGroupBuilder group);
}

public sealed class KbEntityDefinition<TEntity, TDto>(
    string slug,
    Func<TEntity, TDto> toDto,
    Action<TDto, TEntity> apply,
    Func<TDto, AdvisorDbContext, CancellationToken, Task<KbErrors>> validate,
    Func<int, Expression<Func<TEntity, bool>>>? namFilter = null) : IKbEntityDefinition
    where TEntity : KbEntity, new()
    where TDto : class
{
    public string Slug => slug;
    public Type EntityType => typeof(TEntity);
    public Func<TEntity, TDto> ToDto => toDto;
    /// <summary>Gán nội dung từ DTO vào entity (không đụng Id, LogicalId, ReleaseId).</summary>
    public Action<TDto, TEntity> Apply => apply;
    public Func<TDto, AdvisorDbContext, CancellationToken, Task<KbErrors>> Validate => validate;
    public Func<int, Expression<Func<TEntity, bool>>>? NamFilter => namFilter;

    public IQueryable<KbEntity> Query(AdvisorDbContext db, Guid? releaseId) => db.Set<TEntity>().Where(x => x.ReleaseId == releaseId);
    public void Map(RouteGroupBuilder group) => KbCrudEndpoints.MapCrud(group, this);
}

public static class KbEntityDefinitions
{
    public static readonly KbEntityDefinition<MuaTuyenSinh, MuaTuyenSinhDto> MuaTuyenSinh = new("mua-tuyen-sinh",
        e => new(e.LogicalId, e.Nam, e.LaHienTai, e.NguonTen, e.NguonUrl),
        (d, e) => { e.Nam = d.Nam; e.LaHienTai = d.LaHienTai; Src(d.NguonTen, d.NguonUrl, e); },
        (d, _, _) => { var er = new KbErrors(); KbValidators.Nam(er, d.Nam); return Task.FromResult(er); },
        n => x => x.Nam == n);

    public static readonly KbEntityDefinition<Nganh, NganhDto> Nganh = new("nganh",
        e => new(e.LogicalId, e.MaNganh, e.Ten, e.TenGoiKhac, e.DonVi, e.MoTaNgan, e.ConTuyen, e.NguonTen, e.NguonUrl),
        (d, e) =>
        {
            e.MaNganh = d.MaNganh!.Trim(); e.Ten = d.Ten!.Trim(); e.TenGoiKhac = d.TenGoiKhac ?? [];
            e.DonVi = d.DonVi; e.MoTaNgan = d.MoTaNgan; e.ConTuyen = d.ConTuyen ?? true; Src(d.NguonTen, d.NguonUrl, e);
        },
        (d, _, _) =>
        {
            var er = new KbErrors();
            KbValidators.NotEmpty(er, d.MaNganh, "maNganh", "Mã ngành");
            KbValidators.NotEmpty(er, d.Ten, "ten", "Tên ngành");
            return Task.FromResult(er);
        });

    public static readonly KbEntityDefinition<PhuongThuc, PhuongThucDto> PhuongThuc = new("phuong-thuc",
        e => new(e.LogicalId, e.Ma, e.Ten, e.MoTa, e.ThangDiem, e.NguonTen, e.NguonUrl),
        (d, e) => { e.Ma = d.Ma!.Trim(); e.Ten = d.Ten!.Trim(); e.MoTa = d.MoTa; e.ThangDiem = d.ThangDiem ?? 30m; Src(d.NguonTen, d.NguonUrl, e); },
        (d, _, _) =>
        {
            var er = new KbErrors();
            KbValidators.NotEmpty(er, d.Ma, "ma", "Mã phương thức");
            KbValidators.NotEmpty(er, d.Ten, "ten", "Tên phương thức");
            KbValidators.Positive(er, d.ThangDiem ?? 30m, "thangDiem", "Thang điểm");
            return Task.FromResult(er);
        });

    public static readonly KbEntityDefinition<ToHop, ToHopDto> ToHop = new("to-hop",
        e => new(e.LogicalId, e.Ma, e.Mon, e.CachDoc, e.NguonTen, e.NguonUrl),
        (d, e) =>
        {
            e.Ma = d.Ma!.Trim(); e.Mon = d.Mon ?? [];
            e.CachDoc = string.IsNullOrWhiteSpace(d.CachDoc) ? ToHopReading.Generate(e.Ma) : d.CachDoc;
            Src(d.NguonTen, d.NguonUrl, e);
        },
        (d, _, _) => { var er = new KbErrors(); KbValidators.ToHopMa(er, d.Ma); return Task.FromResult(er); });

    public static readonly KbEntityDefinition<NganhToHop, NganhToHopDto> NganhToHop = new("nganh-to-hop",
        e => new(e.LogicalId, e.NganhLid, e.Nam, e.ToHopLid, e.NguonTen, e.NguonUrl),
        (d, e) => { e.NganhLid = d.NganhLid!.Value; e.Nam = d.Nam; e.ToHopLid = d.ToHopLid!.Value; Src(d.NguonTen, d.NguonUrl, e); },
        async (d, db, ct) =>
        {
            var er = new KbErrors();
            KbValidators.Nam(er, d.Nam);
            await KbValidators.RefAsync<Nganh>(er, db, d.NganhLid, "nganhLid", "Ngành", ct);
            await KbValidators.RefAsync<ToHop>(er, db, d.ToHopLid, "toHopLid", "Tổ hợp", ct);
            return er;
        },
        n => x => x.Nam == n);

    public static readonly KbEntityDefinition<DiemChuan, DiemChuanDto> DiemChuan = new("diem-chuan",
        e => new(e.LogicalId, e.NganhLid, e.Nam, e.PhuongThucLid, e.Diem, e.GhiChu, e.NguonTen, e.NguonUrl),
        (d, e) => { e.NganhLid = d.NganhLid!.Value; e.Nam = d.Nam; e.PhuongThucLid = d.PhuongThucLid!.Value; e.Diem = d.Diem; e.GhiChu = d.GhiChu; Src(d.NguonTen, d.NguonUrl, e); },
        async (d, db, ct) =>
        {
            var er = new KbErrors();
            KbValidators.Nam(er, d.Nam);
            await KbValidators.RefAsync<Nganh>(er, db, d.NganhLid, "nganhLid", "Ngành", ct);
            var pt = await KbValidators.RefAsync<PhuongThuc>(er, db, d.PhuongThucLid, "phuongThucLid", "Phương thức", ct);
            KbValidators.Diem(er, d.Diem, pt);
            return er;
        },
        n => x => x.Nam == n);

    public static readonly KbEntityDefinition<ChiTieu, ChiTieuDto> ChiTieu = new("chi-tieu",
        e => new(e.LogicalId, e.NganhLid, e.Nam, e.PhuongThucLid, e.SoLuong, e.NguonTen, e.NguonUrl),
        (d, e) => { e.NganhLid = d.NganhLid!.Value; e.Nam = d.Nam; e.PhuongThucLid = d.PhuongThucLid; e.SoLuong = d.SoLuong; Src(d.NguonTen, d.NguonUrl, e); },
        async (d, db, ct) =>
        {
            var er = new KbErrors();
            KbValidators.Nam(er, d.Nam);
            KbValidators.Positive(er, d.SoLuong, "soLuong", "Số lượng");
            await KbValidators.RefAsync<Nganh>(er, db, d.NganhLid, "nganhLid", "Ngành", ct);
            await KbValidators.RefAsync<PhuongThuc>(er, db, d.PhuongThucLid, "phuongThucLid", "Phương thức", ct, required: false);
            return er;
        },
        n => x => x.Nam == n);

    public static readonly KbEntityDefinition<HocPhi, HocPhiDto> HocPhi = new("hoc-phi",
        e => new(e.LogicalId, e.NganhLid, e.Nam, e.Muc, e.DonVi, e.GhiChu, e.NguonTen, e.NguonUrl),
        (d, e) => { e.NganhLid = d.NganhLid; e.Nam = d.Nam; e.Muc = d.Muc; e.DonVi = d.DonVi; e.GhiChu = d.GhiChu; Src(d.NguonTen, d.NguonUrl, e); },
        async (d, db, ct) =>
        {
            var er = new KbErrors();
            KbValidators.Nam(er, d.Nam);
            KbValidators.Positive(er, d.Muc, "muc", "Mức học phí");
            await KbValidators.RefAsync<Nganh>(er, db, d.NganhLid, "nganhLid", "Ngành", ct, required: false);
            return er;
        },
        n => x => x.Nam == n);

    public static readonly KbEntityDefinition<MocThoiGian, MocThoiGianDto> MocThoiGian = new("moc-thoi-gian",
        e => new(e.LogicalId, e.Nam, e.TenMoc, e.BatDau, e.KetThuc, e.PhuongThucLid, e.MoTa, e.NguonTen, e.NguonUrl),
        (d, e) => { e.Nam = d.Nam; e.TenMoc = d.TenMoc!.Trim(); e.BatDau = d.BatDau; e.KetThuc = d.KetThuc; e.PhuongThucLid = d.PhuongThucLid; e.MoTa = d.MoTa; Src(d.NguonTen, d.NguonUrl, e); },
        async (d, db, ct) =>
        {
            var er = new KbErrors();
            KbValidators.Nam(er, d.Nam);
            KbValidators.NotEmpty(er, d.TenMoc, "tenMoc", "Tên mốc");
            await KbValidators.RefAsync<PhuongThuc>(er, db, d.PhuongThucLid, "phuongThucLid", "Phương thức", ct, required: false);
            return er;
        },
        n => x => x.Nam == n);

    public static readonly KbEntityDefinition<Faq, FaqDto> Faq = new("faq",
        e => new(e.LogicalId, e.CauHoi, e.CachHoiKhac, e.TraLoi, e.ChuDe, e.NguonTen, e.NguonUrl),
        (d, e) => { e.CauHoi = d.CauHoi!.Trim(); e.CachHoiKhac = d.CachHoiKhac ?? []; e.TraLoi = d.TraLoi!.Trim(); e.ChuDe = d.ChuDe; Src(d.NguonTen, d.NguonUrl, e); },
        (d, _, _) =>
        {
            var er = new KbErrors();
            KbValidators.NotEmpty(er, d.CauHoi, "cauHoi", "Câu hỏi");
            KbValidators.NotEmpty(er, d.TraLoi, "traLoi", "Câu trả lời");
            return Task.FromResult(er);
        });

    /// <summary>10 loại tri thức có CRUD bản nháp.</summary>
    public static readonly IReadOnlyList<IKbEntityDefinition> All =
        [MuaTuyenSinh, Nganh, PhuongThuc, ToHop, NganhToHop, DiemChuan, ChiTieu, HocPhi, MocThoiGian, Faq];

    public static KbEntityDefinition<TEntity, TDto> Get<TEntity, TDto>(string slug)
        where TEntity : KbEntity, new() where TDto : class =>
        (KbEntityDefinition<TEntity, TDto>)All.Single(d => d.Slug == slug);

    private static void Src(string? ten, string? url, KbEntity e) { e.NguonTen = ten; e.NguonUrl = url; }
}
