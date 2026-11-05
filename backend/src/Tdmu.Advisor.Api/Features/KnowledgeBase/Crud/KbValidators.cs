using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Tdmu.Advisor.Api.Features.KnowledgeBase.Entities;
using Tdmu.Advisor.Api.Infrastructure;

namespace Tdmu.Advisor.Api.Features.KnowledgeBase.Crud;

/// <summary>Gom lỗi theo trường (khóa camelCase) để trả ValidationProblem.</summary>
public sealed class KbErrors
{
    private readonly Dictionary<string, List<string>> _map = [];
    public bool Any => _map.Count > 0;
    public void Add(string field, string message)
    {
        if (!_map.TryGetValue(field, out var l)) _map[field] = l = [];
        l.Add(message);
    }
    public Dictionary<string, string[]> ToDictionary() => _map.ToDictionary(kv => kv.Key, kv => kv.Value.ToArray());
}

/// <summary>Quy tắc validate dùng chung cho CRUD và import CSV.</summary>
public static partial class KbValidators
{
    [GeneratedRegex("^[A-Z][0-9]{2}$")]
    private static partial Regex ToHopMaRegex();

    public static void Required<T>(KbErrors e, T? value, string field, string label) where T : struct
    {
        if (value is null) e.Add(field, $"{label} là bắt buộc");
    }

    public static void Nam(KbErrors e, int? nam, string field = "nam")
    {
        if (nam is null) e.Add(field, "Năm là bắt buộc");
        else if (nam is < 2000 or > 2100) e.Add(field, "Năm phải nằm trong khoảng 2000–2100");
    }

    public static void NotEmpty(KbErrors e, string? value, string field, string label)
    {
        if (string.IsNullOrWhiteSpace(value)) e.Add(field, $"{label} không được để trống");
    }

    public static void Positive(KbErrors e, decimal? value, string field, string label)
    {
        if (value is null) e.Add(field, $"{label} là bắt buộc");
        else if (value <= 0) e.Add(field, $"{label} phải lớn hơn 0");
    }

    public static void ToHopMa(KbErrors e, string? ma)
    {
        if (ma is null || !ToHopMaRegex().IsMatch(ma.Trim())) e.Add("ma", "Mã tổ hợp phải gồm 1 chữ cái in hoa và 2 chữ số (ví dụ A00)");
    }

    /// <summary>Lid phải trỏ tới một bản ghi trong bản nháp. Trả về bản ghi để validator dùng tiếp, null nếu thiếu.</summary>
    public static async Task<T?> RefAsync<T>(KbErrors e, AdvisorDbContext db, Guid? lid, string field, string label, CancellationToken ct, bool required = true)
        where T : KbEntity
    {
        if (lid is null || lid == Guid.Empty)
        {
            if (required) e.Add(field, $"{label} là bắt buộc");
            return null;
        }
        var row = await db.Set<T>().AsNoTracking().FirstOrDefaultAsync(x => x.ReleaseId == null && x.LogicalId == lid, ct);
        if (row is null) e.Add(field, $"{label} không tồn tại trong bản nháp");
        return row;
    }

    public static void Diem(KbErrors e, decimal? diem, PhuongThuc? phuongThuc)
    {
        if (diem is null) e.Add("diem", "Điểm là bắt buộc");
        else if (diem < 0) e.Add("diem", "Điểm phải lớn hơn hoặc bằng 0");
        else if (phuongThuc is not null && diem > phuongThuc.ThangDiem)
            e.Add("diem", $"Điểm không được vượt quá thang điểm của phương thức ({phuongThuc.ThangDiem:0.##})");
    }
}
