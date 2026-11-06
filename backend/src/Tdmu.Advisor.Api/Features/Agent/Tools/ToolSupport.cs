using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Tdmu.Advisor.Api.Features.KnowledgeBase.Query;

namespace Tdmu.Advisor.Api.Features.Agent.Tools;

/// <summary>Đọc tham số do mô hình sinh: số có thể là số JSON hoặc chuỗi ("2026"), chuỗi rỗng coi như không truyền.</summary>
public static class ToolArgs
{
    public static string? Str(JsonObject args, string key)
    {
        if (args[key] is not JsonValue v) return null;
        var s = v.GetValueKind() switch
        {
            JsonValueKind.String => v.GetValue<string>(),
            JsonValueKind.Number => v.ToJsonString(),
            _ => null,
        };
        return string.IsNullOrWhiteSpace(s) ? null : s.Trim();
    }

    public static int? Int(JsonObject args, string key)
    {
        if (args[key] is not JsonValue v) return null;
        if (v.GetValueKind() == JsonValueKind.Number && v.TryGetValue<decimal>(out var d)) return (int)d;
        var s = Str(args, key);
        return int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var i) ? i
            : decimal.TryParse(s, NumberStyles.Number, CultureInfo.InvariantCulture, out var dd) ? (int)dd : null;
    }
}

public static class ToolText
{
    /// <summary>Chữ thường, bỏ dấu tiếng Việt (đ → d), gộp khoảng trắng.</summary>
    public static string Fold(string s)
    {
        var sb = new StringBuilder(s.Length);
        foreach (var ch in s.Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark) continue;
            sb.Append(ch switch { 'đ' => 'd', 'Đ' => 'd', _ => char.ToLowerInvariant(ch) });
        }
        return string.Join(' ', sb.ToString().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }

    public static string Date(DateTimeOffset t) => TimeZoneInfo.ConvertTime(t, VietnamTime).ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture);

    private static readonly TimeZoneInfo VietnamTime = FindVietnamTime();

    private static TimeZoneInfo FindVietnamTime()
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh"); }
        catch (Exception e) when (e is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            return TimeZoneInfo.CreateCustomTimeZone("Asia/Ho_Chi_Minh", TimeSpan.FromHours(7), "Giờ Việt Nam", "Giờ Việt Nam");
        }
    }
}

/// <summary>Khung FunctionDeclaration của Gemini.</summary>
public static class ToolDeclaration
{
    public static JsonObject Create(string name, string description, params (string Name, string Type, string Description, bool Required)[] parameters)
    {
        var props = new JsonObject();
        foreach (var p in parameters)
            props[p.Name] = new JsonObject { ["type"] = p.Type, ["description"] = p.Description };
        var schema = new JsonObject { ["type"] = "object", ["properties"] = props };
        var required = parameters.Where(p => p.Required).Select(p => (JsonNode)p.Name).ToArray();
        if (required.Length > 0) schema["required"] = new JsonArray(required);
        return new JsonObject { ["name"] = name, ["description"] = description, ["parameters"] = schema };
    }

    public const string NamDescription = "Năm tuyển sinh, ví dụ 2026. Bỏ trống để dùng mùa tuyển sinh hiện tại.";
    public const string NganhDescription = "Tên ngành, tên gọi khác, viết tắt hoặc mã ngành như người dùng nói (có dấu hay không đều được).";
    public const string PhuongThucDescription = "Mã hoặc tên phương thức xét tuyển, ví dụ THPT, học bạ, đánh giá năng lực. Bỏ trống để lấy mọi phương thức.";
}

/// <summary>Phân giải tham số ngành/phương thức/năm dùng chung cho các tool tra cứu.</summary>
public static class ToolLookup
{
    public const string ChonNganhHuongDan = "Có nhiều ngành khớp. Hãy đọc các lựa chọn và hỏi người dùng muốn hỏi ngành nào; không tự chọn.";

    public sealed record Outcome<T>(T? Value, ToolResult? Stop);

