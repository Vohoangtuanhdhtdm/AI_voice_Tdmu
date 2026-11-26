using System.Text;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Tdmu.Advisor.Api.Infrastructure.Gemini;
using UglyToad.PdfPig;

namespace Tdmu.Advisor.Api.Features.Ingestion;

public record ExtractedBlock(string Text, int? Trang, string? TieuDe);

/// <summary>Lỗi trích nội dung có thông báo tiếng Việt hiển thị nguyên văn cho cán bộ.</summary>
public class ExtractionException(string message) : Exception(message);

public class TextExtractor(IGeminiClient gemini)
{
    /// <summary>PDF trung bình dưới ngưỡng này (ký tự/trang) được coi là bản scan và chuyển qua Gemini.</summary>
    private const int ScanThresholdCharsPerPage = 200;
    /// <summary>Giới hạn PDF gửi inline cho Gemini (base64 phình ~33%, request inline tối đa ~20 MB).</summary>
    public const int MaxScanPdfBytes = 14 * 1024 * 1024;
    public const string ScanPdfTooLarge = "File PDF dạng ảnh quá lớn để chuyển đổi (tối đa 14 MB)";

    public async Task<IReadOnlyList<ExtractedBlock>> ExtractAsync(byte[] data, string ext, CancellationToken ct)
    {
        switch (ext.ToLowerInvariant())
        {
            case ".pdf": return await ExtractPdfAsync(data, ct);
            case ".docx": return ExtractDocx(data);
            case ".md":
            case ".txt": return ParseMarkdown(Encoding.UTF8.GetString(data));
            default: throw new NotSupportedException($"Định dạng {ext} chưa được hỗ trợ");
        }
    }

    private async Task<IReadOnlyList<ExtractedBlock>> ExtractPdfAsync(byte[] data, CancellationToken ct)
    {
        var pages = new List<(int Number, string Text)>();
        using (var pdf = PdfDocument.Open(data))
            foreach (var page in pdf.GetPages())
                pages.Add((page.Number, UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor.ContentOrderTextExtractor.GetText(page).Trim()));

        var total = pages.Sum(p => p.Text.Length);
        if (pages.Count == 0 || total / pages.Count < ScanThresholdCharsPerPage)
        {
            if (data.Length > MaxScanPdfBytes) throw new ExtractionException(ScanPdfTooLarge);
            return ParseMarkdown(await gemini.PdfToMarkdownAsync(data, ct));
        }

        return pages.SelectMany(p => System.Text.RegularExpressions.Regex.Split(p.Text, @"\n\s*\n")
                .Select(t => t.Trim()).Where(t => t.Length > 0).Select(t => new ExtractedBlock(t, p.Number, null))).ToList();
    }

    private static IReadOnlyList<ExtractedBlock> ExtractDocx(byte[] data)
    {
        using var ms = new MemoryStream(data);
        using var doc = WordprocessingDocument.Open(ms, false);
        var styles = doc.MainDocumentPart?.StyleDefinitionsPart?.Styles?.Elements<Style>()
            .Where(x => x.StyleId?.Value is not null).GroupBy(x => x.StyleId!.Value!).ToDictionary(g => g.Key, g => g.First())
            ?? new Dictionary<string, Style>();
        var blocks = new List<ExtractedBlock>();
        string? heading = null;

        void AddParagraph(Paragraph p)
        {
            var text = p.InnerText.Trim();
            if (text.Length == 0) return;
            if (IsHeading(p, styles)) heading = text;
            else blocks.Add(new ExtractedBlock(text, null, heading));
        }

        // Duyệt thân tài liệu theo đúng thứ tự để bảng nằm đúng chỗ giữa các đoạn.
        foreach (var el in doc.MainDocumentPart?.Document?.Body?.ChildElements ?? Enumerable.Empty<DocumentFormat.OpenXml.OpenXmlElement>())
        {
            switch (el)
            {
                case Paragraph p: AddParagraph(p); break;
                case Table t: blocks.AddRange(TableBlocks(t, heading)); break;
                default:
                    // Khối khác (content control...): giữ cách cũ, lấy các đoạn bên trong.
                    foreach (var p in el.Descendants<Paragraph>()) AddParagraph(p);
                    break;
            }
        }
        return blocks;
    }

    /// <summary>
    /// Mỗi dòng bảng thành một khối "ô | ô | ô"; dòng đầu (tiêu đề cột) được thêm vào đầu mỗi khối để số liệu
    /// luôn đi kèm tên cột dù bảng bị chia sang nhiều chunk.
    /// </summary>
    private static IEnumerable<ExtractedBlock> TableBlocks(Table table, string? heading)
    {
        static string Cell(TableCell c) => string.Join(" ", c.Descendants<Paragraph>().Select(p => p.InnerText.Trim()).Where(t => t.Length > 0));
        var rows = table.Elements<TableRow>()
            .Select(r => r.Elements<TableCell>().Select(Cell).ToList())
            .Where(cells => cells.Any(c => c.Length > 0))
            .Select(cells => string.Join(" | ", cells))
            .ToList();
        if (rows.Count == 0) yield break;
        if (rows.Count == 1) { yield return new ExtractedBlock(rows[0], null, heading); yield break; }
        foreach (var row in rows.Skip(1))
            yield return new ExtractedBlock(rows[0] + "\n" + row, null, heading);
    }

    /// <summary>Word tiếng Việt dùng id style địa phương hóa (ví dụ "u1"), nên tra tên style và outline level; kiểm tra id "Heading*" chỉ là dự phòng.</summary>
    private static bool IsHeading(Paragraph p, Dictionary<string, Style> styles)
    {
        static bool Outlined(int? level) => level is >= 0 and < 9; // 9 = văn bản thường.
        if (Outlined(p.ParagraphProperties?.OutlineLevel?.Val?.Value)) return true;
        var id = p.ParagraphProperties?.ParagraphStyleId?.Val?.Value;
        if (id is null) return false;
        if (styles.TryGetValue(id, out var style))
        {
            var name = style.StyleName?.Val?.Value;
            if (name is not null && name.StartsWith("heading", StringComparison.OrdinalIgnoreCase)) return true;
            if (Outlined(style.StyleParagraphProperties?.OutlineLevel?.Val?.Value)) return true;
        }
        return id.StartsWith("Heading", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Dòng bắt đầu bằng <c>#</c> là tiêu đề; các đoạn cách nhau bằng dòng trống.</summary>
    internal static IReadOnlyList<ExtractedBlock> ParseMarkdown(string markdown)
    {
        var blocks = new List<ExtractedBlock>();
        var para = new StringBuilder();
        string? heading = null;
        var path = new List<(int Level, string Title)>();

        void FlushPara()
        {
            var text = para.ToString().Trim();
            if (text.Length > 0) blocks.Add(new ExtractedBlock(text, null, heading));
            para.Clear();
        }

        foreach (var raw in markdown.Replace("\r\n", "\n").Split('\n'))
        {
            var line = raw.TrimEnd();
            if (line.StartsWith('#'))
            {
                FlushPara();
                var title = line.TrimStart('#').Trim();
                if (title.Length > 0)
                {
                    var level = line.Length - line.TrimStart('#').Length;
                    path.RemoveAll(x => x.Level >= level);
                    path.Add((level, title));
                    heading = string.Join(" › ", path.Select(x => x.Title));
                }
            }
            else if (line.Length == 0) FlushPara();
            else para.Append(para.Length > 0 ? "\n" : "").Append(line);
        }
        FlushPara();
        return blocks;
    }
}
