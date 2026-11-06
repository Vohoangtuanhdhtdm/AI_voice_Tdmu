using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using System.Text;
using Tdmu.Advisor.Api.Features.Ingestion;
using Tdmu.Advisor.Tests.Infrastructure;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;

namespace Tdmu.Advisor.Tests;

public class TextExtractorTests
{
    private readonly FakeGeminiClient _gemini = new();

    private static byte[] Docx()
    {
        using var ms = new MemoryStream();
        using (var doc = WordprocessingDocument.Create(ms, WordprocessingDocumentType.Document))
        {
            var main = doc.AddMainDocumentPart();
            main.Document = new Document(new Body(
                new Paragraph(new ParagraphProperties(new ParagraphStyleId { Val = "Heading1" }), new Run(new Text("Học phí"))),
                new Paragraph(new Run(new Text("Học phí năm 2026 là 20 triệu đồng."))),
                new Paragraph(new Run(new Text("")))));
        }
        return ms.ToArray();
    }

    [Fact]
    public async Task Docx_headings_become_TieuDe()
    {
        var blocks = await new TextExtractor(_gemini).ExtractAsync(Docx(), ".docx", default);
        var b = Assert.Single(blocks);
        Assert.Equal("Học phí", b.TieuDe);
        Assert.Contains("20 triệu", b.Text);
    }

    [Fact]
    public async Task Markdown_hash_lines_are_headings()
    {
        var md = "# Ký túc xá\n\nPhòng 8 người.\n\nGiá 300k.\n\n## Nội quy\nKhông hút thuốc.";
        var blocks = await new TextExtractor(_gemini).ExtractAsync(Encoding.UTF8.GetBytes(md), ".md", default);
        Assert.Equal(3, blocks.Count);
        Assert.All(blocks.Take(2), b => Assert.Equal("Ký túc xá", b.TieuDe));
        Assert.Equal("Ký túc xá › Nội quy", blocks[2].TieuDe);
        Assert.DoesNotContain("#", blocks[0].Text);
    }

    [Fact]
    public async Task Scanned_pdf_falls_back_to_gemini_markdown()
    {
        var builder = new PdfDocumentBuilder();
        builder.AddPage(595, 842);
        _gemini.PdfMarkdown = "# Điểm chuẩn\n\nNgành Dược 24 điểm.";
        var blocks = await new TextExtractor(_gemini).ExtractAsync(builder.Build(), ".pdf", default);
        var b = Assert.Single(blocks);
        Assert.Equal("Điểm chuẩn", b.TieuDe);
        Assert.Contains("Dược", b.Text);
    }

    [Fact]
    public async Task Pdf_lines_are_not_merged()
    {
        var builder = new PdfDocumentBuilder();
        var font = builder.AddStandard14Font(Standard14Font.Helvetica);
        var page = builder.AddPage(595, 842);
        var filler = string.Concat(Enumerable.Repeat("chu ", 30));
        page.AddText(filler + "alpha", 8, new PdfPoint(20, 700), font);
        page.AddText("beta " + filler, 8, new PdfPoint(20, 680), font);
        var blocks = await new TextExtractor(_gemini).ExtractAsync(builder.Build(), ".pdf", default);
        var text = string.Join("\n", blocks.Select(b => b.Text));
        Assert.Contains("alpha", text);
        Assert.Contains("beta", text);
        Assert.DoesNotContain("alphabeta", text);
        Assert.Matches(@"alpha\s+beta", text);
    }

    [Fact]
    public async Task Docx_localized_heading_style_id_is_resolved_by_name()
    {
        using var ms = new MemoryStream();
        using (var doc = WordprocessingDocument.Create(ms, WordprocessingDocumentType.Document))
        {
            var main = doc.AddMainDocumentPart();
            main.AddNewPart<StyleDefinitionsPart>().Styles = new Styles(
                new Style(new StyleName { Val = "heading 1" }) { Type = StyleValues.Paragraph, StyleId = "u1" });
            main.Document = new Document(new Body(
                new Paragraph(new ParagraphProperties(new ParagraphStyleId { Val = "u1" }), new Run(new Text("Ký túc xá"))),
                new Paragraph(new Run(new Text("Phòng 8 người.")))));
        }
        var b = Assert.Single(await new TextExtractor(_gemini).ExtractAsync(ms.ToArray(), ".docx", default));
        Assert.Equal("Ký túc xá", b.TieuDe);
    }
}
