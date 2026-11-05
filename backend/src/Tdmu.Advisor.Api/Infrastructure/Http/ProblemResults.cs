using Microsoft.AspNetCore.Mvc;

namespace Tdmu.Advisor.Api.Infrastructure.Http;

/// <summary>ProblemDetails tiếng Việt cho lỗi trả về admin.</summary>
public static class ProblemResults
{
    public static IResult Unauthorized(string title = "Email hoặc mật khẩu không đúng") =>
        Problem(StatusCodes.Status401Unauthorized, title);

    public static IResult Locked(string title = "Tài khoản tạm khóa do đăng nhập sai nhiều lần") =>
        Problem(423, title);

    public static IResult Problem(int status, string title, string? detail = null) =>
        Results.Json(new ProblemDetails { Status = status, Title = title, Detail = detail },
            statusCode: status, contentType: "application/problem+json");
}
