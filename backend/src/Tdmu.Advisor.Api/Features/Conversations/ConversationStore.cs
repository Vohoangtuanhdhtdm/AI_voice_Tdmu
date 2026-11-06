using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Tdmu.Advisor.Api.Infrastructure;

namespace Tdmu.Advisor.Api.Features.Conversations;

public class ConversationStore(AdvisorDbContext db)
{
    public async Task<Conversation> GetOrCreateAsync(Guid? id, string kenh, bool laHoiThu, Guid? releaseId, string? userAgent, CancellationToken ct)
    {
        if (id is { } existing)
        {
            var found = await db.Set<Conversation>().FirstOrDefaultAsync(c => c.Id == existing, ct);
            if (found != null) return found;
        }
        var c = new Conversation
        {
            Id = Guid.NewGuid(), KenhDauTien = kenh, LaHoiThu = laHoiThu,
            ReleaseId = releaseId, UserAgent = userAgent, BatDau = DateTime.UtcNow,
        };
        db.Set<Conversation>().Add(c);
        await db.SaveChangesAsync(ct);
        return c;
    }

    public async Task<IReadOnlyList<Message>> RecentAsync(Guid conversationId, int turns, CancellationToken ct)
    {
        var latest = await db.Set<Message>().AsNoTracking()
            .Where(m => m.ConversationId == conversationId)
            .OrderByDescending(m => m.TaoLuc).ThenByDescending(m => m.Id)
            .Take(turns * 2).ToListAsync(ct);
        latest.Reverse();
        return latest;
    }

    public async Task<Message> AddAsync(Guid conversationId, string vaiTro, string kenh, string noiDung,
        JsonNode? toolCalls, JsonNode? latency, bool coLoi, CancellationToken ct)
    {
        var m = new Message
        {
            Id = Guid.NewGuid(), ConversationId = conversationId, VaiTro = vaiTro, Kenh = kenh,
            NoiDung = PiiMasker.Mask(noiDung), ToolCalls = MaskStrings(toolCalls), LatencyMs = latency,
            CoLoi = coLoi, TaoLuc = DateTime.UtcNow,
        };
        db.Set<Message>().Add(m);
        await db.SaveChangesAsync(ct);
        return m;
    }

    /// <summary>Bản sao của <paramref name="node"/> với mọi chuỗi đã che SĐT/email: liên hệ chỉ được lưu nguyên vẹn trong ticket.</summary>
    private static JsonNode? MaskStrings(JsonNode? node) => node switch
    {
        null => null,
        JsonObject o => new JsonObject(o.Select(kv => KeyValuePair.Create(kv.Key, MaskStrings(kv.Value)))),
        JsonArray a => new JsonArray(a.Select(MaskStrings).ToArray()),
        JsonValue v when v.TryGetValue<string>(out var str) => JsonValue.Create(PiiMasker.Mask(str)),
        _ => node.DeepClone(),
    };

    public async Task<Ticket> CreateTicketAsync(Guid? conversationId, string hoTen, string lienHe, string lyDo, CancellationToken ct)
    {
        var t = new Ticket
        {
            Id = Guid.NewGuid(), ConversationId = conversationId, HoTen = hoTen,
            LienHe = lienHe, LyDo = lyDo, TaoLuc = DateTime.UtcNow,
        };
        db.Set<Ticket>().Add(t);
        await db.SaveChangesAsync(ct);
        return t;
    }
}
