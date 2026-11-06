namespace Tdmu.Advisor.Api.Features.Agent.Search;

/// <summary>Reciprocal Rank Fusion: điểm của một id là tổng 1/(k + hạng) qua các danh sách, hạng bắt đầu từ 1.</summary>
public static class Rrf
{
    public static IReadOnlyList<(Guid Id, double Score)> Fuse(IEnumerable<IReadOnlyList<Guid>> rankedLists, int k)
    {
        var scores = new Dictionary<Guid, double>();
        var order = new List<Guid>(); // thứ tự xuất hiện đầu tiên, dùng để phá hòa
        foreach (var list in rankedLists)
            for (var i = 0; i < list.Count; i++)
            {
                if (!scores.TryGetValue(list[i], out var s)) order.Add(list[i]);
                scores[list[i]] = s + 1.0 / (k + i + 1);
            }
        return order.Select(id => (id, scores[id])).OrderByDescending(x => x.Item2).ToList(); // OrderBy ổn định
    }
}
