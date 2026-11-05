using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Tdmu.Advisor.Api.Features.KnowledgeBase.Entities;

namespace Tdmu.Advisor.Api.Infrastructure.Auditing;

/// <summary>Ghi AuditLog cho mọi thay đổi trên bản nháp (ReleaseId == null), trong cùng lần SaveChanges.</summary>
public class AuditInterceptor(ICurrentUser user, AuditSuppression suppression) : SaveChangesInterceptor
{
    private static readonly HashSet<string> Excluded = ["Id", "Embedding", "Tsv"];
    private static readonly JsonSerializerOptions Json = new() { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        AddAuditRows(eventData.Context);
        return result;
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        AddAuditRows(eventData.Context);
        return ValueTask.FromResult(result);
    }

    private void AddAuditRows(DbContext? db)
    {
        if (db is null || suppression.IsSuppressed) return;
        var entries = db.ChangeTracker.Entries<KbEntity>()
            .Where(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted && e.Entity.ReleaseId == null)
            .ToList();
        var now = DateTimeOffset.UtcNow;
        foreach (var e in entries)
        {
            var (action, before, after) = e.State switch
            {
                EntityState.Added => ("Created", null, Snapshot(e, e.CurrentValues)),
                EntityState.Deleted => ("Deleted", Snapshot(e, e.OriginalValues), null),
                _ => ("Updated", Snapshot(e, e.OriginalValues), Snapshot(e, e.CurrentValues)),
            };
            db.Set<AuditLog>().Add(new AuditLog
            {
                Entity = e.Entity.GetType().Name,
                EntityLogicalId = e.Entity.LogicalId,
                Action = action,
                BeforeJson = before,
                AfterJson = after,
                UserId = user.UserId,
                At = now,
            });
        }
    }

    private static string Snapshot(EntityEntry entry, PropertyValues values) =>
        JsonSerializer.Serialize(entry.Properties
            .Where(p => !Excluded.Contains(p.Metadata.Name))
            .ToDictionary(p => p.Metadata.Name, p => values[p.Metadata.Name]), Json);
}
