namespace Tdmu.Advisor.Api.Infrastructure.Auditing;

/// <summary>Tạm tắt audit theo từng dòng trong một scope (dùng khi khôi phục bản nháp: chỉ ghi một dòng audit tổng).</summary>
public class AuditSuppression
{
    public bool IsSuppressed { get; private set; }

    public IDisposable Begin()
    {
        var previous = IsSuppressed;
        IsSuppressed = true;
        return new Scope(this, previous);
    }

    private sealed class Scope(AuditSuppression owner, bool previous) : IDisposable
    {
        public void Dispose() => owner.IsSuppressed = previous;
    }
}
