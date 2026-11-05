using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Tdmu.Advisor.Api.Features.KnowledgeBase.Entities;
using Tdmu.Advisor.Api.Infrastructure;
using Tdmu.Advisor.Api.Infrastructure.Auditing;
using Tdmu.Advisor.Tests.Infrastructure;

namespace Tdmu.Advisor.Tests;

[Collection("api")]
public class AuditTests(ApiFactory f)
{
    private sealed class FakeUser(string id) : ICurrentUser
    {
        public string UserId => id;
    }

    [Fact]
    public async Task Create_update_delete_draft_entity_writes_audit_rows()
    {
        await f.ResetDatabaseAsync();
        using var host = f.WithWebHostBuilder(b => b.ConfigureServices(s => s.AddSingleton<ICurrentUser>(new FakeUser("u1"))));
        using var scope = host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AdvisorDbContext>();

        var n = new Nganh { MaNganh = "7480201", Ten = "Cũ" };
        db.Nganh.Add(n);
        await db.SaveChangesAsync();
        n.Ten = "Mới";
        await db.SaveChangesAsync();
        db.Nganh.Remove(n);
        await db.SaveChangesAsync();

        var rows = await db.AuditLog.OrderBy(a => a.Id).ToListAsync();
        Assert.Equal(["Created", "Updated", "Deleted"], rows.Select(r => r.Action));
        Assert.All(rows, r => { Assert.Equal("u1", r.UserId); Assert.Equal("Nganh", r.Entity); Assert.Equal(n.LogicalId, r.EntityLogicalId); });
        Assert.Contains("Cũ", rows[1].BeforeJson);
        Assert.Contains("Mới", rows[1].AfterJson);
    }
}
