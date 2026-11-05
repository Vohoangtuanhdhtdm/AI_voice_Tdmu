using System.Globalization;
using System.Text.RegularExpressions;

namespace Tdmu.Advisor.Api.Features.KnowledgeBase.Import;

/// <summary>Đọc số từ file Excel/CSV của Việt Nam: chấp nhận dấu thập phân là ',' hoặc '.', không dùng culture của máy.</summary>
public static partial class NumberParser
{
    [GeneratedRegex(@"^[+-]?[0-9]+([.,][0-9]+)?$")]
    private static partial Regex Pattern();

    [GeneratedRegex(@"^([0-9]+|[0-9]{1,3}(\.[0-9]{3})+|[0-9]{1,3}(,[0-9]{3})+)$")]
    private static partial Regex MoneyPattern();

    /// <summary>Số tiền nguyên: "450000", "450.000", "1,200,000". Dấu '.' hoặc ',' chỉ là dấu nhóm nghìn; từ chối phần thập phân, dấu lẫn lộn, số âm.</summary>
    public static bool TryParseMoney(string? raw, out decimal value)
    {
        value = 0;
        var s = raw?.Trim();
        if (string.IsNullOrEmpty(s) || !MoneyPattern().IsMatch(s)) return false;
        return decimal.TryParse(s.Replace(".", "").Replace(",", ""), NumberStyles.None, CultureInfo.InvariantCulture, out value);
    }

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
