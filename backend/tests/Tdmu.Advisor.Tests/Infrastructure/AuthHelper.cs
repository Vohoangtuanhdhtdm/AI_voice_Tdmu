using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace Tdmu.Advisor.Tests.Infrastructure;

public static class AuthHelper
{
    /// <summary>Client đã đăng nhập bằng tài khoản admin seed (Bearer token).</summary>
    public static async Task<HttpClient> AdminClientAsync(ApiFactory f)
    {
        var client = f.CreateClient();
        var res = await client.PostAsJsonAsync("/api/auth/login",
            new { email = ApiFactory.AdminEmail, password = ApiFactory.AdminPassword });
        res.EnsureSuccessStatusCode();
        var token = (await res.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("accessToken").GetString();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }
}
