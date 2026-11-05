using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Npgsql;
using Tdmu.Advisor.Api.Infrastructure;
using Tdmu.Advisor.Api.Infrastructure.Auditing;
using System.Threading.RateLimiting;
using Tdmu.Advisor.Api.Features.KnowledgeBase.Query;
using Tdmu.Advisor.Api.Features.KnowledgeBase.Seed;
using Tdmu.Advisor.Api.Infrastructure.Auth;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, HttpCurrentUser>();
builder.Services.AddScoped<AuditSuppression>();
builder.Services.AddScoped<AuditInterceptor>();
builder.Services.AddScoped<Tdmu.Advisor.Api.Features.KnowledgeBase.Releases.ReleaseService>();
builder.Services.AddScoped<IKnowledgeQuery, KnowledgeQuery>();
var permits = builder.Configuration.GetValue<int?>("RateLimit:PublicPermitsPerMinute") ?? 60;
builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    o.AddPolicy(PublicEndpoints.RateLimitPolicy, ctx => RateLimitPartition.GetFixedWindowLimiter(
        ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = permits, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
});
var dataSourceBuilder = new NpgsqlDataSourceBuilder(builder.Configuration.GetConnectionString("Default"));
dataSourceBuilder.UseVector();
var dataSource = dataSourceBuilder.Build();
builder.Services.AddSingleton(dataSource);
builder.Services.AddDbContext<AdvisorDbContext>((sp, o) => o
    .AddInterceptors(sp.GetRequiredService<AuditInterceptor>())
    .UseNpgsql(dataSource, npgsql => npgsql.UseVector())
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
using (var seedScope = app.Services.CreateScope())
{
    await AdminSeeder.SeedAsync(seedScope.ServiceProvider);
    if (app.Environment.IsDevelopment() && app.Configuration.GetValue<bool>("Seed:SampleData"))
        await DevSampleSeeder.SeedAsync(seedScope.ServiceProvider.GetRequiredService<AdvisorDbContext>());
}

app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.MapAuthEndpoints();
app.MapPublicEndpoints();

app.Run();

public partial class Program;
