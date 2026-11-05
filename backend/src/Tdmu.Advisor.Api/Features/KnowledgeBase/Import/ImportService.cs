using System.Globalization;
using System.Text;
using ClosedXML.Excel;
using CsvHelper;
using Microsoft.EntityFrameworkCore;
using Tdmu.Advisor.Api.Features.KnowledgeBase.Crud;
using Tdmu.Advisor.Api.Features.KnowledgeBase.Entities;
using Tdmu.Advisor.Api.Infrastructure;

namespace Tdmu.Advisor.Api.Features.KnowledgeBase.Import;

/// <summary>Đọc CSV/Excel, kiểm tra từng dòng bằng validator của CRUD, rồi upsert vào bản nháp.</summary>
public sealed class ImportService(AdvisorDbContext db)
{
    public const int MaxBytes = 5 * 1024 * 1024;

    public sealed record Parsed(ImportPreview Preview, IReadOnlyList<object> Dtos);

    public static bool IsKnownKind(string kind) => Handlers.ContainsKey(kind);

    public static (string[] Columns, object?[] Example) Template(string kind)
    {
        var h = Handlers[kind];
        return (h.Columns, h.Example);
    }

    public async Task<Parsed> PreviewAsync(string kind, Stream content, string fileName, CancellationToken ct)
    {
        var handler = Handlers[kind];
        var table = fileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase) ? ReadXlsx(content) : ReadCsv(content);

        var index = new Dictionary<string, int>();
        for (var i = 0; i < table.Headers.Count; i++) index.TryAdd(table.Headers[i], i);
        var missing = handler.Required.Where(c => !index.ContainsKey(c)).ToList();
        if (missing.Count > 0) throw new ImportFileException($"File thiếu cột bắt buộc: {string.Join(", ", missing)}");

        var lookups = new Lookups(
            await db.Nganh.AsNoTracking().Where(x => x.ReleaseId == null).ToDictionaryAsync(x => x.MaNganh.Trim(), x => x.LogicalId, StringComparer.OrdinalIgnoreCase, ct),
            await db.PhuongThuc.AsNoTracking().Where(x => x.ReleaseId == null).ToDictionaryAsync(x => x.Ma.Trim(), x => x.LogicalId, StringComparer.OrdinalIgnoreCase, ct),
            await db.ToHop.AsNoTracking().Where(x => x.ReleaseId == null).ToDictionaryAsync(x => x.Ma.Trim(), x => x.LogicalId, StringComparer.OrdinalIgnoreCase, ct));

