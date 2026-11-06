using System.Globalization;
using System.Text;

namespace Tdmu.Advisor.Api.Features.Agent.Tools;

/// <summary>Đọc số thành chữ tiếng Việt cho <c>cach_doc</c> (giọng nói đọc đúng số, không tự đoán).</summary>
public static class VietnameseNumberReader
{
    private static readonly string[] Digits = ["không", "một", "hai", "ba", "bốn", "năm", "sáu", "bảy", "tám", "chín"];
    private static readonly string[] GroupUnits = ["", "nghìn", "triệu"];
    private const decimal Billion = 1_000_000_000m;

    /// <summary>Phần nguyên đọc theo hàng; phần thập phân (bỏ số 0 cuối) đọc sau "phẩy": số 0 đứng đầu đọc từng chữ, phần còn lại đọc như một số.</summary>
    public static string Read(decimal n)
    {
        var sb = new StringBuilder();
        if (n < 0) { sb.Append("âm "); n = -n; }
        var integer = decimal.Truncate(n);
        sb.Append(ReadInteger(integer));

        var frac = (n - integer).ToString(CultureInfo.InvariantCulture); // "0.75"
        var dot = frac.IndexOf('.');
        if (dot >= 0)
        {
            var digits = frac[(dot + 1)..].TrimEnd('0');
            if (digits.Length > 0)
            {
                sb.Append(" phẩy");
                var i = 0;
                for (; i < digits.Length - 1 && digits[i] == '0'; i++) sb.Append(" không");
                sb.Append(' ').Append(ReadInteger(decimal.Parse(digits[i..], CultureInfo.InvariantCulture)));
            }
        }
        return sb.ToString();
    }

    public static string ReadMoney(decimal vnd) => Read(vnd) + " đồng";

    private static string ReadInteger(decimal n)
    {
        if (n == 0) return Digits[0];
        if (n >= Billion)
        {
            var head = ReadInteger(decimal.Truncate(n / Billion)) + " tỷ";
            var rest = n % Billion;
            return rest == 0 ? head : head + " " + ReadBelowBillion((long)rest, leading: false);
        }
        return ReadBelowBillion((long)n, leading: true);
    }

    /// <param name="leading">Nhóm đầu tiên của số thì không đọc "không trăm"/"lẻ" ở đầu.</param>
    private static string ReadBelowBillion(long n, bool leading)
    {
        var groups = new List<int>();
        for (var x = n; x > 0; x /= 1000) groups.Add((int)(x % 1000));
        while (groups.Count < 3 && !leading) groups.Add(0);
        var parts = new List<string>();
        var first = true;
        for (var g = groups.Count - 1; g >= 0; g--)
        {
            if (groups[g] == 0) continue;
            var text = ReadGroup(groups[g], full: !(leading && first));
            parts.Add(GroupUnits[g].Length > 0 ? text + " " + GroupUnits[g] : text);
            first = false;
        }
        return string.Join(' ', parts);
    }

    /// <param name="full">Đọc đủ hàng trăm ("không trăm lẻ năm") khi nhóm không đứng đầu số.</param>
    private static string ReadGroup(int n, bool full)
    {
        int h = n / 100, t = n / 10 % 10, u = n % 10;
        var parts = new List<string>();
        if (h > 0 || full) parts.Add(Digits[h] + " trăm");
        if (t == 0)
        {
            if (u > 0) { if (parts.Count > 0) parts.Add("lẻ"); parts.Add(Digits[u]); }
            return string.Join(' ', parts);
        }
        parts.Add(t == 1 ? "mười" : Digits[t] + " mươi");
        if (u == 1 && t >= 2) parts.Add("mốt");
        else if (u == 5) parts.Add("lăm");
        else if (u > 0) parts.Add(Digits[u]);
        return string.Join(' ', parts);
    }
}
