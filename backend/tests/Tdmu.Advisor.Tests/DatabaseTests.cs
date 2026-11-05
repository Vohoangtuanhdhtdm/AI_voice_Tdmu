using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Tdmu.Advisor.Api.Infrastructure;
using Tdmu.Advisor.Tests.Infrastructure;

namespace Tdmu.Advisor.Tests;

[Collection("api")]
public class DatabaseTests(ApiFactory f)
{
    [Fact]
    public async Task Required_extensions_are_installed()
    {
        using var scope = f.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AdvisorDbContext>();
        var names = await db.Database.SqlQueryRaw<string>("SELECT extname AS \"Value\" FROM pg_extension").ToListAsync();
        Assert.Contains("vector", names);
        Assert.Contains("unaccent", names);
        Assert.Contains("pg_trgm", names);
    }

    [Fact]
    public async Task ResetDatabase_preserves_identity_rows()
    {
        using var scope = f.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AdvisorDbContext>();
        await db.Database.ExecuteSqlRawAsync(
            "INSERT INTO \"AspNetRoles\" (id, name, normalized_name) VALUES ('reset-test', 'ResetTest', 'RESETTEST') ON CONFLICT DO NOTHING");

        await f.ResetDatabaseAsync();

        var count = await db.Database.SqlQueryRaw<int>(
            "SELECT count(*)::int AS \"Value\" FROM \"AspNetRoles\" WHERE id = 'reset-test'").SingleAsync();
        Assert.Equal(1, count);
    }
}
