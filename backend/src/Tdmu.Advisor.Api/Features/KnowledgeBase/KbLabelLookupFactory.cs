using Microsoft.EntityFrameworkCore;
using Tdmu.Advisor.Api.Features.KnowledgeBase.Entities;
using Tdmu.Advisor.Api.Infrastructure;

namespace Tdmu.Advisor.Api.Features.KnowledgeBase;

/// <summary>Dựng <see cref="IKbLabelLookup"/> từ một scope (bản nháp khi releaseId = null, hoặc một release).</summary>
public static class KbLabelLookupFactory
{
    public static async Task<IKbLabelLookup> BuildAsync(AdvisorDbContext db, Guid? releaseId, CancellationToken ct)
    {
        var nganh = await db.Nganh.AsNoTracking().Where(x => x.ReleaseId == releaseId).ToDictionaryAsync(x => x.LogicalId, x => x.Ten, ct);
        var pt = await db.PhuongThuc.AsNoTracking().Where(x => x.ReleaseId == releaseId).ToDictionaryAsync(x => x.LogicalId, x => x.Ten, ct);
        var th = await db.ToHop.AsNoTracking().Where(x => x.ReleaseId == releaseId).ToDictionaryAsync(x => x.LogicalId, x => x.Ma, ct);
        var tl = await db.TaiLieu.AsNoTracking().Where(x => x.ReleaseId == releaseId).ToDictionaryAsync(x => x.LogicalId, x => x.TieuDe, ct);
        return new DictionaryKbLabelLookup(nganh, pt, th, tl);
    }
}
