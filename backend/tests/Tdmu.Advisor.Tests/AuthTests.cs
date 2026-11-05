using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Tdmu.Advisor.Tests.Infrastructure;

namespace Tdmu.Advisor.Tests;

[Collection("api")]
public class AuthTests(ApiFactory f)
{
    [Fact]
    public async Task Login_with_valid_credentials_returns_token()
    {
        var res = await f.CreateClient().PostAsJsonAsync("/api/auth/login",
            new { email = ApiFactory.AdminEmail, password = ApiFactory.AdminPassword });
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var body = await res.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(string.IsNullOrEmpty(body.GetProperty("accessToken").GetString()));
        var expiresAt = body.GetProperty("expiresAt").GetDateTimeOffset();
        Assert.InRange((expiresAt - DateTimeOffset.UtcNow).TotalMinutes, 59, 61);

        var admin = await AuthHelper.AdminClientAsync(f);
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync("/api/admin/ping")).StatusCode);
    }

    [Fact]
    public async Task Admin_endpoint_without_token_returns_401()
    {
        var res = await f.CreateClient().GetAsync("/api/admin/ping");
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task Wrong_password_returns_401()
    {
        var res = await f.CreateClient().PostAsJsonAsync("/api/auth/login",
            new { email = ApiFactory.AdminEmail, password = "sai-mat-khau" });
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task Non_admin_token_returns_403()
    {
        var email = $"user-{Guid.NewGuid():N}@test.local";
        const string password = "User#Pass123";
        using (var scope = f.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
            Assert.True((await users.CreateAsync(new IdentityUser { UserName = email, Email = email }, password)).Succeeded);
        }
        var client = f.CreateClient();
        var login = await client.PostAsJsonAsync("/api/auth/login", new { email, password });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var token = (await login.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("accessToken").GetString();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/admin/ping")).StatusCode);
    }

    [Fact]
    public async Task Five_wrong_passwords_lock_account()
    {
        // Dùng user riêng để không khóa admin seed của các test khác.
        var email = $"lock-{Guid.NewGuid():N}@test.local";
        const string password = "Right#Pass123";
        using (var scope = f.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
            var created = await users.CreateAsync(new IdentityUser { UserName = email, Email = email }, password);
            Assert.True(created.Succeeded);
        }

        var client = f.CreateClient();
        for (var i = 0; i < 5; i++)
        {
            var bad = await client.PostAsJsonAsync("/api/auth/login", new { email, password = "sai" });
            // Lần sai thứ 5 kích hoạt khóa nên có thể đã trả 423; 4 lần đầu phải là 401.
            if (i < 4) Assert.Equal(HttpStatusCode.Unauthorized, bad.StatusCode);
        }
        var res = await client.PostAsJsonAsync("/api/auth/login", new { email, password });
        Assert.Equal((HttpStatusCode)423, res.StatusCode);
        var problem = await res.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Tài khoản tạm khóa do đăng nhập sai nhiều lần", problem.GetProperty("title").GetString());
    }
}
