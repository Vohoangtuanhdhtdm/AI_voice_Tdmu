using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Tdmu.Advisor.Api.Features.KnowledgeBase;
using Tdmu.Advisor.Api.Features.KnowledgeBase.Entities;

namespace Tdmu.Advisor.Api.Infrastructure;

public class AdvisorDbContext(DbContextOptions<AdvisorDbContext> options) : IdentityDbContext<IdentityUser>(options)
{
    public DbSet<MuaTuyenSinh> MuaTuyenSinh => Set<MuaTuyenSinh>();
    public DbSet<Nganh> Nganh => Set<Nganh>();
    public DbSet<PhuongThuc> PhuongThuc => Set<PhuongThuc>();
    public DbSet<ToHop> ToHop => Set<ToHop>();
    public DbSet<NganhToHop> NganhToHop => Set<NganhToHop>();
    public DbSet<DiemChuan> DiemChuan => Set<DiemChuan>();
    public DbSet<ChiTieu> ChiTieu => Set<ChiTieu>();
    public DbSet<HocPhi> HocPhi => Set<HocPhi>();
    public DbSet<MocThoiGian> MocThoiGian => Set<MocThoiGian>();
    public DbSet<Faq> Faq => Set<Faq>();
    public DbSet<TaiLieu> TaiLieu => Set<TaiLieu>();
    public DbSet<TaiLieuChunk> TaiLieuChunk => Set<TaiLieuChunk>();
    public DbSet<Release> Release => Set<Release>();
    public DbSet<AuditLog> AuditLog => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasPostgresExtension("vector");
        modelBuilder.HasPostgresExtension("unaccent");
        modelBuilder.HasPostgresExtension("pg_trgm");
        KbModelConfiguration.Configure(modelBuilder);
    }
}
