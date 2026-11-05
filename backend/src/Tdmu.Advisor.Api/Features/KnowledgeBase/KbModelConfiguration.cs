using Microsoft.EntityFrameworkCore;
using Tdmu.Advisor.Api.Features.KnowledgeBase.Entities;

namespace Tdmu.Advisor.Api.Features.KnowledgeBase;

public static class KbModelConfiguration
{
    public static void Configure(ModelBuilder mb)
    {
        Kb<MuaTuyenSinh>(mb, "mua_tuyen_sinh", e =>
            e.HasIndex(x => x.ReleaseId).IsUnique().AreNullsDistinct(false).HasFilter("la_hien_tai"));

        Kb<Nganh>(mb, "nganh", e =>
        {
            e.Property(x => x.MaNganh).IsRequired();
            e.Property(x => x.Ten).IsRequired();
            e.Property(x => x.TenGoiKhac).HasColumnType("text[]");
            e.HasIndex(x => new { x.ReleaseId, x.MaNganh }).IsUnique().AreNullsDistinct(false);
            e.HasIndex(x => x.Ten).HasMethod("gin").HasOperators("gin_trgm_ops");
        });

        Kb<PhuongThuc>(mb, "phuong_thuc", e =>
        {
            e.Property(x => x.ThangDiem).HasColumnType("numeric(7,2)").HasDefaultValue(30m);
            e.HasIndex(x => new { x.ReleaseId, x.Ma }).IsUnique().AreNullsDistinct(false);
        });

        Kb<ToHop>(mb, "to_hop", e =>
        {
            e.Property(x => x.Mon).HasColumnType("text[]");
            e.HasIndex(x => new { x.ReleaseId, x.Ma }).IsUnique().AreNullsDistinct(false);
        });

        Kb<NganhToHop>(mb, "nganh_to_hop", e =>
            e.HasIndex(x => new { x.ReleaseId, x.NganhLid, x.Nam, x.ToHopLid }).IsUnique().AreNullsDistinct(false));

        Kb<DiemChuan>(mb, "diem_chuan", e =>
        {
            e.Property(x => x.Diem).HasColumnType("numeric(7,2)");
            e.HasIndex(x => new { x.ReleaseId, x.NganhLid, x.Nam, x.PhuongThucLid }).IsUnique().AreNullsDistinct(false);
        });

        Kb<ChiTieu>(mb, "chi_tieu", e =>
            e.HasIndex(x => new { x.ReleaseId, x.NganhLid, x.Nam, x.PhuongThucLid }).IsUnique().AreNullsDistinct(false));

        Kb<HocPhi>(mb, "hoc_phi", e =>
        {
            e.Property(x => x.Muc).HasColumnType("numeric(14,2)");
            e.Property(x => x.DonVi).HasConversion<string>();
        });

        Kb<MocThoiGian>(mb, "moc_thoi_gian", _ => { });

        Kb<Faq>(mb, "faq", e =>
        {
            e.Property(x => x.CachHoiKhac).HasColumnType("text[]");
            e.Property(x => x.Embedding).HasColumnType("vector(768)");
            e.HasIndex(x => x.Embedding).HasMethod("hnsw").HasOperators("vector_cosine_ops");
        });

        Kb<TaiLieu>(mb, "tai_lieu", e => e.Property(x => x.TrangThai).HasConversion<string>());

        Kb<TaiLieuChunk>(mb, "tai_lieu_chunk", e =>
        {
            e.Property(x => x.Embedding).HasColumnType("vector(768)");
            e.HasIndex(x => x.Embedding).HasMethod("hnsw").HasOperators("vector_cosine_ops");
            e.Property(x => x.Tsv).HasColumnType("tsvector");
            e.HasIndex(x => x.Tsv).HasMethod("gin");
        });

        mb.Entity<Release>(e =>
        {
            e.ToTable("release");
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.IsActive).IsUnique().HasFilter("is_active");
        });

        mb.Entity<AuditLog>(e =>
        {
            e.ToTable("audit_log");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedOnAdd();
            e.Property(x => x.BeforeJson).HasColumnType("jsonb");
            e.Property(x => x.AfterJson).HasColumnType("jsonb");
            e.HasIndex(x => new { x.Entity, x.EntityLogicalId });
        });
    }

    private static void Kb<T>(ModelBuilder mb, string table, Action<Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<T>> extra)
        where T : KbEntity
    {
        mb.Entity<T>(e =>
        {
            e.ToTable(table);
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.ReleaseId, x.LogicalId }).IsUnique().AreNullsDistinct(false);
            e.HasOne<Release>().WithMany().HasForeignKey(x => x.ReleaseId).OnDelete(DeleteBehavior.Cascade);
            extra(e);
        });
    }
}
