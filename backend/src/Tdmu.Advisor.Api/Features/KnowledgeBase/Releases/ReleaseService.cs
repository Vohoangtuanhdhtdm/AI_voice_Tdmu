using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Tdmu.Advisor.Api.Features.KnowledgeBase.Entities;
using Tdmu.Advisor.Api.Infrastructure;
using Tdmu.Advisor.Api.Infrastructure.Auditing;

namespace Tdmu.Advisor.Api.Features.KnowledgeBase.Releases;

public class ReleaseService(AdvisorDbContext db, AuditSuppression suppression)
{
    private const long LockKey = 727001;

    /// <summary>Chỉ dùng cho test: chạy ngay sau khi xuất bản/khôi phục đã giữ khóa (transaction còn mở).</summary>
    public Func<CancellationToken, Task>? AfterTablesLocked { get; set; }

    /// <summary>Thao tác trên một loại tri thức (gồm TaiLieu, TaiLieuChunk, không chỉ 10 loại có CRUD).</summary>
    private abstract class KbSet
    {
        public abstract string Name { get; }
        public abstract Task<List<KbEntity>> LoadAsync(AdvisorDbContext db, Guid? releaseId, CancellationToken ct);
        public abstract Task DeleteDraftAsync(AdvisorDbContext db, CancellationToken ct);
        public abstract void Add(AdvisorDbContext db, IEnumerable<KbEntity> rows);
        public abstract string TableName(AdvisorDbContext db);
    }

    private sealed class KbSet<T> : KbSet where T : KbEntity
    {
        public override string Name => typeof(T).Name;
        public override async Task<List<KbEntity>> LoadAsync(AdvisorDbContext db, Guid? releaseId, CancellationToken ct) =>
            (await db.Set<T>().AsNoTracking().Where(x => x.ReleaseId == releaseId).ToListAsync(ct)).Cast<KbEntity>().ToList();
        public override Task DeleteDraftAsync(AdvisorDbContext db, CancellationToken ct) =>
            db.Set<T>().Where(x => x.ReleaseId == null).ExecuteDeleteAsync(ct);
        public override void Add(AdvisorDbContext db, IEnumerable<KbEntity> rows) => db.Set<T>().AddRange(rows.Cast<T>());
        public override string TableName(AdvisorDbContext db) => db.Model.FindEntityType(typeof(T))!.GetTableName()!;
    }

    private static readonly KbSet[] Sets =
    [
        new KbSet<MuaTuyenSinh>(), new KbSet<Nganh>(), new KbSet<PhuongThuc>(), new KbSet<ToHop>(), new KbSet<NganhToHop>(),
        new KbSet<DiemChuan>(), new KbSet<ChiTieu>(), new KbSet<HocPhi>(), new KbSet<MocThoiGian>(), new KbSet<Faq>(),
        new KbSet<TaiLieu>(), new KbSet<TaiLieuChunk>(),
    ];

    public async Task<ReleaseDiff> DiffAsync(CancellationToken ct)
    {
        var activeId = await db.Release.Where(r => r.IsActive).Select(r => (Guid?)r.Id).FirstOrDefaultAsync(ct);
        var draftLookup = await KbLabelLookupFactory.BuildAsync(db, null, ct);
        var activeLookup = activeId is null ? null : await KbLabelLookupFactory.BuildAsync(db, activeId, ct);
        var items = new List<DiffItem>();
        foreach (var set in Sets)
        {
            var drafts = await set.LoadAsync(db, null, ct);
            var active = activeId is null ? [] : await set.LoadAsync(db, activeId, ct);
            var activeByLid = active.ToDictionary(x => x.LogicalId);
            foreach (var d in drafts)
            {
                if (!activeByLid.TryGetValue(d.LogicalId, out var a))
                    items.Add(new(set.Name, d.LogicalId, DiffKind.Added, d.Label(draftLookup)));
                else if (!Equals(d.ContentKey(), a.ContentKey()))
                    items.Add(new(set.Name, d.LogicalId, DiffKind.Modified, d.Label(draftLookup)));
            }
            var draftLids = drafts.Select(x => x.LogicalId).ToHashSet();
            foreach (var a in active.Where(a => !draftLids.Contains(a.LogicalId)))
                items.Add(new(set.Name, a.LogicalId, DiffKind.Removed, a.Label(activeLookup!)));
        }
        return new ReleaseDiff(items);
    }

    public async Task<Release> PublishAsync(string ghiChu, string userId, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await LockAsync(ct);
        await LockTablesAsync("SHARE", ct);
        if (AfterTablesLocked is { } hook) await hook(ct);
        var diff = await DiffAsync(ct); // Tính lại sau khi giữ khóa: yêu cầu đến sau sẽ thấy release vừa tạo.
        if (diff.Items.Count == 0) throw new NoChangesException();

        var release = new Release
        {
            SoHieu = (await db.Release.MaxAsync(r => (int?)r.SoHieu, ct) ?? 0) + 1,
            GhiChu = ghiChu, TaoBoi = userId, TaoLuc = DateTimeOffset.UtcNow, IsActive = false,
        };
        db.Release.Add(release);
        await db.SaveChangesAsync(ct);

        await CopyAsync(null, release.Id, ct);
        await SetActiveAsync(release.Id, ct);
        release.IsActive = true;
        await AuditAsync("Published", release, userId, new { release.SoHieu, ghiChu, soThayDoi = diff.Items.Count }, ct);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return release;
    }

