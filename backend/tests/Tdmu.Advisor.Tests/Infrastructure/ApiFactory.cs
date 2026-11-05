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
    }

    /// <summary>Xóa dữ liệu mọi bảng (trừ lịch sử migration và bảng Identity). Respawner tạo lười vì ở Task 1 chưa có bảng nào.</summary>
    public async Task ResetDatabaseAsync()
    {
        await using var conn = new NpgsqlConnection(_db.GetConnectionString());
        await conn.OpenAsync();
        if (_respawner is null)
        {
            try
            {
                _respawner = await Respawner.CreateAsync(conn, new RespawnerOptions
                {
                    DbAdapter = DbAdapter.Postgres,
                    SchemasToInclude = ["public"],
                    TablesToIgnore = await GetTablesToIgnoreAsync(conn),
                });
            }
            catch (InvalidOperationException)
            {
                return; // Chưa có bảng nào để xóa (Respawn báo lỗi khi không tìm thấy bảng); thử lại ở lần gọi sau.
            }
        }
        await _respawner.ResetAsync(conn);
    }

    // Respawn không hỗ trợ wildcard, nên liệt kê các bảng asp_net_* (Identity, snake_case) hiện có.
    private static async Task<Table[]> GetTablesToIgnoreAsync(NpgsqlConnection conn)
    {
        var ignore = new List<Table> { new("public", "__EFMigrationsHistory") };
        await using var cmd = new NpgsqlCommand(
            "select table_name from information_schema.tables where table_schema = 'public' and table_name like 'asp_net%'", conn);
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            ignore.Add(new Table("public", reader.GetString(0)));
        return [.. ignore];
    }
}
