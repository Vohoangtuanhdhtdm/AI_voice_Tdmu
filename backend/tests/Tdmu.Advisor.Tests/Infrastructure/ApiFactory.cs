using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Npgsql;
using Respawn;
using Respawn.Graph;
using Testcontainers.PostgreSql;

namespace Tdmu.Advisor.Tests.Infrastructure;

public class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _db = new PostgreSqlBuilder("pgvector/pgvector:pg16").Build();

    public const string TestJwtKey = "test-only-jwt-signing-key-0123456789-abcdef";
    public const string AdminEmail = "admin@test.local";
    public const string AdminPassword = "Admin#Test123";

    private Respawner? _respawner;

    public async Task InitializeAsync() => await _db.StartAsync();

    async Task IAsyncLifetime.DisposeAsync()
    {
        await base.DisposeAsync();
        await _db.DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Default", _db.GetConnectionString());
        builder.UseSetting("Auth:JwtKey", TestJwtKey);
        builder.UseSetting("Auth:SeedAdminEmail", AdminEmail);
        builder.UseSetting("Auth:SeedAdminPassword", AdminPassword);
        builder.UseSetting("RateLimit:PublicPermitsPerMinute", "100000");
    }

    /// <summary>Xóa dữ liệu mọi bảng (trừ lịch sử migration và bảng Identity). Respawner tạo lười vì ở Task 1 chưa có bảng nào.</summary>
    public async Task ResetDatabaseAsync()
    {
        await using var conn = new NpgsqlConnection(_db.GetConnectionString());
        await conn.OpenAsync();
        if (_respawner is null)
        {
            var tables = await GetTablesToIgnoreAsync(conn);
            if (!await HasResettableTablesAsync(conn, tables)) return; // Chưa có bảng nào để xóa; thử lại ở lần gọi sau.
            _respawner = await Respawner.CreateAsync(conn, new RespawnerOptions
            {
                DbAdapter = DbAdapter.Postgres,
                SchemasToInclude = ["public"],
                TablesToIgnore = tables,
            });
        }
        await _respawner.ResetAsync(conn);
    }

    private static async Task<bool> HasResettableTablesAsync(NpgsqlConnection conn, Table[] ignored)
    {
        var names = ignored.Select(t => t.Name).ToHashSet();
        await using var cmd = new NpgsqlCommand(
            "select table_name from information_schema.tables where table_schema = 'public' and table_type = 'BASE TABLE'", conn);
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            if (!names.Contains(reader.GetString(0))) return true;
        return false;
    }

    // Respawn không hỗ trợ wildcard, nên liệt kê các bảng AspNet* hiện có (IdentityDbContext đặt tên bảng tường minh, snake_case chỉ áp dụng cho cột, khóa, chỉ mục).
    private static async Task<Table[]> GetTablesToIgnoreAsync(NpgsqlConnection conn)
    {
        var ignore = new List<Table> { new("public", "__EFMigrationsHistory") };
        await using var cmd = new NpgsqlCommand(
            "select table_name from information_schema.tables where table_schema = 'public' and table_name like 'AspNet%'", conn);
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            ignore.Add(new Table("public", reader.GetString(0)));
        return [.. ignore];
    }
}
