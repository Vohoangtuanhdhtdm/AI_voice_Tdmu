using System.Text;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Tdmu.Advisor.Api.Infrastructure.Gemini;
using UglyToad.PdfPig;

namespace Tdmu.Advisor.Api.Features.Ingestion;

public record ExtractedBlock(string Text, int? Trang, string? TieuDe);

public class TextExtractor(IGeminiClient gemini)
{
    /// <summary>PDF trung bình dưới ngưỡng này (ký tự/trang) được coi là bản scan và chuyển qua Gemini.</summary>
    private const int ScanThresholdCharsPerPage = 200;

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
            return ParseMarkdown(await gemini.PdfToMarkdownAsync(data, ct));

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
        foreach (var p in doc.MainDocumentPart?.Document?.Body?.Descendants<Paragraph>() ?? [])
        {
            var text = p.InnerText.Trim();
            if (text.Length == 0) continue;
            if (IsHeading(p, styles)) heading = text;
            else blocks.Add(new ExtractedBlock(text, null, heading));
        }
        return blocks;
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
