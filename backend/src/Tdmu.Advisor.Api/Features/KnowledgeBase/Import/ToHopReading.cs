using System.Text;

namespace Tdmu.Advisor.Api.Features.KnowledgeBase.Import;

/// <summary>Sinh cách đọc tổ hợp cho giọng nói: chữ cái giữ nguyên, từng chữ số đọc riêng ("A00" → "A không không").</summary>
public static class ToHopReading
{
    private static readonly string[] Digits = ["không", "một", "hai", "ba", "bốn", "năm", "sáu", "bảy", "tám", "chín"];

    public static string Generate(string ma)
    {
        var parts = new List<string>();
        foreach (var ch in ma.Trim())
            parts.Add(ch is >= '0' and <= '9' ? Digits[ch - '0'] : ch.ToString());
        return string.Join(' ', parts);
    }
}
