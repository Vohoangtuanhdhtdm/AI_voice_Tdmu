using System.Globalization;
using System.Text.RegularExpressions;

namespace Tdmu.Advisor.Api.Features.KnowledgeBase.Import;

/// <summary>Đọc số từ file Excel/CSV của Việt Nam: chấp nhận dấu thập phân là ',' hoặc '.', không dùng culture của máy.</summary>
public static partial class NumberParser
{
    [GeneratedRegex(@"^[+-]?[0-9]+([.,][0-9]+)?$")]
    private static partial Regex Pattern();

    /// <summary>Từ chối chuỗi rỗng, chữ, và số có cả dấu phân nhóm lẫn dấu thập phân ("1.234,5").</summary>
    public static bool TryParseDecimal(string? raw, out decimal value)
    {
        value = 0;
        var s = raw?.Trim();
        if (string.IsNullOrEmpty(s) || !Pattern().IsMatch(s)) return false;
        return decimal.TryParse(s.Replace(',', '.'), NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
            CultureInfo.InvariantCulture, out value);
    }
}
