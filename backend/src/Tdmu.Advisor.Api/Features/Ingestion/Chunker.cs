using System.Text;

namespace Tdmu.Advisor.Api.Features.Ingestion;

public record ChunkDraft(int ThuTu, string NoiDung, string ViTri);

/// <summary>Chia các khối văn bản thành chunk ~2.400 ký tự (tối đa 3.200), chồng lấn 15% và không vượt qua ranh giới tiêu đề.</summary>
public static class Chunker
{
    public const int TargetChars = 2400;
    public const int MaxChars = 3200;
    private const double OverlapRatio = 0.15;
    private const string Separator = "\n\n";

    public static IReadOnlyList<ChunkDraft> Split(IReadOnlyList<ExtractedBlock> blocks)
    {
        var result = new List<ChunkDraft>();
        var current = new StringBuilder();
        var hasContent = false; // false khi current chỉ chứa phần chồng lấn.
        string? heading = null;
        int? page = null;
        var started = false;

        void Flush(bool keepOverlap)
        {
            if (!hasContent) { current.Clear(); return; }
            var text = current.ToString();
            result.Add(new ChunkDraft(result.Count + 1, text, ViTri(heading, page)));
            current.Clear();
            hasContent = false;
            if (keepOverlap) current.Append(OverlapTail(text));
        }

        foreach (var block in blocks)
        {
            var text = block.Text.Trim();
            if (text.Length == 0) continue;
            if (started && block.TieuDe != heading) Flush(keepOverlap: false);
            started = true;
            heading = block.TieuDe;

            foreach (var unit in SplitLong(text))
            {
                if (hasContent && current.Length + Separator.Length + unit.Length > MaxChars) Flush(keepOverlap: true);
                if (!hasContent) page = block.Trang;
                if (current.Length > 0) current.Append(Separator);
                current.Append(unit);
                hasContent = true;
                if (current.Length >= TargetChars) Flush(keepOverlap: true);
            }
        }
        Flush(keepOverlap: false);
        return result;
    }

    private static string ViTri(string? heading, int? page) => (heading, page) switch
    {
        ({ Length: > 0 } h, { } p) => $"{h} – trang {p}",
        ({ Length: > 0 } h, null) => h,
        (_, { } p) => $"Trang {p}",
        _ => "",
    };

    /// <summary>15% cuối của chunk, bắt đầu ở ranh giới từ.</summary>
    private static string OverlapTail(string text)
    {
        var n = (int)(text.Length * OverlapRatio);
        if (n == 0) return "";
        var start = text.Length - n;
        if (start > 0 && !char.IsWhiteSpace(text[start - 1]))
        {
            var ws = text.IndexOfAny([' ', '\n', '\t'], start);
            if (ws < 0) return ""; // Không có ranh giới từ trong đoạn cuối.
            start = ws + 1;
        }
        var tail = text[start..].TrimStart();
        return tail.Length == 0 ? "" : tail;
    }

    /// <summary>Đoạn dài hơn <see cref="TargetChars"/> được cắt ở ranh giới câu (hoặc khoảng trắng nếu không có dấu câu).</summary>
    private static IEnumerable<string> SplitLong(string text)
    {
        while (text.Length > TargetChars)
        {
            var cut = LastBoundary(text, TargetChars);
            yield return text[..cut].Trim();
            text = text[cut..].TrimStart();
        }
        if (text.Length > 0) yield return text;
    }

    private static int LastBoundary(string text, int limit)
    {
        for (var i = limit - 1; i > limit / 2; i--)
            if (text[i] is '.' or '!' or '?' or '\n' && (i + 1 >= text.Length || char.IsWhiteSpace(text[i + 1]))) return i + 1;
        var space = text.LastIndexOf(' ', limit - 1);
        return space > limit / 2 ? space : limit;
    }
}
