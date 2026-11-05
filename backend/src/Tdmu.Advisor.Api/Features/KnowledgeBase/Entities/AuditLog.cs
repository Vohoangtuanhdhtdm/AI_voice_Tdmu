namespace Tdmu.Advisor.Api.Features.KnowledgeBase.Entities;

public class AuditLog
{
    public long Id { get; set; }
    public string Entity { get; set; } = "";
    public Guid EntityLogicalId { get; set; }
    public string Action { get; set; } = "";
    public string? BeforeJson { get; set; }
    public string? AfterJson { get; set; }
    public string UserId { get; set; } = "";
    public DateTimeOffset At { get; set; } = DateTimeOffset.UtcNow;
}
