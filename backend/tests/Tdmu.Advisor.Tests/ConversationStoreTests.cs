using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Tdmu.Advisor.Api.Features.Conversations;
using Tdmu.Advisor.Api.Infrastructure;
using Tdmu.Advisor.Tests.Infrastructure;

namespace Tdmu.Advisor.Tests;

[Collection("api")]
public class ConversationStoreTests(ApiFactory f)
{
    [Fact]
    public async Task Add_masks_pii_but_ticket_keeps_contact()
    {
        await f.ResetDatabaseAsync();
        using var scope = f.Services.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<ConversationStore>();
        var c = await store.GetOrCreateAsync(null, "chat", false, null, "ua", default);

        var m = await store.AddAsync(c.Id, "user", "chat", "SĐT em 0912345678, mail a@b.vn", new JsonObject { ["x"] = 1 }, new JsonObject { ["total"] = 12 }, false, default);
        var t = await store.CreateTicketAsync(c.Id, "An", "0912345678", "cần tư vấn", default);

        var db = scope.ServiceProvider.GetRequiredService<AdvisorDbContext>();
        var saved = await db.Set<Message>().AsNoTracking().SingleAsync(x => x.Id == m.Id);
        Assert.Equal("SĐT em [SĐT], mail [EMAIL]", saved.NoiDung);
        Assert.Equal(1, (int)saved.ToolCalls!["x"]!);
        Assert.Equal(12, (int)saved.LatencyMs!["total"]!);
        var ticket = await db.Set<Ticket>().AsNoTracking().SingleAsync(x => x.Id == t.Id);
        Assert.Equal("0912345678", ticket.LienHe);
        Assert.Equal("moi", ticket.TrangThai);
    }

    [Fact]
    public async Task Recent_returns_last_n_turns_in_order()
    {
        await f.ResetDatabaseAsync();
        using var scope = f.Services.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<ConversationStore>();
        var c = await store.GetOrCreateAsync(null, "chat", false, null, null, default);
        for (var i = 1; i <= 12; i++)
        {
            await store.AddAsync(c.Id, "user", "chat", $"hỏi {i}", null, null, false, default);
            await store.AddAsync(c.Id, "assistant", "chat", $"đáp {i}", null, null, false, default);
        }

        var recent = await store.RecentAsync(c.Id, 10, default);

        Assert.Equal(20, recent.Count);
        Assert.Equal("hỏi 3", recent[0].NoiDung);
        Assert.Equal("đáp 12", recent[^1].NoiDung);
    }

    [Fact]
    public async Task GetOrCreate_with_unknown_id_creates_new()
    {
        await f.ResetDatabaseAsync();
        using var scope = f.Services.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<ConversationStore>();
        var unknown = Guid.NewGuid();

        var c = await store.GetOrCreateAsync(unknown, "live", true, null, null, default);
        var again = await store.GetOrCreateAsync(c.Id, "chat", true, null, null, default);

        Assert.NotEqual(unknown, c.Id);
        Assert.Equal("live", c.KenhDauTien);
        Assert.True(c.LaHoiThu);
        Assert.Equal(c.Id, again.Id);
    }
}
