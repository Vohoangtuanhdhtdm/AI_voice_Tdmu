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
}
