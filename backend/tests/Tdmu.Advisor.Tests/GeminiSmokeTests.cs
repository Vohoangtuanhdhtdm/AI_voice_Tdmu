using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Tdmu.Advisor.Api.Features.Ingestion;
using Tdmu.Advisor.Api.Features.KnowledgeBase.Entities;
using Tdmu.Advisor.Api.Infrastructure;
using Tdmu.Advisor.Api.Infrastructure.Gemini;
using Tdmu.Advisor.Tests.Infrastructure;
using Xunit.Abstractions;

namespace Tdmu.Advisor.Tests;

/// <summary>
/// Smoke test duy nhất gọi Gemini thật (embedding + chat có tool). Chỉ chạy khi có biến môi trường GEMINI_API_KEY:
/// <c>GEMINI_API_KEY=... dotnet test backend --filter Category=GeminiLive</c>. CI loại test này bằng <c>Category!=GeminiLive</c>.
/// Factory tạo riêng trong test (không dùng fixture) để khi bị bỏ qua thì không dựng container.
/// </summary>
[Trait("Category", "GeminiLive")]
public class GeminiSmokeTests(ITestOutputHelper output)
{
    private const int MaxAttempts = 5;
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(20);

    private const string KyTucXaMd = """
        # Ký túc xá sinh viên

        Ký túc xá của Trường Đại học Thủ Dầu Một có tổng cộng 1.200 chỗ ở dành cho sinh viên, gồm 200 phòng, mỗi phòng 6 chỗ.

        ## Đăng ký ở ký túc xá

        Sinh viên năm nhất đăng ký ở ký túc xá trực tuyến ngay sau khi nhập học. Phí ở ký túc xá là 400.000 đồng mỗi tháng.
        """;

    [GeminiLiveFact]
    public async Task Real_gemini_answers_with_tools_and_citations()
    {
        var f = new RealGeminiApiFactory();
        await f.InitializeAsync();
        try
        {
            await RunAsync(f);
        }
        finally
        {
            foreach (var w in f.Warnings) output.WriteLine(w);
            await ((IAsyncLifetime)f).DisposeAsync();
        }
    }

