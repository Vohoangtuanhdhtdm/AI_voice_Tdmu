using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace Tdmu.Advisor.Api.Infrastructure.Auth;

public static class AdminSeeder
{
    public const string AdminRole = "Admin";

    public static async Task SeedAsync(IServiceProvider services)
    {
        var o = services.GetRequiredService<IOptions<AuthOptions>>().Value;
        var roles = services.GetRequiredService<RoleManager<IdentityRole>>();
        var users = services.GetRequiredService<UserManager<IdentityUser>>();

        if (!await roles.RoleExistsAsync(AdminRole))
            await roles.CreateAsync(new IdentityRole(AdminRole));

        if (string.IsNullOrWhiteSpace(o.SeedAdminEmail) || string.IsNullOrEmpty(o.SeedAdminPassword)) return;
        if (await users.FindByEmailAsync(o.SeedAdminEmail) is not null) return;

        var user = new IdentityUser { UserName = o.SeedAdminEmail, Email = o.SeedAdminEmail, EmailConfirmed = true };
        var created = await users.CreateAsync(user, o.SeedAdminPassword);
        if (!created.Succeeded)
            throw new InvalidOperationException("Không tạo được admin seed: " + string.Join("; ", created.Errors.Select(e => e.Description)));
        await users.AddToRoleAsync(user, AdminRole);
    }
}
