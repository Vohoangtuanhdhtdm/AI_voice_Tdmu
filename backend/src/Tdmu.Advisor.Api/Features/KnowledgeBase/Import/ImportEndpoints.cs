using ClosedXML.Excel;
using Tdmu.Advisor.Api.Infrastructure.Http;

namespace Tdmu.Advisor.Api.Features.KnowledgeBase.Import;

public static class ImportEndpoints
{
    private const string XlsxType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    public static void MapImportEndpoints(this RouteGroupBuilder admin)
    {
        var g = admin.MapGroup("/import");

        g.MapGet("/{kind}/template", (string kind) =>
        {
            if (!ImportService.IsKnownKind(kind)) return ProblemResults.NotFound("Loại dữ liệu nhập không tồn tại");
            var (columns, example) = ImportService.Template(kind);
            using var wb = new XLWorkbook();
            var ws = wb.Worksheets.Add(kind);
            for (var i = 0; i < columns.Length; i++)
            {
                ws.Cell(1, i + 1).Value = columns[i];
                ws.Cell(1, i + 1).Style.Font.Bold = true;
                if (example[i] is { } v && v is not "") ws.Cell(2, i + 1).Value = XLCellValue.FromObject(v);
            }
            ws.Columns().AdjustToContents();
            using var ms = new MemoryStream();
            wb.SaveAs(ms);
            return Results.File(ms.ToArray(), XlsxType, $"mau-{kind}.xlsx");
        });

        g.MapPost("/{kind}/preview", async (string kind, IFormFile? file, ImportService svc, CancellationToken ct) =>
        {
            var (problem, parsed) = await ParseAsync(kind, file, svc, ct);
            return problem ?? Results.Ok(parsed!.Preview);
        }).DisableAntiforgery();

        g.MapPost("/{kind}/commit", async (string kind, IFormFile? file, ImportService svc, CancellationToken ct) =>
        {
            var (problem, parsed) = await ParseAsync(kind, file, svc, ct);
            if (problem is not null) return problem;
            if (parsed!.Preview.ErrorCount > 0)
                return Results.Json(new { status = 400, title = "File còn dòng lỗi, chưa nhập dữ liệu nào", preview = parsed.Preview },
                    statusCode: 400, contentType: "application/problem+json");
            try
            {
                return Results.Ok(await svc.CommitAsync(kind, parsed, ct));
            }
            catch (Microsoft.EntityFrameworkCore.DbUpdateException)
            {
                return ProblemResults.Conflict("Dữ liệu xung đột với bản ghi đang có trong bản nháp, vui lòng thử lại");
            }
        }).DisableAntiforgery();
    }

    private static async Task<(IResult? Problem, ImportService.Parsed? Parsed)> ParseAsync(
        string kind, IFormFile? file, ImportService svc, CancellationToken ct)
    {
        if (!ImportService.IsKnownKind(kind)) return (ProblemResults.NotFound("Loại dữ liệu nhập không tồn tại"), null);
        if (file is null || file.Length == 0) return (ProblemResults.Problem(400, "Chưa chọn file hoặc file rỗng"), null);
        if (file.Length > ImportService.MaxBytes) return (ProblemResults.Problem(413, "File vượt quá 5 MB"), null);
        var name = file.FileName ?? "";
        if (!name.EndsWith(".csv", StringComparison.OrdinalIgnoreCase) && !name.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
            return (ProblemResults.Problem(400, "Chỉ nhận file .csv hoặc .xlsx"), null);
        try
        {
            await using var stream = file.OpenReadStream();
            return (null, await svc.PreviewAsync(kind, stream, name, ct));
        }
        catch (ImportFileException ex)
        {
            return (ProblemResults.Problem(400, ex.Message), null);
        }
    }
}