    private async Task RunAsync(RealGeminiApiFactory f)
    {
        await f.ResetDatabaseAsync();
        output.WriteLine($"Chat model: {f.Services.GetRequiredService<IOptions<AgentOptions>>().Value.ChatModel}");
        output.WriteLine($"Embedding model: {f.Services.GetRequiredService<IOptions<EmbeddingOptions>>().Value.Model}");
        var admin = await AuthHelper.AdminClientAsync(f);

        // Ngành CNTT, điểm chuẩn 21,5 theo phương thức thi THPT.
        await Create(admin, "mua-tuyen-sinh", new { nam = 2026, laHienTai = true });
        var cntt = await Create(admin, "nganh", new { maNganh = "7480201", ten = "Công nghệ thông tin", tenGoiKhac = new[] { "CNTT" } });
        var thpt = await Create(admin, "phuong-thuc", new { ma = "THPT", ten = "Xét điểm thi tốt nghiệp THPT", thangDiem = 30 });
        await Create(admin, "diem-chuan", new
        {
            nganhLid = cntt, nam = 2026, phuongThucLid = thpt, diem = 21.5,
            nguonTen = "Thông báo điểm chuẩn 2026", nguonUrl = "https://tdmu.edu.vn/diem-chuan-2026",
        });

        // Tài liệu ký túc xá: tải lên rồi chạy IngestionService thật (embedding Gemini thật).
        var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(Encoding.UTF8.GetBytes(KyTucXaMd));
        file.Headers.ContentType = new("text/markdown");
        form.Add(file, "file", "ky-tuc-xa.md");
        form.Add(new StringContent("Thông tin ký túc xá"), "tieuDe");
        form.Add(new StringContent("2026"), "namApDung");
        var up = await admin.PostAsync("/api/admin/tai-lieu", form);
        Assert.Equal(HttpStatusCode.Created, up.StatusCode);
        var docLid = (await up.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("logicalId").GetGuid();
        await IngestAsync(f, admin, docLid);

        Assert.Equal(HttpStatusCode.Created, (await admin.PostAsJsonAsync("/api/admin/releases", new { ghiChu = "Smoke test Gemini" })).StatusCode);

        var chat = f.CreateClient();

        var q1 = await AskAsync(chat, "Điểm chuẩn ngành công nghệ thông tin năm 2026 bao nhiêu?");
        Assert.Contains(q1.Tools, t => t == "tra_diem_chuan");
        Assert.True(q1.Text.Contains("21,5") || q1.Text.Contains("21.5"), $"Câu trả lời không có 21,5: {q1.Text}");

        var q2 = await AskAsync(chat, "Ký túc xá có bao nhiêu chỗ?");
        Assert.Contains(q2.Tools, t => t == "tim_kiem_tri_thuc");
        Assert.Contains("citations", q2.Events);
    }

    private static async Task<Guid> Create(HttpClient c, string slug, object body)
    {
        var res = await c.PostAsJsonAsync($"/api/admin/kb/{slug}", body);
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        return (await res.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("logicalId").GetGuid();
    }

    private async Task IngestAsync(RealGeminiApiFactory f, HttpClient admin, Guid docLid)
    {
        for (var attempt = 1; ; attempt++)
        {
            using (var scope = f.Services.CreateScope())
            {
                var svc = scope.ServiceProvider.GetRequiredService<IngestionService>();
                Assert.Equal(1, await svc.ProcessPendingDocumentsAsync(default));
                await svc.EmbedPendingFaqsAsync(default);
            }
            using var check = f.Services.CreateScope();
            var db = check.ServiceProvider.GetRequiredService<AdvisorDbContext>();
            var doc = await db.TaiLieu.AsNoTracking().SingleAsync(x => x.LogicalId == docLid && x.ReleaseId == null);
            output.WriteLine($"Ingestion lần {attempt}: {doc.TrangThai} {doc.Loi}");
            if (doc.TrangThai == TrangThaiTaiLieu.Xong)
            {
                var chunks = await db.TaiLieuChunk.AsNoTracking().Where(x => x.TaiLieuLid == docLid && x.ReleaseId == null).ToListAsync();
                Assert.NotEmpty(chunks);
                Assert.All(chunks, c => Assert.Equal(768, c.Embedding!.ToArray().Length));
                return;
            }
            Assert.True(attempt < MaxAttempts, $"Xử lý tài liệu thất bại: {doc.Loi}");
            await Task.Delay(RetryDelay);
            Assert.Equal(HttpStatusCode.NoContent, (await admin.PostAsync($"/api/admin/tai-lieu/{docLid}/xu-ly-lai", null)).StatusCode);
        }
    }

    private sealed record Answer(List<string> Events, List<string> Tools, string Text);

    /// <summary>Hỏi qua /api/chat; gặp event error (429 của gói miễn phí hoặc 503 "high demand" tạm thời của Google) thì chờ rồi hỏi lại, tối đa <see cref="MaxAttempts"/> lần.</summary>
    private async Task<Answer> AskAsync(HttpClient chat, string question)
    {
        for (var attempt = 1; ; attempt++)
        {
            var res = await chat.PostAsJsonAsync("/api/chat", new { message = question });
            Assert.Equal(HttpStatusCode.OK, res.StatusCode);
            var events = await ChatEndpointTests.ReadSseAsync(res);
            var names = events.Select(e => e.Event).ToList();
            var tools = events.Where(e => e.Event == "tool_status" && e.Data.GetProperty("state").GetString() == "start")
                .Select(e => e.Data.GetProperty("name").GetString()!).ToList();
            var text = string.Concat(events.Where(e => e.Event == "delta").Select(e => e.Data.GetProperty("text").GetString()));
            output.WriteLine($"Hỏi (lần {attempt}): {question}");
            output.WriteLine($"  events: {string.Join(", ", names)}");
            output.WriteLine($"  tools: {string.Join(", ", tools)}");
            output.WriteLine($"  trả lời: {text}");
            foreach (var c in events.Where(e => e.Event == "citations"))
                output.WriteLine($"  citations: {c.Data.GetRawText()}");
            Assert.Equal("done", names[^1]);
            if (!names.Contains("error")) return new(names, tools, text);
            Assert.True(attempt < MaxAttempts, $"Gemini báo lỗi sau {MaxAttempts} lần hỏi: {question}");
            await Task.Delay(RetryDelay);
        }
    }
}
