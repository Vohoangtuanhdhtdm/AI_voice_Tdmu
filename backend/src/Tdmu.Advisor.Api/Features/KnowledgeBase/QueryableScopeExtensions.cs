using Tdmu.Advisor.Api.Features.KnowledgeBase.Entities;

namespace Tdmu.Advisor.Api.Features.KnowledgeBase;

public static class QueryableScopeExtensions
{
    /// <summary>Lọc theo scope: <c>null</c> là bản nháp, ngược lại là một release.</summary>
    public static IQueryable<T> InScope<T>(this IQueryable<T> q, Guid? releaseId) where T : KbEntity =>
        q.Where(x => x.ReleaseId == releaseId);
}
