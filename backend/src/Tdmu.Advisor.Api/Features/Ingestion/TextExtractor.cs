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
                pages.Add((page.Number, page.Text.Trim()));

        var total = pages.Sum(p => p.Text.Length);
        if (pages.Count == 0 || total / pages.Count < ScanThresholdCharsPerPage)
            return ParseMarkdown(await gemini.PdfToMarkdownAsync(data, ct));

        return pages.Where(p => p.Text.Length > 0).Select(p => new ExtractedBlock(p.Text, p.Number, null)).ToList();
    }

    private static IReadOnlyList<ExtractedBlock> ExtractDocx(byte[] data)
    {
        using var ms = new MemoryStream(data);
        using var doc = WordprocessingDocument.Open(ms, false);
        var blocks = new List<ExtractedBlock>();
        string? heading = null;
        foreach (var p in doc.MainDocumentPart?.Document?.Body?.Descendants<Paragraph>() ?? [])
        {
            var text = p.InnerText.Trim();
            if (text.Length == 0) continue;
            var style = p.ParagraphProperties?.ParagraphStyleId?.Val?.Value;
            if (style is not null && style.StartsWith("Heading", StringComparison.OrdinalIgnoreCase)) heading = text;
            else blocks.Add(new ExtractedBlock(text, null, heading));
        }
        return blocks;
    }

    /// <summary>Dòng bắt đầu bằng <c>#</c> là tiêu đề; các đoạn cách nhau bằng dòng trống.</summary>
    internal static IReadOnlyList<ExtractedBlock> ParseMarkdown(string markdown)
    {
        var blocks = new List<ExtractedBlock>();
        var para = new StringBuilder();
        string? heading = null;

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
                heading = title.Length > 0 ? title : heading;
            }
            else if (line.Length == 0) FlushPara();
            else para.Append(para.Length > 0 ? "\n" : "").Append(line);
        }
        FlushPara();
        return blocks;
    }
}
