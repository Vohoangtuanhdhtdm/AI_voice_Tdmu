using Tdmu.Advisor.Api.Features.KnowledgeBase.Import;
using Tdmu.Advisor.Api.Features.KnowledgeBase.Releases;
using Microsoft.AspNetCore.Identity;
using Tdmu.Advisor.Api.Features.KnowledgeBase.Crud;
using Tdmu.Advisor.Api.Infrastructure.Http;

namespace Tdmu.Advisor.Api.Infrastructure.Auth;

public record LoginRequest(string? Email, string? Password);

public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/auth/login", async (
            LoginRequest req,
            UserManager<IdentityUser> users,
            SignInManager<IdentityUser> signIn,
            JwtTokenService tokens) =>
        {
            if (string.IsNullOrWhiteSpace(req.Email) || string.IsNullOrEmpty(req.Password))
                return ProblemResults.Unauthorized();

            var user = await users.FindByEmailAsync(req.Email);
            if (user is null) return ProblemResults.Unauthorized();

            var result = await signIn.CheckPasswordSignInAsync(user, req.Password, lockoutOnFailure: true);
            if (result.IsLockedOut) return ProblemResults.Locked();
            if (!result.Succeeded) return ProblemResults.Unauthorized();

            var (token, expiresAt) = tokens.Create(user, await users.GetRolesAsync(user));
            return Results.Ok(new { accessToken = token, expiresAt });
        });

        var admin = app.MapGroup("/api/admin").RequireAuthorization();
        admin.MapKbEndpoints();
        admin.MapReleaseEndpoints();
        admin.MapImportEndpoints();
        admin.MapGet("/ping", () => Results.Ok(new { status = "ok" }));
    }
}
