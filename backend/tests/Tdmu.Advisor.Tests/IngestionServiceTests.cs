using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Tdmu.Advisor.Api.Features.Ingestion;
using Tdmu.Advisor.Api.Features.KnowledgeBase.Entities;
using Tdmu.Advisor.Api.Infrastructure;
using Tdmu.Advisor.Api.Infrastructure.Gemini;
using Tdmu.Advisor.Api.Infrastructure.Storage;
using Tdmu.Advisor.Tests.Infrastructure;

namespace Tdmu.Advisor.Tests;

[Collection("api")]
public class IngestionServiceTests(ApiFactory f)
{
    private async Task<T> Run<T>(Func<IServiceProvider, AdvisorDbContext, Task<T>> fn)
    {
        using var scope = f.Services.CreateScope();
        return await fn(scope.ServiceProvider, scope.ServiceProvider.GetRequiredService<AdvisorDbContext>());
    }

    private Task<Guid> AddDoc(string md)
    {
        return Run(async (sp, db) =>
        {
            var t = new TaiLieu { TieuDe = "Đề án", TenFileGoc = "a.md", LoaiNoiDung = "text/markdown", KichThuoc = md.Length };
            t.DuongDanFile = await sp.GetRequiredService<IFileStore>().SaveAsync(t.LogicalId, ".md", new MemoryStream(Encoding.UTF8.GetBytes(md)), default);
            db.TaiLieu.Add(t);
            await db.SaveChangesAsync();
            return t.LogicalId;
        });
    }

    private Task<int> Process() => Run((sp, _) => sp.GetRequiredService<IngestionService>().ProcessPendingDocumentsAsync(default));

    private const string TwoSections = "# Học phí\n\nHọc phí ngành Dược là 30 triệu đồng mỗi năm.\n\n# Ký túc xá\n\nKý túc xá có 500 chỗ ở cho sinh viên.";

    [Fact]
    public async Task Processes_document_into_chunks_with_tsv_and_embeddings()
    {
        await f.ResetDatabaseAsync();
        var lid = await AddDoc(TwoSections);
        Assert.Equal(1, await Process());

        await Run(async (_, db) =>
        {
            var t = await db.TaiLieu.SingleAsync(x => x.LogicalId == lid);
            Assert.Equal(TrangThaiTaiLieu.Xong, t.TrangThai);
            var chunks = await db.TaiLieuChunk.Where(x => x.TaiLieuLid == lid).ToListAsync();
            Assert.True(chunks.Count >= 2);
            Assert.All(chunks, c =>
            {
                Assert.Null(c.ReleaseId);
                Assert.NotNull(c.Tsv);
                Assert.Equal(768, c.Embedding!.ToArray().Length);
            });
            Assert.Contains(chunks, c => c.Tsv!.ToString().Contains("'tuc'")); // unaccent: "túc" → "tuc"
            return 0;
        });
        Assert.Equal(0, await Process()); // Không còn gì để xử lý.
    }

    [Fact]
    public async Task Reprocess_replaces_chunks()
    {
        await f.ResetDatabaseAsync();
        var lid = await AddDoc(TwoSections);
        await Process();
        var before = await Run((_, db) => db.TaiLieuChunk.Where(x => x.TaiLieuLid == lid).Select(x => x.Id).ToListAsync());

        await Run(async (_, db) =>
        {
            await db.TaiLieu.Where(x => x.LogicalId == lid).ExecuteUpdateAsync(s => s.SetProperty(x => x.TrangThai, TrangThaiTaiLieu.ChoXuLy));
            return 0;
        });
        Assert.Equal(1, await Process());

        var after = await Run((_, db) => db.TaiLieuChunk.Where(x => x.TaiLieuLid == lid).Select(x => x.Id).ToListAsync());
        Assert.Equal(before.Count, after.Count);
        Assert.Empty(before.Intersect(after));
    }

    [Fact]
    public async Task Missing_api_key_marks_failed_with_vietnamese_reason()
    {
        await f.ResetDatabaseAsync();
        var lid = await AddDoc(TwoSections);
        f.Gemini.FailNext = new GeminiNotConfiguredException();
        Assert.Equal(1, await Process());
        var t = await Run((_, db) => db.TaiLieu.SingleAsync(x => x.LogicalId == lid));
        Assert.Equal(TrangThaiTaiLieu.ThatBai, t.TrangThai);
        Assert.Equal("Chưa cấu hình Gemini API key", t.Loi);
        Assert.Equal(0, await Run((_, db) => db.TaiLieuChunk.CountAsync()));
    }