    public async Task ActivateAsync(Guid releaseId, string userId, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await LockAsync(ct);
        var release = await db.Release.FirstOrDefaultAsync(r => r.Id == releaseId, ct) ?? throw new ReleaseNotFoundException();
        await SetActiveAsync(release.Id, ct);
        release.IsActive = true;
        await AuditAsync("Activated", release, userId, new { release.SoHieu }, ct);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }

    public async Task RestoreToDraftAsync(Guid releaseId, string userId, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await LockAsync(ct);
        await LockTablesAsync("SHARE ROW EXCLUSIVE", ct);
        if (AfterTablesLocked is { } hook) await hook(ct);
        var release = await db.Release.AsNoTracking().FirstOrDefaultAsync(r => r.Id == releaseId, ct) ?? throw new ReleaseNotFoundException();
        foreach (var set in Sets) await set.DeleteDraftAsync(db, ct);
        using (suppression.Begin()) // Không ghi audit từng dòng: chỉ một dòng "RestoredToDraft".
        {
            await CopyAsync(releaseId, null, ct);
            await AuditAsync("RestoredToDraft", release, userId, new { release.SoHieu }, ct);
            await db.SaveChangesAsync(ct);
        }
        await tx.CommitAsync(ct);
    }

    private Task LockAsync(CancellationToken ct) =>
        db.Database.ExecuteSqlAsync($"SELECT pg_advisory_xact_lock({LockKey})", ct);

    /// <summary>
    /// Khóa cả 12 bảng tri thức tới hết transaction để bản chụp không bị lẫn giữa chừng (READ COMMITTED đọc mỗi câu lệnh một snapshot,
    /// còn CRUD/import không lấy advisory lock). Gọi sau advisory lock nên chỉ một thao tác release giữ khóa bảng tại một thời điểm.
    /// <list type="bullet">
    /// <item>Xuất bản: SHARE — chặn mọi INSERT/UPDATE/DELETE của transaction khác (ROW EXCLUSIVE xung đột với SHARE) nhưng vẫn cho đọc;
    /// chính transaction này vẫn chèn được bản sao release vì khóa của cùng một transaction không xung đột với nhau.</item>
    /// <item>Khôi phục: SHARE ROW EXCLUSIVE — cũng chặn ghi từ nơi khác và cho đọc, đồng thời tự xung đột với chính nó, nên dù có hai
    /// transaction cùng giữ khóa bảng thì cũng không thể cùng giữ SHARE rồi nâng lên ROW EXCLUSIVE (kiểu deadlock nâng khóa).
    /// Transaction khôi phục tự xóa/chèn bản nháp mà không bị chặn bởi khóa của chính nó.</item>
    /// </list>
    /// Không có deadlock với CRUD/import: chúng chỉ ghi vào một bảng tri thức mỗi transaction (cộng audit_log, không nằm trong danh sách),
    /// nên chỉ phải chờ, không giữ khóa nào mà transaction release cần sau đó.
    /// </summary>
    private Task LockTablesAsync(string mode, CancellationToken ct)
    {
        var tables = string.Join(", ", Sets.Select(s => "\"" + s.TableName(db) + "\""));
        // Tên bảng lấy từ model EF và mode là hằng số trong lớp này, không có dữ liệu người dùng nên ghép chuỗi là an toàn.
        return db.Database.ExecuteSqlRawAsync("LOCK TABLE " + tables + " IN " + mode + " MODE", ct);
    }

    /// <summary>Tắt mọi release khác trước rồi bật release này (index unique một-active kiểm tra từng câu lệnh).</summary>
    private async Task SetActiveAsync(Guid releaseId, CancellationToken ct)
    {
        await db.Release.Where(r => r.IsActive && r.Id != releaseId).ExecuteUpdateAsync(s => s.SetProperty(r => r.IsActive, false), ct);
        await db.Release.Where(r => r.Id == releaseId).ExecuteUpdateAsync(s => s.SetProperty(r => r.IsActive, true), ct);
    }

    /// <summary>Sao chép toàn bộ dòng của scope nguồn sang scope đích: Id mới, giữ LogicalId, Embedding, Tsv.</summary>
    private async Task CopyAsync(Guid? fromReleaseId, Guid? toReleaseId, CancellationToken ct)
    {
        foreach (var set in Sets)
        {
            var rows = await set.LoadAsync(db, fromReleaseId, ct);
            foreach (var r in rows) { r.Id = Guid.NewGuid(); r.ReleaseId = toReleaseId; }
            set.Add(db, rows);
        }
    }

    private Task AuditAsync(string action, Release release, string userId, object after, CancellationToken ct)
    {
        db.AuditLog.Add(new AuditLog
        {
            Entity = nameof(Release), EntityLogicalId = release.Id, Action = action,
            AfterJson = JsonSerializer.Serialize(after), UserId = userId, At = DateTimeOffset.UtcNow,
        });
        return Task.CompletedTask;
    }
}
