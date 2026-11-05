using Microsoft.AspNetCore.Hosting;
using System.Net;
using System.Text.Json;
using Tdmu.Advisor.Tests.Infrastructure;

namespace Tdmu.Advisor.Tests;

[Collection("api")]
public class OpenApiTests(ApiFactory f)
{
    [Fact]
    public async Task OpenApi_document_lists_admin_and_public_routes()
    {
        using var factory = f.WithWebHostBuilder(b =>
        {
            b.UseEnvironment("Development");
            b.UseSetting("Seed:SampleData", "false");
        });
        var res = await factory.CreateClient().GetAsync("/swagger/v1/swagger.json");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
        var paths = doc.RootElement.GetProperty("paths");
        foreach (var p in new[] { "/api/admin/kb/diem-chuan", "/api/admin/releases", "/api/public/nganh" })
            Assert.True(paths.TryGetProperty(p, out _), $"Thiếu path {p}");
        Assert.True(doc.RootElement.GetProperty("components").GetProperty("securitySchemes").TryGetProperty("Bearer", out _));
    }
}
