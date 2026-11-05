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
}
