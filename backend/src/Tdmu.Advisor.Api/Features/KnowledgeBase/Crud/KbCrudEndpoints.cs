using Microsoft.EntityFrameworkCore;
using Npgsql;
using Tdmu.Advisor.Api.Features.KnowledgeBase.Entities;
using Tdmu.Advisor.Api.Infrastructure;
using Tdmu.Advisor.Api.Infrastructure.Http;

namespace Tdmu.Advisor.Api.Features.KnowledgeBase.Crud;

public static class KbCrudEndpoints
{
    /// <summary>Mount CRUD của mọi loại tri thức dưới <c>/kb/{slug}</c> của group admin.</summary>
    public static void MapKbEndpoints(this RouteGroupBuilder admin)
    {
        var kb = admin.MapGroup("/kb");
        foreach (var def in KbEntityDefinitions.All) def.Map(kb);
    }

    public static RouteGroupBuilder MapKbCrud<TEntity, TDto>(this RouteGroupBuilder group, string slug)
        where TEntity : KbEntity, new() where TDto : class
    {
        MapCrud(group, KbEntityDefinitions.Get<TEntity, TDto>(slug));
        return group;
    }

    internal static void MapCrud<TEntity, TDto>(RouteGroupBuilder group, KbEntityDefinition<TEntity, TDto> def)
        where TEntity : KbEntity, new() where TDto : class
    {
        var r = group.MapGroup("/" + def.Slug);

        r.MapGet("", async (AdvisorDbContext db, int? nam, CancellationToken ct) =>
        {
            IQueryable<TEntity> q = db.Set<TEntity>().AsNoTracking().Where(x => x.ReleaseId == null);
            if (nam is { } n && def.NamFilter is { } f) q = q.Where(f(n));
            var rows = await q.OrderBy(x => x.CapNhatLuc).ToListAsync(ct);
            return Results.Ok(rows.Select(def.ToDto));
        });

        r.MapGet("/{logicalId:guid}", async (Guid logicalId, AdvisorDbContext db, CancellationToken ct) =>
        {
            var e = await db.Set<TEntity>().AsNoTracking().FirstOrDefaultAsync(x => x.ReleaseId == null && x.LogicalId == logicalId, ct);
            return e is null ? ProblemResults.NotFound() : Results.Ok(def.ToDto(e));
        });

        r.MapPost("", async (TDto dto, AdvisorDbContext db, CancellationToken ct) =>
        {
            var errors = await def.Validate(dto, null, db, ct);
            if (errors.Any) return ProblemResults.Invalid(errors.ToDictionary());
            var e = new TEntity();
            return await SaveAsync(db, def, dto, e, isNew: true, ct,
                () => Results.Created($"/api/admin/kb/{def.Slug}/{e.LogicalId}", def.ToDto(e)));
        });

        r.MapPut("/{logicalId:guid}", async (Guid logicalId, TDto dto, AdvisorDbContext db, CancellationToken ct) =>
        {
            var e = await db.Set<TEntity>().FirstOrDefaultAsync(x => x.ReleaseId == null && x.LogicalId == logicalId, ct);
            if (e is null) return ProblemResults.NotFound();
            var errors = await def.Validate(dto, e, db, ct);
            if (errors.Any) return ProblemResults.Invalid(errors.ToDictionary());
            return await SaveAsync(db, def, dto, e, isNew: false, ct, () => Results.Ok(def.ToDto(e)));
        });

        r.MapDelete("/{logicalId:guid}", async (Guid logicalId, AdvisorDbContext db, CancellationToken ct) =>
        {
            var e = await db.Set<TEntity>().FirstOrDefaultAsync(x => x.ReleaseId == null && x.LogicalId == logicalId, ct);
            if (e is null) return ProblemResults.NotFound();
            var refs = await KbReferenceChecker.ReferrersAsync(db, e, ct);
            if (refs.Count > 0)
            {
                var lookup = await KbLabelLookupFactory.BuildAsync(db, null, ct);
                return ProblemResults.Conflict("Bản ghi đang được tham chiếu nên không thể xóa",
                    new Dictionary<string, object?> { ["thamChieu"] = refs.Select(x => x.Label(lookup)).ToArray() });
            }
            db.Remove(e);
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        });
    }

    private static async Task<IResult> SaveAsync<TEntity, TDto>(
        AdvisorDbContext db, KbEntityDefinition<TEntity, TDto> def, TDto dto, TEntity e, bool isNew,
        CancellationToken ct, Func<IResult> ok)
        where TEntity : KbEntity, new() where TDto : class
    {
        try
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            // Chỉ một mùa được là "hiện tại": bỏ cờ ở các mùa khác trước (cùng transaction) để không vi phạm unique index.
            if (e is MuaTuyenSinh && dto is MuaTuyenSinhDto { LaHienTai: true })
            {
                var others = await db.MuaTuyenSinh.Where(x => x.ReleaseId == null && x.LaHienTai && x.LogicalId != e.LogicalId).ToListAsync(ct);
                foreach (var o in others) { o.LaHienTai = false; o.CapNhatLuc = DateTimeOffset.UtcNow; }
                if (others.Count > 0) await db.SaveChangesAsync(ct);
            }
            def.Apply(dto, e);
            e.CapNhatLuc = DateTimeOffset.UtcNow;
            if (isNew) db.Add(e);
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return ok();
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            return ProblemResults.Conflict("Bản ghi đã tồn tại");
        }
    }
}
