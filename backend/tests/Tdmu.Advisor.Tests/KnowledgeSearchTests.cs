using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pgvector;
using Tdmu.Advisor.Api.Features.Agent.Search;
using Tdmu.Advisor.Api.Features.KnowledgeBase;
using Tdmu.Advisor.Api.Features.KnowledgeBase.Entities;
using Tdmu.Advisor.Api.Features.KnowledgeBase.Releases;
using Tdmu.Advisor.Api.Infrastructure;
using Tdmu.Advisor.Tests.Infrastructure;

namespace Tdmu.Advisor.Tests;

[Collection("api")]
public class KnowledgeSearchTests(ApiFactory f)
{
    private static float[] Vec(int i) { var v = new float[FakeGeminiClient.Dimensions]; v[i] = 1f; return v; }

    private async Task<T> Run<T>(Func<IServiceProvider, AdvisorDbContext, Task<T>> fn)
    {
        using var scope = f.Services.CreateScope();
        return await fn(scope.ServiceProvider, scope.ServiceProvider.GetRequiredService<AdvisorDbContext>());
    }

    private Task<SearchResult> Search(string q, KbScope scope = KbScope.Draft) =>
        Run((sp, _) => sp.GetRequiredService<IKnowledgeSearch>().SearchAsync(q, scope, default));

    private Task<Guid> AddFaq(string cauHoi, string traLoi, int vec) => Run(async (_, db) =>
    {
        var faq = new Faq { CauHoi = cauHoi, TraLoi = traLoi, Embedding = new Vector(Vec(vec)), NguonTen = "Sổ tay" };
        db.Faq.Add(faq);
        await db.SaveChangesAsync();
        return faq.LogicalId;
    });

    private Task<Guid> AddDoc(string title, int? nam, params (string Text, int Vec)[] chunks) => Run(async (_, db) =>
    {
        var doc = new TaiLieu { TieuDe = title, NamApDung = nam, TrangThai = TrangThaiTaiLieu.Xong, TenFileGoc = "a.md", LoaiNoiDung = "text/markdown", NguonUrl = "https://x.test/" + title };
        db.TaiLieu.Add(doc);
        var i = 0;
        foreach (var (text, v) in chunks)
            db.TaiLieuChunk.Add(new TaiLieuChunk { TaiLieuLid = doc.LogicalId, ThuTu = i++, NoiDung = text, ViTri = "Mục " + i, Embedding = new Vector(Vec(v)) });
        await db.SaveChangesAsync();
        await db.Database.ExecuteSqlRawAsync("UPDATE tai_lieu_chunk SET tsv = to_tsvector('simple', unaccent(noi_dung)) WHERE tsv IS NULL");
        return doc.LogicalId;
    });

    [Fact]
    public async Task Faq_hit_above_threshold_returns_answer()
    {
        await f.ResetDatabaseAsync();
        await AddFaq("Học phí bao nhiêu?", "30 triệu mỗi năm", 0);
        f.Gemini.EmbedOverride = _ => Vec(0);
        var r = await Search("học phí");
        Assert.Equal("faq", r.Kind);
        Assert.Equal("30 triệu mỗi năm", r.FaqAnswer);
        var hit = Assert.Single(r.Hits);
        Assert.Equal("Sổ tay", hit.NguonTen);
    }

    [Fact]
    public async Task Faq_below_threshold_falls_through_to_chunks()
    {
        await f.ResetDatabaseAsync();
        await AddFaq("Học phí bao nhiêu?", "30 triệu", 0);
        await AddDoc("Đề án", 2026, ("Ngành Dược có 100 chỉ tiêu", 1));
        f.Gemini.EmbedOverride = _ => Vec(1);
        var r = await Search("chỉ tiêu dược");
        Assert.Equal("tai_lieu", r.Kind);
        Assert.Null(r.FaqAnswer);
        var hit = Assert.Single(r.Hits);
        Assert.Equal("Ngành Dược có 100 chỉ tiêu", hit.NoiDung);
        Assert.Equal("Đề án", hit.NguonTen);
        Assert.Equal("https://x.test/Đề án", hit.NguonUrl);
        Assert.Equal("Mục 1", hit.ViTri);
    }