    [Fact]
    public async Task Empty_text_marks_failed()
    {
        await f.ResetDatabaseAsync();
        var lid = await AddDoc("   \n\n  ");
        await Process();
        var t = await Run((_, db) => db.TaiLieu.SingleAsync(x => x.LogicalId == lid));
        Assert.Equal(TrangThaiTaiLieu.ThatBai, t.TrangThai);
        Assert.Equal("Không trích được nội dung từ file", t.Loi);
    }

    [Fact]
    public async Task Other_errors_are_short_and_prefixed()
    {
        await f.ResetDatabaseAsync();
        var lid = await AddDoc(TwoSections);
        f.Gemini.FailNext = new InvalidOperationException(new string('x', 1000) + "\nsecond line");
        await Process();
        var t = await Run((_, db) => db.TaiLieu.SingleAsync(x => x.LogicalId == lid));
        Assert.StartsWith("Lỗi xử lý: ", t.Loi);
        Assert.True(t.Loi!.Length <= 320);
        Assert.DoesNotContain("second line", t.Loi);
    }

    [Fact]
    public async Task Concurrent_ticks_process_a_document_once()
    {
        await f.ResetDatabaseAsync();
        var lid = await AddDoc(TwoSections);
        var results = await Task.WhenAll(Process(), Process());
        Assert.Equal(1, results.Sum());
        Assert.Equal(2, await Run((_, db) => db.TaiLieuChunk.CountAsync(x => x.TaiLieuLid == lid)));
    }

    [Fact]
    public async Task Embeds_pending_faqs()
    {
        await f.ResetDatabaseAsync();
        var lid = await Run(async (_, db) =>
        {
            var faq = new Faq { CauHoi = "Học phí bao nhiêu?", CachHoiKhac = ["Phí học là bao nhiêu"], TraLoi = "30 triệu" };
            db.Faq.Add(faq);
            await db.SaveChangesAsync();
            return faq.LogicalId;
        });
        var n = await Run((sp, _) => sp.GetRequiredService<IngestionService>().EmbedPendingFaqsAsync(default));
        Assert.Equal(1, n);
        var faqRow = await Run((_, db) => db.Faq.SingleAsync(x => x.LogicalId == lid));
        Assert.Equal(768, faqRow.Embedding!.ToArray().Length);
        Assert.Equal(0, await Run((sp, _) => sp.GetRequiredService<IngestionService>().EmbedPendingFaqsAsync(default)));
    }

    [Fact]
    public async Task Heading_is_part_of_chunk_text_and_searchable()
    {
        await f.ResetDatabaseAsync();
        var lid = await AddDoc("## Học phí\n\nNgành Dược 30 triệu mỗi năm.");
        await Process();
        var c = await Run((_, db) => db.TaiLieuChunk.SingleAsync(x => x.TaiLieuLid == lid));
        Assert.StartsWith("Học phí", c.NoiDung);
        var matches = await Run((_, db) => db.Database
            .SqlQuery<int>($"SELECT count(*)::int AS \"Value\" FROM tai_lieu_chunk WHERE tsv @@ to_tsquery('simple', unaccent('hoc & phi'))").SingleAsync());
        Assert.Equal(1, matches);
    }

    [Fact]
    public async Task Stuck_documents_are_reset_and_failure_does_not_override_other_states()
    {
        await f.ResetDatabaseAsync();
        var lid = await AddDoc(TwoSections);
        await Run(async (_, db) =>
        {
            await db.TaiLieu.Where(x => x.LogicalId == lid).ExecuteUpdateAsync(s => s.SetProperty(x => x.TrangThai, TrangThaiTaiLieu.DangXuLy));
            return 0;
        });
        Assert.Equal(1, await Run((sp, _) => sp.GetRequiredService<IngestionService>().ResetStuckDocumentsAsync(default)));
        Assert.Equal(1, await Process());
        Assert.Equal(TrangThaiTaiLieu.Xong, (await Run((_, db) => db.TaiLieu.SingleAsync(x => x.LogicalId == lid))).TrangThai);
    }
}
