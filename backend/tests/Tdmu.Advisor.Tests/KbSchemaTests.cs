using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Tdmu.Advisor.Api.Features.KnowledgeBase.Entities;
using Tdmu.Advisor.Api.Infrastructure;
using Tdmu.Advisor.Tests.Infrastructure;

namespace Tdmu.Advisor.Tests;

[Collection("api")]
public class KbSchemaTests(ApiFactory f)
{
    private static DiemChuan Dc(Guid nganh, Guid pt, Guid? releaseId = null) =>
        new() { NganhLid = nganh, PhuongThucLid = pt, Nam = 2026, Diem = 21.5m, ReleaseId = releaseId };

    [Fact]
    public async Task Duplicate_diem_chuan_in_draft_is_rejected()
    {
        await f.ResetDatabaseAsync();
        using var scope = f.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AdvisorDbContext>();
        var (n, p) = (Guid.NewGuid(), Guid.NewGuid());
        db.DiemChuan.AddRange(Dc(n, p), Dc(n, p));

        var ex = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        Assert.Equal("23505", Assert.IsType<PostgresException>(ex.InnerException).SqlState);
    }

    [Fact]
    public async Task Same_diem_chuan_in_draft_and_release_is_allowed()
    {
        await f.ResetDatabaseAsync();
        using var scope = f.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AdvisorDbContext>();
        var r = new Release { SoHieu = 1, GhiChu = "r1", TaoBoi = "admin" };
        db.Release.Add(r);
        await db.SaveChangesAsync();
        var (n, p) = (Guid.NewGuid(), Guid.NewGuid());
        db.DiemChuan.AddRange(Dc(n, p), Dc(n, p, r.Id));

        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task Only_one_active_release()
    {
        await f.ResetDatabaseAsync();
        using var scope = f.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AdvisorDbContext>();
        db.Release.AddRange(
            new Release { SoHieu = 1, TaoBoi = "a", IsActive = true },
            new Release { SoHieu = 2, TaoBoi = "a", IsActive = true });

        var ex = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        Assert.Equal("23505", Assert.IsType<PostgresException>(ex.InnerException).SqlState);
    }

    [Fact]
    public async Task Only_one_current_season_per_release_scope()
    {
        await f.ResetDatabaseAsync();
        using var scope = f.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AdvisorDbContext>();
        db.MuaTuyenSinh.AddRange(new MuaTuyenSinh { Nam = 2025, LaHienTai = true }, new MuaTuyenSinh { Nam = 2026, LaHienTai = true });

        var ex = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        Assert.Equal("23505", Assert.IsType<PostgresException>(ex.InnerException).SqlState);
    }

    [Fact]
    public async Task Duplicate_logical_id_in_draft_is_rejected()
    {
        await f.ResetDatabaseAsync();
        using var scope = f.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AdvisorDbContext>();
        var lid = Guid.NewGuid();
        db.Faq.AddRange(new Faq { LogicalId = lid, CauHoi = "a", TraLoi = "b" }, new Faq { LogicalId = lid, CauHoi = "c", TraLoi = "d" });

        var ex = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        Assert.Equal("23505", Assert.IsType<PostgresException>(ex.InnerException).SqlState);
    }

    [Fact]
    public async Task Entities_round_trip_with_arrays_enums_vectors_and_audit_json()
    {
        await f.ResetDatabaseAsync();
        using var scope = f.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AdvisorDbContext>();
        var vec = new Pgvector.Vector(Enumerable.Repeat(0.1f, 768).ToArray());
        db.Nganh.Add(new Nganh { MaNganh = "7480201", Ten = "Công nghệ thông tin", TenGoiKhac = ["CNTT", "IT"] });
        db.PhuongThuc.Add(new PhuongThuc { Ma = "DGNL", Ten = "ĐGNL", ThangDiem = 1200m });
        db.HocPhi.Add(new HocPhi { Nam = 2026, Muc = 500000m, DonVi = DonViHocPhi.TinChi });
        db.TaiLieu.Add(new TaiLieu { TieuDe = "t", DuongDanFile = "f" });
        db.Faq.Add(new Faq { CauHoi = "q", TraLoi = "a", Embedding = vec });
        db.AuditLog.Add(new AuditLog { Entity = "nganh", Action = "create", AfterJson = "{\"a\":1}", UserId = "u" });
        await db.SaveChangesAsync();

        using var scope2 = f.Services.CreateScope();
        var db2 = scope2.ServiceProvider.GetRequiredService<AdvisorDbContext>();
        Assert.Equal(["CNTT", "IT"], (await db2.Nganh.SingleAsync()).TenGoiKhac);
        Assert.Equal(1200m, (await db2.PhuongThuc.SingleAsync()).ThangDiem);
        Assert.Equal(DonViHocPhi.TinChi, (await db2.HocPhi.SingleAsync()).DonVi);
        Assert.Equal(TrangThaiTaiLieu.ChoXuLy, (await db2.TaiLieu.SingleAsync()).TrangThai);
        Assert.Equal(768, (await db2.Faq.SingleAsync()).Embedding!.ToArray().Length);
        Assert.Equal("{\"a\": 1}", (await db2.AuditLog.SingleAsync(a => a.Entity == "nganh")).AfterJson);
    }

    [Fact]
    public void ContentKey_ignores_identity_fields_and_tracks_content()
    {
        var a = new Nganh { MaNganh = "1", Ten = "A", TenGoiKhac = ["x"] };
        var b = new Nganh { MaNganh = "1", Ten = "A", TenGoiKhac = ["x"], ReleaseId = Guid.NewGuid(), CapNhatLuc = DateTimeOffset.MinValue };
        Assert.Equal(a.ContentKey(), b.ContentKey());
        b.Ten = "B";
        Assert.NotEqual(a.ContentKey(), b.ContentKey());
    }

    [Fact]
    public async Task Required_indexes_exist()
    {
        using var scope = f.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AdvisorDbContext>();
        var defs = await db.Database.SqlQueryRaw<string>("SELECT indexdef AS \"Value\" FROM pg_indexes WHERE schemaname = 'public'").ToListAsync();
        var all = string.Join("\n", defs);
        Assert.Contains("USING hnsw (embedding vector_cosine_ops)", all);
        Assert.Contains("USING gin (tsv)", all);
        Assert.Contains("USING gin (ten gin_trgm_ops)", all);
        Assert.Contains("NULLS NOT DISTINCT", all);
    }

    [Fact]
    public void DiemChuan_label_uses_lookup_names_and_falls_back_for_unknown_lids()
    {
        var (n, p) = (Guid.NewGuid(), Guid.NewGuid());
        var dc = Dc(n, p);
        var lookup = new DictionaryKbLabelLookup(
            nganh: new Dictionary<Guid, string> { [n] = "Công nghệ thông tin" },
            phuongThuc: new Dictionary<Guid, string> { [p] = "thi THPT" });
        Assert.Equal("Điểm chuẩn Công nghệ thông tin 2026 – thi THPT", dc.Label(lookup));
        Assert.Equal($"Điểm chuẩn #{n.ToString("N")[..8]} 2026 – #{p.ToString("N")[..8]}", dc.Label(new DictionaryKbLabelLookup()));
    }

    [Fact]
    public async Task Release_with_rows_cannot_be_deleted()
    {
        await f.ResetDatabaseAsync();
        using var scope = f.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AdvisorDbContext>();
        var r = new Release { SoHieu = 1, TaoBoi = "a" };
        db.Release.Add(r);
        db.Faq.Add(new Faq { CauHoi = "q", TraLoi = "a", ReleaseId = r.Id });
        await db.SaveChangesAsync();
        var ex = await Assert.ThrowsAsync<PostgresException>(() =>
            db.Database.ExecuteSqlRawAsync("DELETE FROM release"));
        Assert.Equal("23503", ex.SqlState);
    }
}
