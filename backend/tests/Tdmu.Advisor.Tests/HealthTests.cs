using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Tdmu.Advisor.Tests.Infrastructure;

namespace Tdmu.Advisor.Tests;

[Collection("api")]
public class HealthTests(ApiFactory f)
{
    [Fact]
    public async Task Health_returns_ok()
    {
        var res = await f.CreateClient().GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        Assert.Equal("ok", (await res.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("status").GetString());
    }
}
