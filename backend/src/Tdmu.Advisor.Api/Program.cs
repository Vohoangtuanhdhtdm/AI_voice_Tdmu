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
using Tdmu.Advisor.Api.Infrastructure.Gemini;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, HttpCurrentUser>();
builder.Services.AddScoped<AuditSuppression>();
builder.Services.AddScoped<AuditInterceptor>();
builder.Services.AddScoped<Tdmu.Advisor.Api.Features.KnowledgeBase.Releases.ReleaseService>();
builder.Services.AddScoped<IKnowledgeQuery, KnowledgeQuery>();
builder.Services.AddScoped<Tdmu.Advisor.Api.Features.Conversations.ConversationStore>();
builder.Services.AddScoped<Tdmu.Advisor.Api.Features.KnowledgeBase.Import.ImportService>();
builder.Services.AddSingleton<Tdmu.Advisor.Api.Infrastructure.Storage.IFileStore, Tdmu.Advisor.Api.Infrastructure.Storage.LocalFileStore>();
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

builder.Services.Configure<GeminiOptions>(builder.Configuration.GetSection(GeminiOptions.Section));
builder.Services.Configure<EmbeddingOptions>(builder.Configuration.GetSection(EmbeddingOptions.Section));
builder.Services.Configure<AgentOptions>(builder.Configuration.GetSection(AgentOptions.Section));
builder.Services.Configure<RagOptions>(builder.Configuration.GetSection(RagOptions.Section));
builder.Services.AddScoped<Tdmu.Advisor.Api.Features.Ingestion.TextExtractor>();
builder.Services.AddScoped<Tdmu.Advisor.Api.Features.Ingestion.IngestionService>();
if (builder.Configuration.GetValue<bool?>("Ingestion:Enabled") ?? true)
    builder.Services.AddHostedService<Tdmu.Advisor.Api.Features.Ingestion.IngestionWorker>();
builder.Services.AddHttpClient<IGeminiClient, GeminiRestClient>(c => c.Timeout = TimeSpan.FromSeconds(60));

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

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(o =>
{
    o.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo { Title = "TDMU Advisor API", Version = "v1" });
    o.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        Description = "Dán access token lấy từ POST /api/auth/login.",
    });
    o.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        [new Microsoft.OpenApi.Models.OpenApiSecurityScheme
        {
            Reference = new Microsoft.OpenApi.Models.OpenApiReference { Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme, Id = "Bearer" },
        }] = Array.Empty<string>(),
    });
});

var app = builder.Build();

if (app.Environment.IsDevelopment() || app.Environment.IsEnvironment("Testing"))
{
    using var scope = app.Services.CreateScope();
    await scope.ServiceProvider.GetRequiredService<AdvisorDbContext>().Database.MigrateAsync();
}
else if (!EF.IsDesignTime) // `dotnet ef` dừng host ngay sau Build(); kiểm tra thêm cho chắc chắn không chạm DB lúc design-time.
{
    // Staging/Production không tự migrate: thiếu migration thì dừng ngay với thông báo rõ ràng (xem backend/README.md mục 6),
    // thay vì lỗi khó hiểu "relation ... does not exist" trong AdminSeeder.
    using var scope = app.Services.CreateScope();
    var pending = (await scope.ServiceProvider.GetRequiredService<AdvisorDbContext>().Database.GetPendingMigrationsAsync()).ToList();
    if (pending.Count > 0)
    {
        app.Logger.LogCritical(
            "CSDL chưa được migrate ({Count} migration chưa áp: {Pending}). Hãy chạy `dotnet ef database update` hoặc migration bundle trước khi khởi động (xem backend/README.md). " +
            "Database has pending migrations; apply them before starting the API.",
            pending.Count, string.Join(", ", pending));
        Environment.ExitCode = 1;
        return;
    }
}
using (var seedScope = app.Services.CreateScope())
{
    await AdminSeeder.SeedAsync(seedScope.ServiceProvider);
    if (app.Environment.IsDevelopment() && app.Configuration.GetValue<bool>("Seed:SampleData"))
        await DevSampleSeeder.SeedAsync(seedScope.ServiceProvider.GetRequiredService<AdvisorDbContext>());
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.MapAuthEndpoints();
app.MapPublicEndpoints();

app.Run();

public partial class Program;
