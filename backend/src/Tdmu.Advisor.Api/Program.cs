using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Tdmu.Advisor.Api.Infrastructure;
using Tdmu.Advisor.Api.Infrastructure.Auth;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AdvisorDbContext>(o => o
    .UseNpgsql(builder.Configuration.GetConnectionString("Default"), npgsql => npgsql.UseVector())
    .UseSnakeCaseNamingConvention());

builder.Services.Configure<AuthOptions>(builder.Configuration.GetSection(AuthOptions.Section));
var auth = builder.Configuration.GetSection(AuthOptions.Section).Get<AuthOptions>() ?? new AuthOptions();
if (auth.JwtKey.Length < 32)
    throw new InvalidOperationException("Auth:JwtKey phải có tối thiểu 32 ký tự (đặt qua biến môi trường hoặc user-secrets).");

builder.Services.AddIdentityCore<IdentityUser>(o =>
    {
        o.Lockout.MaxFailedAccessAttempts = 5;
        o.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
        o.Lockout.AllowedForNewUsers = true;
    })
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<AdvisorDbContext>()
    .AddSignInManager();
builder.Services.AddSingleton<JwtTokenService>();
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
        o.MapInboundClaims = false;
        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidIssuer = auth.Issuer,
            ValidAudience = auth.Audience,
            IssuerSigningKey = JwtTokenService.SigningKey(auth.JwtKey),
            ClockSkew = TimeSpan.FromSeconds(30),
            RoleClaimType = System.Security.Claims.ClaimTypes.Role,
            NameClaimType = "sub",
        };
    });
builder.Services.AddAuthorizationBuilder()
    .SetDefaultPolicy(new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder()
        .AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme)
        .RequireRole(AdminSeeder.AdminRole)
        .Build());

var app = builder.Build();

if (app.Environment.IsDevelopment() || app.Environment.IsEnvironment("Testing"))
{
    using var scope = app.Services.CreateScope();
    await scope.ServiceProvider.GetRequiredService<AdvisorDbContext>().Database.MigrateAsync();
}
await AdminSeeder.SeedAsync(app.Services.CreateScope().ServiceProvider);

app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.MapAuthEndpoints();

app.Run();

public partial class Program;