        var rows = new List<ImportRow>();
        var dtos = new List<object>();
        var seen = new Dictionary<object, int>();
        foreach (var (rowNumber, cells) in table.Rows)
        {
            var values = new Dictionary<string, string?>();
            foreach (var col in handler.Required.Concat(handler.Optional))
            {
                var raw = index.TryGetValue(col, out var ix) && ix < cells.Length ? cells[ix]?.Trim() : null;
                values[col] = string.IsNullOrEmpty(raw) ? null : raw;
            }
            var ctx = new RowContext(values, lookups);
            var (dto, key) = await handler.CheckAsync(ctx, db, ct);
            if (dto is not null && key is not null)
            {
                if (seen.TryGetValue(key, out var first)) { ctx.Errors.Add($"Trùng khóa với dòng {first} trong cùng file"); dto = null; }
                else seen[key] = rowNumber;
            }
            if (dto is not null && ctx.Errors.Count == 0) dtos.Add(dto);
            rows.Add(new ImportRow(rowNumber, values, ctx.Errors));
        }
        var errorCount = rows.Count(r => r.Errors.Count > 0);
        return new Parsed(new ImportPreview(kind, rows, rows.Count - errorCount, errorCount), dtos);
    }

    public async Task<ImportResult> CommitAsync(string kind, IReadOnlyList<object> dtos, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var result = await Handlers[kind].ApplyAsync(db, dtos, ct);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return result;
    }

    // ---- đọc file ----

    private sealed record Table(List<string> Headers, List<(int RowNumber, string?[] Cells)> Rows);

    private static string NormalizeHeader(string? h) => (h ?? "").Trim().TrimStart('﻿').Trim().TrimEnd('?').ToLowerInvariant();

    private static Table ReadCsv(Stream content)
    {
        string text;
        using (var reader = new StreamReader(content, new UTF8Encoding(false), detectEncodingFromByteOrderMarks: true))
            text = reader.ReadToEnd().TrimStart('﻿');

        var firstLine = text.Split('\n', 2)[0];
        var delimiter = firstLine.Count(ch => ch == ';') > firstLine.Count(ch => ch == ',') ? ';' : ',';
        var options = new CsvReaderOptions
        {
            Separator = delimiter,
            HasHeader = false,
            SkipEmptyLines = false,
            FormatProvider = CultureInfo.InvariantCulture,
        };
        var headers = new List<string>();
        var rows = new List<(int, string?[])>();
        try
        {
            using var csv = CsvReader.Create(new StringReader(text), options);
            var row = 0;
            while (csv.Read())
            {
                row++;
                var rec = csv.Current.ToArray();
                if (row == 1) { headers = rec.Select(NormalizeHeader).ToList(); continue; }
                if (rec.All(string.IsNullOrWhiteSpace)) continue;
                rows.Add((row, rec));
            }
        }
        catch (Exception ex) when (ex is not ImportFileException)
        {
            throw new ImportFileException("File CSV không đúng định dạng");
        }
        return new Table(headers, rows);
    }

    private static Table ReadXlsx(Stream content)
    {
        try
        {
            using var wb = new XLWorkbook(content);
            var ws = wb.Worksheets.First();
            var lastCol = ws.Row(1).LastCellUsed()?.Address.ColumnNumber ?? 0;
            var headers = Enumerable.Range(1, lastCol).Select(c => NormalizeHeader(ws.Cell(1, c).GetString())).ToList();
            var rows = new List<(int, string?[])>();
            var lastRow = ws.LastRowUsed()?.RowNumber() ?? 1;
            for (var r = 2; r <= lastRow; r++)
            {
                var cells = Enumerable.Range(1, lastCol).Select(c => CellText(ws.Cell(r, c))).ToArray();
                if (cells.All(string.IsNullOrWhiteSpace)) continue;
                rows.Add((r, cells));
            }
            return new Table(headers, rows);
        }
        catch (Exception ex) when (ex is not ImportFileException)
        {
            throw new ImportFileException("Không đọc được file Excel (.xlsx)");
        }
    }

    private static string? CellText(IXLCell cell)
    {
        if (cell.IsEmpty()) return null;
        return cell.DataType == XLDataType.Number
            ? ((decimal)cell.GetDouble()).ToString(CultureInfo.InvariantCulture)
            : cell.GetString();
    }

    // ---- kiểm tra từng dòng ----

    private sealed record Lookups(Dictionary<string, Guid> Nganh, Dictionary<string, Guid> PhuongThuc, Dictionary<string, Guid> ToHop);

    private sealed class RowContext(Dictionary<string, string?> values, Lookups lookups)
    {
        public List<string> Errors { get; } = [];
        public Lookups L => lookups;

        public string? Text(string col) => values[col];

        public Guid? Ref(string col, Dictionary<string, Guid> map, string label, bool required = true)
        {
            var raw = values[col];
            if (raw is null) { if (required) Errors.Add($"Cột {col} là bắt buộc"); return null; }
            if (map.TryGetValue(raw, out var lid)) return lid;
            Errors.Add($"{label} '{raw}' không có trong bản nháp");
            return null;
        }

        public decimal? Dec(string col, bool required = true)
        {
            var raw = values[col];
            if (raw is null) { if (required) Errors.Add($"Cột {col} là bắt buộc"); return null; }
            if (!NumberParser.TryParseDecimal(raw, out var v)) { Errors.Add($"Cột {col}: '{raw}' không phải là số hợp lệ"); return null; }
            values[col] = v.ToString(CultureInfo.InvariantCulture);
            return v;
        }

        public int? Int(string col, bool required = true)
        {
            var v = Dec(col, required);
            if (v is null) return null;
            if (v != decimal.Truncate(v.Value) || Math.Abs(v.Value) > int.MaxValue) { Errors.Add($"Cột {col}: '{values[col]}' phải là số nguyên"); return null; }
            return (int)v.Value;
        }

        public DonViHocPhi? DonVi(string col)
        {
            var raw = values[col];
            if (raw is null) { Errors.Add($"Cột {col} là bắt buộc"); return null; }
            switch (raw.ToLowerInvariant())
            {
                case "tin_chi": return DonViHocPhi.TinChi;
                case "nam": return DonViHocPhi.Nam;
                case "hoc_ky": return DonViHocPhi.HocKy;
                default: Errors.Add($"Cột {col}: '{raw}' không hợp lệ (chỉ nhận tin_chi, nam, hoc_ky)"); return null;
            }
        }
    }

    /// <summary>Cột theo thứ tự của file mẫu; hậu tố "?" là cột tùy chọn.</summary>
    private abstract class Handler(string[] columns, object?[] example)
    {
        public string[] Required { get; } = columns.Where(c => !c.EndsWith('?')).ToArray();
        public string[] Optional { get; } = columns.Where(c => c.EndsWith('?')).Select(c => c.TrimEnd('?')).ToArray();
        public string[] Columns { get; } = columns.Select(c => c.TrimEnd('?')).ToArray();
        public object?[] Example => example;
        public abstract Task<(object? Dto, object? Key)> CheckAsync(RowContext ctx, AdvisorDbContext db, CancellationToken ct);
        public abstract Task<ImportResult> ApplyAsync(AdvisorDbContext db, IReadOnlyList<object> dtos, CancellationToken ct);
    }

    private sealed class Handler<TEntity, TDto>(
        KbEntityDefinition<TEntity, TDto> def,
        string[] columns, object?[] example,
        Func<RowContext, TDto> build,
        Func<TDto, object> dtoKey,
        Func<TEntity, object> entityKey,
        bool insertOnly = false) : Handler(columns, example)
        where TEntity : KbEntity, new() where TDto : class
    {
        public override async Task<(object? Dto, object? Key)> CheckAsync(RowContext ctx, AdvisorDbContext db, CancellationToken ct)
        {
            var dto = build(ctx);
            if (ctx.Errors.Count > 0) return (null, null); // lỗi đọc/tra mã: chưa validate tiếp
            var errors = await def.Validate(dto, db, ct);
            if (errors.Any)
            {
                ctx.Errors.AddRange(errors.ToDictionary().Values.SelectMany(x => x));
                return (null, null);
            }
            return (dto, dtoKey(dto));
        }

        public override async Task<ImportResult> ApplyAsync(AdvisorDbContext db, IReadOnlyList<object> dtos, CancellationToken ct)
        {
            var existing = new Dictionary<object, TEntity>();
            foreach (var e in await db.Set<TEntity>().Where(x => x.ReleaseId == null).OrderBy(x => x.CapNhatLuc).ToListAsync(ct))
                existing.TryAdd(entityKey(e), e);

            int inserted = 0, updated = 0;
            foreach (TDto dto in dtos)
            {
                var key = dtoKey(dto);
                if (existing.TryGetValue(key, out var e))
                {
                    if (insertOnly) continue;
                    var before = e.ContentKey();
                    def.Apply(dto, e);
                    if (!Equals(before, e.ContentKey())) { e.CapNhatLuc = DateTimeOffset.UtcNow; updated++; }
                }
                else
                {
                    e = new TEntity();
                    def.Apply(dto, e);
                    e.CapNhatLuc = DateTimeOffset.UtcNow;
                    db.Add(e);
                    existing[key] = e;
                    inserted++;
                }
            }
            return new ImportResult(inserted, updated);
        }
    }

    private static readonly Dictionary<string, Handler> Handlers = new()
    {
        ["diem-chuan"] = new Handler<DiemChuan, DiemChuanDto>(KbEntityDefinitions.DiemChuan,
            ["ma_nganh", "nam", "ma_phuong_thuc", "diem", "ghi_chu?", "nguon_ten?", "nguon_url?"],
            ["7480201", 2026, "thi-thpt", 21.5m, "", "Đề án tuyển sinh 2026", ""],
            c => new DiemChuanDto(null, c.Ref("ma_nganh", c.L.Nganh, "Mã ngành"), c.Int("nam"),
                c.Ref("ma_phuong_thuc", c.L.PhuongThuc, "Mã phương thức"), c.Dec("diem"),
                c.Text("ghi_chu"), c.Text("nguon_ten"), c.Text("nguon_url")),
            d => (d.NganhLid!.Value, d.Nam!.Value, d.PhuongThucLid!.Value),
            e => (e.NganhLid, e.Nam, e.PhuongThucLid)),

        ["chi-tieu"] = new Handler<ChiTieu, ChiTieuDto>(KbEntityDefinitions.ChiTieu,
            ["ma_nganh", "nam", "ma_phuong_thuc?", "so_luong", "nguon_ten?", "nguon_url?"],
            ["7480201", 2026, "", 120, "Đề án tuyển sinh 2026", ""],
            c => new ChiTieuDto(null, c.Ref("ma_nganh", c.L.Nganh, "Mã ngành"), c.Int("nam"),
                c.Ref("ma_phuong_thuc", c.L.PhuongThuc, "Mã phương thức", required: false), c.Int("so_luong"),
                c.Text("nguon_ten"), c.Text("nguon_url")),
            d => (d.NganhLid!.Value, d.Nam!.Value, d.PhuongThucLid),
            e => (e.NganhLid, e.Nam, e.PhuongThucLid)),

        ["hoc-phi"] = new Handler<HocPhi, HocPhiDto>(KbEntityDefinitions.HocPhi,
            ["ma_nganh?", "nam", "muc", "don_vi", "ghi_chu?", "nguon_ten?", "nguon_url?"],
            ["", 2026, 450000, "tin_chi", "", "Thông báo học phí 2026", ""],
            c => new HocPhiDto(null, c.Ref("ma_nganh", c.L.Nganh, "Mã ngành", required: false), c.Int("nam"),
                c.Dec("muc"), c.DonVi("don_vi"), c.Text("ghi_chu"), c.Text("nguon_ten"), c.Text("nguon_url")),
            d => (d.NganhLid, d.Nam!.Value, d.DonVi!.Value),
            e => (e.NganhLid, e.Nam, e.DonVi)),

        ["nganh-to-hop"] = new Handler<NganhToHop, NganhToHopDto>(KbEntityDefinitions.NganhToHop,
            ["ma_nganh", "nam", "ma_to_hop"],
            ["7480201", 2026, "A00"],
            c => new NganhToHopDto(null, c.Ref("ma_nganh", c.L.Nganh, "Mã ngành"), c.Int("nam"),
                c.Ref("ma_to_hop", c.L.ToHop, "Mã tổ hợp"), null, null),
            d => (d.NganhLid!.Value, d.Nam!.Value, d.ToHopLid!.Value),
            e => (e.NganhLid, e.Nam, e.ToHopLid),
            insertOnly: true),
    };
}
