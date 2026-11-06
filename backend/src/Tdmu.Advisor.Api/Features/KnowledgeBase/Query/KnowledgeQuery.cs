using Microsoft.EntityFrameworkCore;
using Tdmu.Advisor.Api.Infrastructure;

namespace Tdmu.Advisor.Api.Features.KnowledgeBase.Query;

public class KnowledgeQuery(AdvisorDbContext db) : IKnowledgeQuery
{
    public async Task<Guid?> ResolveReleaseAsync(KbScope scope, CancellationToken ct)
    {
        if (scope == KbScope.Draft) return null;
        var id = await db.Release.Where(r => r.IsActive).Select(r => (Guid?)r.Id).FirstOrDefaultAsync(ct);
        return id ?? throw new NoActiveReleaseException();
    }

    public async Task<int?> CurrentYearAsync(KbScope scope, CancellationToken ct)
    {
        var rid = await ResolveReleaseAsync(scope, ct);
        return await db.MuaTuyenSinh.AsNoTracking().InScope(rid).Where(m => m.LaHienTai)
            .Select(m => (int?)m.Nam).FirstOrDefaultAsync(ct);
    }

    public async Task<IReadOnlyList<NganhView>> ListNganhAsync(KbScope scope, CancellationToken ct)
    {
        var rid = await ResolveReleaseAsync(scope, ct);
        return await db.Nganh.AsNoTracking().InScope(rid).OrderBy(n => n.MaNganh)
            .Select(n => new NganhView(n.MaNganh, n.Ten, n.DonVi, n.MoTaNgan, n.ConTuyen)).ToListAsync(ct);
    }

    public async Task<NganhDetailView?> GetNganhAsync(KbScope scope, string maNganh, CancellationToken ct)
    {
        var rid = await ResolveReleaseAsync(scope, ct);
        var n = await db.Nganh.AsNoTracking().InScope(rid).FirstOrDefaultAsync(x => x.MaNganh == maNganh, ct);
        if (n is null) return null;
        var view = new NganhView(n.MaNganh, n.Ten, n.DonVi, n.MoTaNgan, n.ConTuyen);
        var nam = await db.MuaTuyenSinh.AsNoTracking().InScope(rid).Where(m => m.LaHienTai)
            .Select(m => (int?)m.Nam).FirstOrDefaultAsync(ct);
        if (nam is null) return new(view, null, [], [], [], []);

        var toHop = await (from nt in db.NganhToHop.AsNoTracking().InScope(rid)
                           join t in db.ToHop.AsNoTracking().InScope(rid) on nt.ToHopLid equals t.LogicalId
                           where nt.NganhLid == n.LogicalId && nt.Nam == nam
                           orderby t.Ma
                           select new ToHopView(t.Ma, t.Mon, t.CachDoc, nt.NguonTen ?? t.NguonTen, nt.NguonUrl ?? t.NguonUrl)).ToListAsync(ct);

        var diem = await (from d in db.DiemChuan.AsNoTracking().InScope(rid)
                          join p in db.PhuongThuc.AsNoTracking().InScope(rid) on d.PhuongThucLid equals p.LogicalId
                          where d.NganhLid == n.LogicalId && d.Nam == nam
                          orderby p.Ma
                          select new DiemChuanView(p.Ma, p.Ten, p.ThangDiem, d.Diem, d.GhiChu, d.NguonTen, d.NguonUrl)).ToListAsync(ct);

        var chiTieu = await (from c in db.ChiTieu.AsNoTracking().InScope(rid)
                             join p in db.PhuongThuc.AsNoTracking().InScope(rid) on c.PhuongThucLid equals p.LogicalId into pj
                             from p in pj.DefaultIfEmpty()
                             where c.NganhLid == n.LogicalId && c.Nam == nam
                             orderby p.Ma
                             select new ChiTieuView(p.Ten, c.SoLuong, c.NguonTen, c.NguonUrl)).ToListAsync(ct);

        var hocPhi = await db.HocPhi.AsNoTracking().InScope(rid)
            .Where(h => h.Nam == nam && (h.NganhLid == n.LogicalId || h.NganhLid == null))
            .OrderBy(h => h.NganhLid == null).ThenBy(h => h.Muc)
            .Select(h => new HocPhiView(h.Muc, h.DonVi, h.GhiChu, h.NganhLid == null, h.NguonTen, h.NguonUrl)).ToListAsync(ct);

        return new(view, nam, toHop, diem, chiTieu, hocPhi);
    }

