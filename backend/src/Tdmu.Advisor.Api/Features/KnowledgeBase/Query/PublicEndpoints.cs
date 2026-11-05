namespace Tdmu.Advisor.Api.Features.KnowledgeBase.Query;

public static class PublicEndpoints
{
    public const string RateLimitPolicy = "public";

    public static void MapPublicEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/public").RequireRateLimiting(RateLimitPolicy);

        g.MapGet("/nganh", async (IKnowledgeQuery q, CancellationToken ct) =>
        {
            try { return Results.Ok(await q.ListNganhAsync(KbScope.Published, ct)); }
            catch (NoActiveReleaseException) { return Results.Ok(Array.Empty<NganhView>()); }
        });

        g.MapGet("/nganh/{maNganh}", async (string maNganh, IKnowledgeQuery q, CancellationToken ct) =>
        {
            try
            {
                var d = await q.GetNganhAsync(KbScope.Published, maNganh, ct);
                return d is null ? Results.NotFound() : Results.Ok(d);
            }
            catch (NoActiveReleaseException) { return Results.NotFound(); }
        });
    }
}
