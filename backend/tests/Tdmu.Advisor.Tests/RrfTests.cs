using Tdmu.Advisor.Api.Features.Agent.Search;

namespace Tdmu.Advisor.Tests;

public class RrfTests
{
    [Fact]
    public void Fuses_lists_by_reciprocal_rank()
    {
        Guid a = Guid.NewGuid(), b = Guid.NewGuid(), c = Guid.NewGuid();
        var r = Rrf.Fuse([[a, b, c], [c, a]], 60);
        Assert.Equal([a, c, b], r.Select(x => x.Id));
        Assert.Equal(1.0 / 61 + 1.0 / 62, r[0].Score, 12);
        Assert.Equal(1.0 / 63 + 1.0 / 61, r[1].Score, 12);
    }

    [Fact]
    public void Ties_keep_first_appearance_order()
    {
        Guid a = Guid.NewGuid(), b = Guid.NewGuid();
        Assert.Equal([a, b], Rrf.Fuse([[a], [b]], 60).Select(x => x.Id));
    }
}
