using Microsoft.AspNetCore.Mvc;

namespace Tdmu.Advisor.Api.Infrastructure.Http;

/// <summary>ProblemDetails tiếng Việt cho lỗi trả về admin.</summary>
public static class ProblemResults
{
    public static IResult Unauthorized(string title = "Email hoặc mật khẩu không đúng") =>
        Problem(StatusCodes.Status401Unauthorized, title);

    public static IResult Locked(string title = "Tài khoản tạm khóa do đăng nhập sai nhiều lần") =>
        Problem(423, title);

    public static IResult NotFound(string title = "Không tìm thấy bản ghi") =>
        Problem(StatusCodes.Status404NotFound, title);

    public static IResult Conflict(string title, IDictionary<string, object?>? extensions = null)
    {
        var pd = new ProblemDetails { Status = StatusCodes.Status409Conflict, Title = title };
        if (extensions is not null)
            foreach (var (k, v) in extensions) pd.Extensions[k] = v;
        return Results.Json(pd, statusCode: pd.Status, contentType: "application/problem+json");
    }

    public static IResult Invalid(IDictionary<string, string[]> errors) =>
        Results.ValidationProblem(errors, title: "Dữ liệu không hợp lệ");

    public static IResult Problem(int status, string title, string? detail = null) =>
        Results.Json(new ProblemDetails { Status = status, Title = title, Detail = detail },
            statusCode: status, contentType: "application/problem+json");
}
