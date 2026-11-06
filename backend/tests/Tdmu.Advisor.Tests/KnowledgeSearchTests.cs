using Npgsql;
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

    private static float[] WithTinyNoise(float[] v, int i) { v[300 + i] = 0.001f; return v; }

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
            db.TaiLieuChunk.Add(new TaiLieuChunk { TaiLieuLid = doc.LogicalId, ThuTu = i++, NoiDung = text, ViTri = "Mục " + i, Embedding = new Vector(WithTinyNoise(Vec(v), i)) });
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

    [Fact]
    public async Task Scoped_vector_search_survives_many_release_copies_on_hnsw()
    {
        await f.ResetDatabaseAsync();
        Assert.Equal("0.8", (await Run((_, db) => db.Database.SqlQuery<string>(
            $"SELECT extversion AS \"Value\" FROM pg_extension WHERE extname = 'vector'").SingleAsync()))[..3]);
        await AddFaq("Học phí bao nhiêu?", "30 triệu", 0);
        await AddDoc("Đề án", 2026, Enumerable.Range(0, 30).Select(i => ($"Đoạn thứ {i} zzz", 2)).ToArray());
        for (var i = 0; i < 3; i++)
        {
            await AddFaq($"Câu hỏi phụ {i}", "x", 10 + i); // tạo thay đổi để mỗi lần xuất bản là một release mới
            await Run(async (sp, _) => { await sp.GetRequiredService<ReleaseService>().PublishAsync("x", "u", default); return 0; });
        }
        await AddFaq("Câu hỏi nháp", "y", 20);
        // Làm đầy release cũ bằng các bản sao khớp tuyệt đối với truy vấn để HNSW (post-filter) trả về toàn hàng của release khác.
        await Run(async (_, db) =>
        {
            // Vector gần truy vấn nhưng có nhiễu (các bản trùng hệt làm đồ thị HNSW suy biến, không giống dữ liệu thật).
            static string Noisy(int hot, double second = 0, int hot2 = 3) => FormattableString.Invariant(
                $"('[' || array_to_string(ARRAY(SELECT CASE WHEN i = {hot + 1} THEN 1 WHEN i = {hot2 + 1} THEN {second} ELSE random() * 0.05 + g * 0 END FROM generate_series(1, 768) i), ',') || ']')::vector");
            var filler = Noisy(1);
            await db.Database.ExecuteSqlRawAsync($"""
                INSERT INTO tai_lieu_chunk (id, logical_id, release_id, nguon_ten, nguon_url, cap_nhat_luc, tai_lieu_lid, thu_tu, noi_dung, vi_tri, embedding, tsv)
                SELECT gen_random_uuid(), gen_random_uuid(), c.release_id, NULL, NULL, now(), c.tai_lieu_lid, g, c.noi_dung, c.vi_tri, {filler}, c.tsv
                FROM tai_lieu_chunk c, generate_series(1, 50) g
                WHERE c.release_id = (SELECT id FROM release ORDER BY so_hieu LIMIT 1)
                """);
            var faqFiller = Noisy(0, 0.3);
            await db.Database.ExecuteSqlRawAsync($"""
                INSERT INTO faq (id, logical_id, release_id, nguon_ten, nguon_url, cap_nhat_luc, cau_hoi, cach_hoi_khac, tra_loi, chu_de, embedding)
                SELECT gen_random_uuid(), gen_random_uuid(), c.release_id, NULL, NULL, now(), c.cau_hoi, c.cach_hoi_khac, 'mồi', c.chu_de, {faqFiller}
                FROM faq c, generate_series(1, 300) g
                WHERE c.cau_hoi = 'Học phí bao nhiêu?' AND c.release_id = (SELECT id FROM release ORDER BY so_hieu LIMIT 1)
                """);
            await db.Database.ExecuteSqlRawAsync("ANALYZE tai_lieu_chunk");
            await db.Database.ExecuteSqlRawAsync("ANALYZE faq");
            return 0;
        });

        // Bảng nhỏ nên planner thích seq scan: dùng kết nối riêng tắt seq scan để bắt buộc đi qua chỉ mục HNSW thật.
        var csb = new Npgsql.NpgsqlConnectionStringBuilder(f.Services.GetRequiredService<Microsoft.Extensions.Configuration.IConfiguration>()["ConnectionStrings:Default"]!) { Options = "-c enable_seqscan=off" };
        var dsb = new Npgsql.NpgsqlDataSourceBuilder(csb.ConnectionString);
        dsb.UseVector();
        await using var ds = dsb.Build();
        await using var db2 = new AdvisorDbContext(new DbContextOptionsBuilder<AdvisorDbContext>()
            .UseNpgsql(ds, o => o.UseVector()).UseSnakeCaseNamingConvention().Options);
        using var svc = f.Services.CreateScope();
        var search = new KnowledgeSearch(db2, svc.ServiceProvider.GetRequiredService<Api.Features.KnowledgeBase.Query.IKnowledgeQuery>(),
            f.Gemini, svc.ServiceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<Api.Infrastructure.Gemini.RagOptions>>());
        foreach (var scope in new[] { KbScope.Published, KbScope.Draft })
        {
            f.Gemini.EmbedOverride = _ => { var q = Vec(0); q[3] = 0.3f; return q; }; // gần FAQ thật (cos ~0.96) nhưng gần bản mồi hơn
            var faq = await search.SearchAsync("học phí", scope, default);
            Assert.Equal("faq", faq.Kind);
            Assert.Equal("30 triệu", faq.FaqAnswer);

            f.Gemini.EmbedOverride = _ => Vec(1);
            var chunks = await search.SearchAsync("qqq", scope, default);
            Assert.True(chunks.Kind == "tai_lieu", "scope " + scope);
            Assert.Equal(5, chunks.Hits.Count);
        }
    }

    [Fact]
    public async Task Blank_question_short_circuits_without_embedding()
    {
        await f.ResetDatabaseAsync();
        var r = await Search("   ");
        Assert.Equal("khong_co", r.Kind);
        Assert.Empty(f.Gemini.Requests);
        Assert.Equal(0, f.Gemini.EmbedCalls);
    }
}
