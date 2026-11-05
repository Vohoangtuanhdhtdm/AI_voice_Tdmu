using Microsoft.EntityFrameworkCore;
using Tdmu.Advisor.Api.Features.KnowledgeBase.Entities;
using Tdmu.Advisor.Api.Infrastructure;

namespace Tdmu.Advisor.Api.Features.KnowledgeBase.Crud;

/// <summary>Tìm các bản ghi nháp còn tham chiếu tới một thực thể (chặn xóa).</summary>
public static class KbReferenceChecker
{
    public static async Task<List<KbEntity>> ReferrersAsync(AdvisorDbContext db, KbEntity target, CancellationToken ct)
    {
        var lid = target.LogicalId;
        var found = new List<KbEntity>();
        switch (target)
        {
            case Nganh:
                found.AddRange(await db.NganhToHop.Where(x => x.ReleaseId == null && x.NganhLid == lid).ToListAsync(ct));
                found.AddRange(await db.DiemChuan.Where(x => x.ReleaseId == null && x.NganhLid == lid).ToListAsync(ct));
                found.AddRange(await db.ChiTieu.Where(x => x.ReleaseId == null && x.NganhLid == lid).ToListAsync(ct));
                found.AddRange(await db.HocPhi.Where(x => x.ReleaseId == null && x.NganhLid == lid).ToListAsync(ct));
                break;
            case PhuongThuc:
                found.AddRange(await db.DiemChuan.Where(x => x.ReleaseId == null && x.PhuongThucLid == lid).ToListAsync(ct));
                found.AddRange(await db.ChiTieu.Where(x => x.ReleaseId == null && x.PhuongThucLid == lid).ToListAsync(ct));
                found.AddRange(await db.MocThoiGian.Where(x => x.ReleaseId == null && x.PhuongThucLid == lid).ToListAsync(ct));
                break;
            case ToHop:
                found.AddRange(await db.NganhToHop.Where(x => x.ReleaseId == null && x.ToHopLid == lid).ToListAsync(ct));
                break;
        }
        return found;
    }
}
