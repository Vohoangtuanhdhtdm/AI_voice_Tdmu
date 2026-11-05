using Microsoft.EntityFrameworkCore;
using Tdmu.Advisor.Api.Infrastructure;
using Tdmu.Advisor.Api.Infrastructure.Auditing;
using Tdmu.Advisor.Api.Infrastructure.Http;

namespace Tdmu.Advisor.Api.Features.KnowledgeBase.Releases;

public record PublishRequest(string? GhiChu);

public static class ReleaseEndpoints
{
    public static void MapReleaseEndpoints(this RouteGroupBuilder admin)
    {
        var r = admin.MapGroup("/releases");

        r.MapGet("", async (AdvisorDbContext db, CancellationToken ct) =>
            Results.Ok(await db.Release.AsNoTracking().OrderByDescending(x => x.SoHieu).ToListAsync(ct)));

        r.MapGet("/diff", async (ReleaseService svc, CancellationToken ct) => Results.Ok(await svc.DiffAsync(ct)));

        r.MapPost("", async (PublishRequest? req, ReleaseService svc, ICurrentUser user, CancellationToken ct) =>
        {
            try
            {
                var release = await svc.PublishAsync(req?.GhiChu ?? "", user.UserId, ct);
                return Results.Created($"/api/admin/releases/{release.Id}", release);
            }
            catch (NoChangesException e) { return ProblemResults.Conflict(e.Message); }
        });

        r.MapPost("/{id:guid}/activate", async (Guid id, ReleaseService svc, ICurrentUser user, CancellationToken ct) =>
        {
            try { await svc.ActivateAsync(id, user.UserId, ct); return Results.NoContent(); }
            catch (ReleaseNotFoundException e) { return ProblemResults.NotFound(e.Message); }
        });

        r.MapPost("/{id:guid}/restore-to-draft", async (Guid id, ReleaseService svc, ICurrentUser user, CancellationToken ct) =>
        {
            try { await svc.RestoreToDraftAsync(id, user.UserId, ct); return Results.NoContent(); }
            catch (ReleaseNotFoundException e) { return ProblemResults.NotFound(e.Message); }
        });
    }
}