    [Fact]
    public async Task Fulltext_matches_unaccented_query()
    {
        await f.ResetDatabaseAsync();
        // 21 đoạn mồi có vector trùng truy vấn chiếm hết top 20 của vector: đoạn đích chỉ tìm được bằng full-text.
        await AddDoc("Mồi", 2026, Enumerable.Range(0, 21).Select(i => ($"Nội dung khác số {i}", 9)).ToArray());
        await AddDoc("Nội trú", 2026, ("Ký túc xá có 2.000 chỗ", 5));
        f.Gemini.EmbedOverride = _ => Vec(9);
        var r = await Search("ky tuc xa");
        Assert.Equal("tai_lieu", r.Kind);
        Assert.Contains(r.Hits, h => h.NoiDung == "Ký túc xá có 2.000 chỗ");
    }

    [Fact]
    public async Task Fulltext_falls_back_to_any_term_when_all_terms_do_not_match()
    {
        await f.ResetDatabaseAsync();
        await AddDoc("Mồi", 2026, Enumerable.Range(0, 21).Select(i => ($"Nội dung khác số {i}", 9)).ToArray());
        await AddDoc("Nội trú", 2026, ("Ký túc xá có 2.000 chỗ", 5));
        f.Gemini.EmbedOverride = _ => Vec(9);
        var r = await Search("ký túc xá bao nhiêu chỗ ở?");
        Assert.Contains(r.Hits, h => h.NoiDung == "Ký túc xá có 2.000 chỗ");
    }

    [Fact]
    public async Task Draft_scope_does_not_see_published_only_and_vice_versa()
    {
        await f.ResetDatabaseAsync();
        var a = await AddDoc("A", 2026, ("alpha ban hanh", 1));
        await Run(async (sp, _) => { await sp.GetRequiredService<ReleaseService>().PublishAsync("x", "u", default); return 0; });
        await AddDoc("B", 2026, ("beta ban nhap", 2));
        f.Gemini.EmbedOverride = _ => Vec(1);

        var draft = await Search("alpha beta", KbScope.Draft);
        Assert.Contains(draft.Hits, h => h.NoiDung == "alpha ban hanh");
        Assert.Contains(draft.Hits, h => h.NoiDung == "beta ban nhap");
        var pub = await Search("alpha beta", KbScope.Published);
        Assert.Equal(["alpha ban hanh"], pub.Hits.Select(h => h.NoiDung));

        // Xóa A khỏi bản nháp: nháp không còn thấy, bản xuất bản vẫn thấy.
        await Run(async (_, db) =>
        {
            await db.TaiLieuChunk.Where(x => x.ReleaseId == null && x.TaiLieuLid == a).ExecuteDeleteAsync();
            await db.TaiLieu.Where(x => x.ReleaseId == null && x.LogicalId == a).ExecuteDeleteAsync();
            return 0;
        });
        Assert.DoesNotContain((await Search("alpha beta", KbScope.Draft)).Hits, h => h.NoiDung == "alpha ban hanh");
        Assert.Contains((await Search("alpha beta", KbScope.Published)).Hits, h => h.NoiDung == "alpha ban hanh");
    }

    [Fact]
    public async Task Current_year_document_ranks_first_on_tie()
    {
        await f.ResetDatabaseAsync();
        await Run(async (_, db) => { db.MuaTuyenSinh.Add(new MuaTuyenSinh { Nam = 2026, LaHienTai = true }); await db.SaveChangesAsync(); return 0; });
        await AddDoc("Cũ", 2025, ("Chỉ tiêu ngành Dược", 3));
        await AddDoc("Mới", 2026, ("Chỉ tiêu ngành Dược", 3));
        f.Gemini.EmbedOverride = _ => Vec(3);
        var r = await Search("chỉ tiêu ngành dược");
        Assert.Equal(2, r.Hits.Count);
        Assert.Equal("Mới", r.Hits[0].NguonTen);
        Assert.True(r.Hits[0].Score > r.Hits[1].Score);
    }

    [Fact]
    public async Task No_hits_returns_khong_co()
    {
        await f.ResetDatabaseAsync();
        f.Gemini.EmbedOverride = _ => Vec(0);
        var r = await Search("bất kỳ");
        Assert.Equal("khong_co", r.Kind);
        Assert.Null(r.FaqAnswer);
        Assert.Empty(r.Hits);
    }
}
