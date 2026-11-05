namespace Tdmu.Advisor.Api.Infrastructure.Auth;

public class AuthOptions
{
    public const string Section = "Auth";

    public string JwtKey { get; set; } = "";
    public string Issuer { get; set; } = "tdmu-advisor";
    public string Audience { get; set; } = "tdmu-advisor-admin";
    public int AccessTokenMinutes { get; set; } = 60;
    public string SeedAdminEmail { get; set; } = "";
    public string SeedAdminPassword { get; set; } = "";
}
