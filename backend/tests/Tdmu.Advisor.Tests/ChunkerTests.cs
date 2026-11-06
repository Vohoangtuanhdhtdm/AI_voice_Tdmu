using Tdmu.Advisor.Api.Features.Ingestion;

namespace Tdmu.Advisor.Tests;

public class ChunkerTests
{
    private static string Para(int i, int len = 500) => (("Đoạn " + i + " nói về tuyển sinh. " + string.Concat(Enumerable.Repeat("chữ ", 200)))[..len]).TrimEnd();

    [Fact]
    public void Groups_paragraphs_under_max_and_overlaps_previous_tail()
    {
        var blocks = Enumerable.Range(0, 10).Select(i => new ExtractedBlock(Para(i) + " ", null, "Học phí")).ToList();
        var chunks = Chunker.Split(blocks);

        Assert.True(chunks.Count >= 2);
        Assert.All(chunks, c => Assert.True(c.NoiDung.Length <= 3200));
        for (var i = 1; i < chunks.Count; i++)
        {
            var tail = chunks[i - 1].NoiDung[^500..];
            var start = chunks[i].NoiDung[..50];
            Assert.Contains(start, tail);
        }
        Assert.Equal(Enumerable.Range(1, chunks.Count), chunks.Select(c => c.ThuTu));
    }

    [Fact]
    public void Heading_change_starts_new_chunk_and_viTri_has_heading()
    {
        var chunks = Chunker.Split([
            new ExtractedBlock("Nội dung học phí.", null, "Học phí"),
            new ExtractedBlock("Nội dung ký túc xá.", null, "Ký túc xá"),
        ]);
        Assert.Equal(2, chunks.Count);
        Assert.Equal("Học phí", chunks[0].ViTri);
        Assert.Equal("Ký túc xá", chunks[1].ViTri);
        Assert.DoesNotContain("học phí", chunks[1].NoiDung);
    }

    [Fact]
    public void ViTri_formats_page_and_heading()
    {
        Assert.Equal("Trang 3", Chunker.Split([new ExtractedBlock("a", 3, null)])[0].ViTri);
        Assert.Equal("Học phí – trang 3", Chunker.Split([new ExtractedBlock("a", 3, "Học phí")])[0].ViTri);
    }

    [Fact]
    public void Long_paragraph_is_cut_at_sentence_boundaries()
    {
        var text = string.Concat(Enumerable.Range(0, 200).Select(i => $"Câu số {i} được viết ở đây. "));
        Assert.True(text.Length > 5000);
        var chunks = Chunker.Split([new ExtractedBlock(text.Trim(), null, null)]);
        Assert.True(chunks.Count >= 2);
        Assert.All(chunks, c =>
        {
            Assert.True(c.NoiDung.Length <= 3200);
            Assert.EndsWith(".", c.NoiDung);
        });
    }
}