    public static async Task<Outcome<NganhMatch>> NganhAsync(IKnowledgeQuery kb, ToolContext ctx, string? nganh, CancellationToken ct)
    {
        if (nganh is null) return new(null, ToolResult.AskBack(null, "Hãy hỏi người dùng muốn hỏi ngành nào."));
        var found = await kb.FindNganhAsync(ctx.Scope, nganh, ct);
        return found.Count switch
        {
            0 => new(null, ToolResult.NotFound("ngành này")),
            1 => new(found[0], null),
            _ => new(null, ToolResult.AskBack(LuaChonNganh(found), ChonNganhHuongDan)),
        };
    }

    /// <summary><c>{ ma_nganh, ten }</c> của ngành đã phân giải, để mô hình thấy đang trả lời cho ngành nào.</summary>
    public static JsonObject NganhJson(NganhMatch n) => new() { ["ma_nganh"] = n.MaNganh, ["ten"] = n.Ten };

    /// <summary>Hướng dẫn xác nhận khi ngành chỉ khớp gần đúng; null nếu khớp chắc chắn.</summary>
    public static string? XacNhanNganh(NganhMatch n, string tuKhoa) => n.Kind == NganhMatchKind.Similar
        ? $"Đây là ngành gần đúng nhất với từ người dùng nói ('{tuKhoa}'): {n.Ten}. Hãy xác nhận đúng ngành với người dùng trước khi đọc số liệu."
        : null;

    public static string? Join(params string?[] huongDan)
    {
        var parts = huongDan.Where(h => !string.IsNullOrWhiteSpace(h)).ToList();
        return parts.Count == 0 ? null : string.Join(' ', parts);
    }

    public static JsonObject LuaChonNganh(IEnumerable<NganhMatch> found) => new()
    {
        ["lua_chon"] = new JsonArray(found.Select(n => (JsonNode)new JsonObject { ["ma_nganh"] = n.MaNganh, ["ten"] = n.Ten }).ToArray()),
    };

    /// <summary>Không truyền thì Value = null (mọi phương thức). Khớp mã (không phân biệt hoa thường) hoặc tên (bỏ dấu, chứa nhau).</summary>
    public static async Task<Outcome<PhuongThucRef>> PhuongThucAsync(IKnowledgeQuery kb, ToolContext ctx, string? phuongThuc, CancellationToken ct)
    {
        if (phuongThuc is null) return new(null, null);
        var all = await kb.ListPhuongThucAsync(ctx.Scope, ct);
        var q = ToolText.Fold(phuongThuc);
        var byMa = all.Where(p => ToolText.Fold(p.Ma) == q).ToList();
        var matches = byMa.Count > 0 ? byMa
            : all.Where(p => { var ten = ToolText.Fold(p.Ten); return ten.Contains(q) || q.Contains(ten); }).ToList();
        if (matches.Count == 1) return new(matches[0], null);
        if (all.Count == 0) return new(null, ToolResult.NotFound("phương thức xét tuyển này"));
        var options = new JsonObject
        {
            ["lua_chon"] = new JsonArray((matches.Count > 1 ? matches : all)
                .Select(p => (JsonNode)new JsonObject { ["ma"] = p.Ma, ["ten"] = p.Ten }).ToArray()),
        };
        return new(null, ToolResult.AskBack(options,
            "Không xác định được phương thức xét tuyển người dùng nói. Hãy đọc các phương thức có trong dữ liệu và hỏi người dùng muốn hỏi phương thức nào."));
    }

    public static async Task<Outcome<int?>> NamAsync(IKnowledgeQuery kb, ToolContext ctx, JsonObject args, CancellationToken ct)
    {
        var nam = ToolArgs.Int(args, "nam") ?? await kb.CurrentYearAsync(ctx.Scope, ct);
        return nam is null
            ? new(null, ToolResult.AskBack(null, "Chưa có mùa tuyển sinh hiện tại trong dữ liệu. Hãy hỏi người dùng muốn xem năm nào."))
            : new(nam, null);
    }
}
