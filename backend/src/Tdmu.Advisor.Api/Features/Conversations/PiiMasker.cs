using System.Text.RegularExpressions;

namespace Tdmu.Advisor.Api.Features.Conversations;

public static partial class PiiMasker
{
    [GeneratedRegex(@"[A-Za-z0-9._%+\-]+@[A-Za-z0-9\-]+(?:\.[A-Za-z0-9\-]+)*\.[A-Za-z]{2,}")]
    private static partial Regex Email();

    [GeneratedRegex(@"(?<!\d)(\+?84|0)(?:[\s.\-]?\d){8,10}(?!\d)")]
    private static partial Regex Phone();

    public static string Mask(string text) =>
        Phone().Replace(Email().Replace(text, "[EMAIL]"), "[SĐT]");
}
