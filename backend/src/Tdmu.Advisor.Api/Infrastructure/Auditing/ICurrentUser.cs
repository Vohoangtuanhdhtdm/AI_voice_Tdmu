namespace Tdmu.Advisor.Api.Infrastructure.Auditing;

public interface ICurrentUser
{
    string UserId { get; }
}

public class HttpCurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    public string UserId =>
        accessor.HttpContext?.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "system";
}
