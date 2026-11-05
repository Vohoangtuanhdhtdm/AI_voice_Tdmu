namespace Tdmu.Advisor.Api.Features.KnowledgeBase.Import;

/// <summary>Một dòng dữ liệu của file. <see cref="RowNumber"/> theo dòng trong file (header là dòng 1).</summary>
public record ImportRow(int RowNumber, IReadOnlyDictionary<string, string?> Values, IReadOnlyList<string> Errors);

public record ImportPreview(string Kind, IReadOnlyList<ImportRow> Rows, int ValidCount, int ErrorCount);

public record ImportResult(int Inserted, int Updated);

/// <summary>Lỗi cấp file (thiếu cột bắt buộc, file không đọc được): trả 400.</summary>
public sealed class ImportFileException(string message) : Exception(message);