    private sealed record NganhCandidate(Guid LogicalId, string MaNganh, string Ten, bool ConTuyen, bool Exact, bool Contains, double Sim);

    private const double MinSimilarity = 0.3, SimilarityBand = 0.15;
    private const int MaxContains = 10, MaxSimilar = 5;

    public async Task<IReadOnlyList<NganhMatch>> FindNganhAsync(KbScope scope, string tuKhoa, CancellationToken ct)
    {
        var q = string.Join(' ', tuKhoa.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        if (q.Length == 0) return [];
        var rid = await ResolveReleaseAsync(scope, ct);
        // strpos thay cho LIKE để ký tự % và _ trong từ khóa không thành ký tự đại diện.
        var rows = await db.Database.SqlQuery<NganhCandidate>($"""
            WITH p AS (SELECT unaccent(lower({q})) AS uq, lower({q}) AS lq)
            SELECT * FROM (
                SELECT n.logical_id, n.ma_nganh, n.ten, n.con_tuyen,
                    (lower(n.ma_nganh) = p.lq OR unaccent(lower(n.ten)) = p.uq
                        OR EXISTS (SELECT 1 FROM unnest(n.ten_goi_khac) a WHERE unaccent(lower(a)) = p.uq)) AS exact,
                    (strpos(unaccent(lower(n.ten)), p.uq) > 0
                        OR EXISTS (SELECT 1 FROM unnest(n.ten_goi_khac) a WHERE strpos(unaccent(lower(a)), p.uq) > 0)) AS contains,
                    similarity(unaccent(lower(n.ten)), p.uq)::float8 AS sim
                FROM nganh n, p
                WHERE n.release_id IS NOT DISTINCT FROM {rid}::uuid) c
            WHERE exact OR contains OR sim >= {MinSimilarity}
            ORDER BY exact DESC, contains DESC, sim DESC, ma_nganh
            """).ToListAsync(ct);

        static Func<NganhCandidate, NganhMatch> M(NganhMatchKind kind) => c => new(c.LogicalId, c.MaNganh, c.Ten, c.ConTuyen, kind);
        if (rows.Any(r => r.Exact)) return rows.Where(r => r.Exact).Select(M(NganhMatchKind.Exact)).ToList();
        if (rows.Any(r => r.Contains)) return rows.Where(r => r.Contains).Take(MaxContains).Select(M(NganhMatchKind.Contains)).ToList();
        if (rows.Count == 0) return [];
        // Gần đúng: chỉ giữ các ngành gần bằng ngành giống nhất, để một lỗi gõ không bị ngành na ná làm thành câu hỏi lại.
        var best = rows[0].Sim;
        return rows.Where(r => r.Sim >= best - SimilarityBand).Take(MaxSimilar).Select(M(NganhMatchKind.Similar)).ToList();
    }

    public async Task<IReadOnlyList<PhuongThucRef>> ListPhuongThucAsync(KbScope scope, CancellationToken ct)
    {
        var rid = await ResolveReleaseAsync(scope, ct);
        return await db.PhuongThuc.AsNoTracking().InScope(rid).OrderBy(p => p.Ma)
            .Select(p => new PhuongThucRef(p.LogicalId, p.Ma, p.Ten, p.ThangDiem)).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<DiemChuanView>> GetDiemChuanAsync(KbScope scope, Guid nganhLid, int nam, Guid? phuongThucLid, CancellationToken ct)
    {
        var rid = await ResolveReleaseAsync(scope, ct);
        return await (from d in db.DiemChuan.AsNoTracking().InScope(rid)
                      join p in db.PhuongThuc.AsNoTracking().InScope(rid) on d.PhuongThucLid equals p.LogicalId
                      where d.NganhLid == nganhLid && d.Nam == nam && (phuongThucLid == null || d.PhuongThucLid == phuongThucLid)
                      orderby p.Ma
                      select new DiemChuanView(p.Ma, p.Ten, p.ThangDiem, d.Diem, d.GhiChu, d.NguonTen, d.NguonUrl)).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<ChiTieuRow>> GetChiTieuAsync(KbScope scope, Guid nganhLid, int nam, Guid? phuongThucLid, CancellationToken ct)
    {
        var rid = await ResolveReleaseAsync(scope, ct);
        return await (from c in db.ChiTieu.AsNoTracking().InScope(rid)
                      join p in db.PhuongThuc.AsNoTracking().InScope(rid) on c.PhuongThucLid equals p.LogicalId into pj
                      from p in pj.DefaultIfEmpty()
                      where c.NganhLid == nganhLid && c.Nam == nam && (phuongThucLid == null || c.PhuongThucLid == phuongThucLid)
                      orderby p.Ma
                      select new ChiTieuRow(p.Ma, p.Ten, c.SoLuong, c.NguonTen, c.NguonUrl)).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<HocPhiView>> GetHocPhiAsync(KbScope scope, Guid? nganhLid, int nam, CancellationToken ct)
    {
        var rid = await ResolveReleaseAsync(scope, ct);
        return await db.HocPhi.AsNoTracking().InScope(rid)
            .Where(h => h.Nam == nam && (h.NganhLid == null || (nganhLid != null && h.NganhLid == nganhLid)))
            .OrderBy(h => h.NganhLid == null).ThenBy(h => h.Muc)
            .Select(h => new HocPhiView(h.Muc, h.DonVi, h.GhiChu, h.NganhLid == null, h.NguonTen, h.NguonUrl)).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<MocThoiGianView>> GetMocThoiGianAsync(KbScope scope, int nam, Guid? phuongThucLid, CancellationToken ct)
    {
        var rid = await ResolveReleaseAsync(scope, ct);
        return await (from m in db.MocThoiGian.AsNoTracking().InScope(rid)
                      join p in db.PhuongThuc.AsNoTracking().InScope(rid) on m.PhuongThucLid equals p.LogicalId into pj
                      from p in pj.DefaultIfEmpty()
                      where m.Nam == nam && (phuongThucLid == null || m.PhuongThucLid == null || m.PhuongThucLid == phuongThucLid)
                      orderby m.BatDau, m.TenMoc
                      select new MocThoiGianView(m.TenMoc, m.BatDau, m.KetThuc, p.Ten, m.MoTa, m.NguonTen, m.NguonUrl)).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<ToHopView>> GetToHopOfNganhAsync(KbScope scope, Guid nganhLid, int nam, CancellationToken ct)
    {
        var rid = await ResolveReleaseAsync(scope, ct);
        return await (from nt in db.NganhToHop.AsNoTracking().InScope(rid)
                      join t in db.ToHop.AsNoTracking().InScope(rid) on nt.ToHopLid equals t.LogicalId
                      where nt.NganhLid == nganhLid && nt.Nam == nam
                      orderby t.Ma
                      select new ToHopView(t.Ma, t.Mon, t.CachDoc, nt.NguonTen ?? t.NguonTen, nt.NguonUrl ?? t.NguonUrl)).ToListAsync(ct);
    }

    public async Task<ToHopRef?> FindToHopAsync(KbScope scope, string ma, CancellationToken ct)
    {
        var rid = await ResolveReleaseAsync(scope, ct);
        var key = ma.Replace(" ", "").ToUpperInvariant();
        return await db.ToHop.AsNoTracking().InScope(rid).Where(t => t.Ma.ToUpper() == key)
            .Select(t => new ToHopRef(t.LogicalId, t.Ma, t.Mon, t.CachDoc, t.NguonTen, t.NguonUrl)).FirstOrDefaultAsync(ct);
    }

    public async Task<IReadOnlyList<NganhOfToHopView>> GetNganhOfToHopAsync(KbScope scope, Guid toHopLid, int nam, CancellationToken ct)
    {
        var rid = await ResolveReleaseAsync(scope, ct);
        return await (from nt in db.NganhToHop.AsNoTracking().InScope(rid)
                      join n in db.Nganh.AsNoTracking().InScope(rid) on nt.NganhLid equals n.LogicalId
                      where nt.ToHopLid == toHopLid && nt.Nam == nam
                      orderby n.MaNganh
                      select new NganhOfToHopView(n.MaNganh, n.Ten, n.ConTuyen, nt.NguonTen ?? n.NguonTen, nt.NguonUrl ?? n.NguonUrl)).ToListAsync(ct);
    }
}
