using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Options;
using Tdmu.Advisor.Api.Infrastructure.Gemini;

namespace Tdmu.Advisor.Tests;

public class GeminiRestClientTests
{
    private sealed class StubHandler(Func<HttpRequestMessage, string?, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public List<(HttpRequestMessage Request, string? Body)> Calls { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var body = request.Content is null ? null : await request.Content.ReadAsStringAsync(ct);
            Calls.Add((request, body));
            return respond(request, body);
        }
    }

    private static GeminiRestClient Make(StubHandler h, string key = "test-key", int idleSeconds = 30) => new(
        new HttpClient(h),
        Options.Create(new GeminiOptions { ApiKey = key, StreamIdleTimeoutSeconds = idleSeconds }),
        Options.Create(new EmbeddingOptions()),
        Options.Create(new AgentOptions()));

    private static HttpResponseMessage Json(string json, HttpStatusCode code = HttpStatusCode.OK) =>
        new(code) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    private static string EmbedResponse(int n) =>
        new JsonObject { ["embeddings"] = new JsonArray(Enumerable.Range(0, n)
            .Select(_ => (JsonNode)new JsonObject { ["values"] = new JsonArray(1f, 2f, 3f) }).ToArray()) }.ToJsonString();

    [Fact]
    public async Task Embed_posts_batch_with_task_type_and_dimensions()
    {
        var h = new StubHandler((_, _) => Json("""{"embeddings":[{"values":[0.1,0.2]},{"values":[0.3,0.4,0.5]}]}"""));
        var result = await Make(h).EmbedAsync(["a", "b"], EmbeddingTask.RetrievalQuery, default);

        var (req, body) = Assert.Single(h.Calls);
        Assert.Equal(HttpMethod.Post, req.Method);
        Assert.Equal("https://generativelanguage.googleapis.com/v1beta/models/gemini-embedding-001:batchEmbedContents", req.RequestUri!.ToString());
        Assert.Equal("test-key", req.Headers.GetValues("x-goog-api-key").Single());
        var requests = JsonNode.Parse(body!)!["requests"]!.AsArray();
        Assert.Equal(2, requests.Count);
        Assert.Equal("RETRIEVAL_QUERY", (string?)requests[0]!["taskType"]);
        Assert.Equal(768, (int?)requests[0]!["outputDimensionality"]);
        Assert.Equal("models/gemini-embedding-001", (string?)requests[0]!["model"]);
        Assert.Equal("a", (string?)requests[0]!["content"]!["parts"]![0]!["text"]);
        Assert.Equal(2, result.Count);
        Assert.Equal([0.1f, 0.2f], result[0]);
        Assert.Equal(3, result[1].Length);
    }

    [Fact]
    public async Task Embed_splits_into_batches_of_100()
    {
        var h = new StubHandler((_, body) =>
            Json(EmbedResponse(JsonNode.Parse(body!)!["requests"]!.AsArray().Count)));
        var result = await Make(h).EmbedAsync(Enumerable.Range(0, 150).Select(i => $"t{i}").ToList(), EmbeddingTask.RetrievalDocument, default);

        Assert.Equal(2, h.Calls.Count);
        Assert.Equal(150, result.Count);
        Assert.Equal("RETRIEVAL_DOCUMENT", (string?)JsonNode.Parse(h.Calls[0].Body!)!["requests"]![0]!["taskType"]);
    }

    [Fact]
    public async Task Stream_parses_sse_text_and_function_call_parts_verbatim()
    {
        const string sse =
            "data: {\"candidates\":[{\"content\":{\"role\":\"model\",\"parts\":[{\"text\":\"Xin \"}]}}]}\r\n\r\n" +
            "data: {\"candidates\":[{\"content\":{\"role\":\"model\",\"parts\":[{\"functionCall\":{\"name\":\"tra_diem_chuan\",\"args\":{\"nganh\":\"CNTT\"}},\"thoughtSignature\":\"abc\"}]},\"finishReason\":\"STOP\"}]}\r\n\r\n";
        var h = new StubHandler((_, _) => new HttpResponseMessage(HttpStatusCode.OK)
            { Content = new StringContent(sse, Encoding.UTF8, "text/event-stream") });
        var req = new GeminiRequest("sys",
            [new JsonObject { ["role"] = "user", ["parts"] = new JsonArray(new JsonObject { ["text"] = "hi" }) }],
            new JsonArray(new JsonObject { ["name"] = "tra_diem_chuan" }), "gemini-test");

        var chunks = new List<GeminiChunk>();
        await foreach (var c in Make(h).StreamGenerateAsync(req, default)) chunks.Add(c);

        var (httpReq, body) = Assert.Single(h.Calls);
        Assert.Equal("https://generativelanguage.googleapis.com/v1beta/models/gemini-test:streamGenerateContent?alt=sse", httpReq.RequestUri!.ToString());
        var sent = JsonNode.Parse(body!)!;
        Assert.Equal("sys", (string?)sent["systemInstruction"]!["parts"]![0]!["text"]);
        Assert.Equal("tra_diem_chuan", (string?)sent["tools"]![0]!["functionDeclarations"]![0]!["name"]);
        Assert.Equal(0.3, (double?)sent["generationConfig"]!["temperature"]);

        Assert.Equal(2, chunks.Count);
        Assert.Equal("Xin ", (string?)chunks[0].Parts[0]["text"]);
        Assert.Null(chunks[0].FinishReason);
        Assert.Equal("abc", (string?)chunks[1].Parts[0]["thoughtSignature"]);
        Assert.Equal("CNTT", (string?)chunks[1].Parts[0]["functionCall"]!["args"]!["nganh"]);
        Assert.Equal("STOP", chunks[1].FinishReason);
    }

    [Fact]
    public async Task Pdf_to_markdown_sends_inline_pdf_and_returns_text()
    {
        var h = new StubHandler((_, _) =>
            Json("""{"candidates":[{"content":{"parts":[{"text":"# Tiêu đề"}]}}]}"""));
        var md = await Make(h).PdfToMarkdownAsync([1, 2, 3], default);

        Assert.Equal("# Tiêu đề", md);
        var part = JsonNode.Parse(h.Calls[0].Body!)!["contents"]![0]!["parts"]!;
        Assert.Equal("application/pdf", (string?)part[0]!["inlineData"]!["mimeType"]);
        Assert.Equal(Convert.ToBase64String([1, 2, 3]), (string?)part[0]!["inlineData"]!["data"]);
        Assert.StartsWith("Chuyển toàn bộ nội dung", (string?)part[1]!["text"]);
    }

    [Theory]
    [InlineData(HttpStatusCode.TooManyRequests, 429)]
    [InlineData(HttpStatusCode.ServiceUnavailable, 503)]
    public async Task Status_429_and_503_throw_unavailable(HttpStatusCode code, int status)
    {
        var h = new StubHandler((_, _) => Json("{}", code));
        var ex = await Assert.ThrowsAsync<GeminiUnavailableException>(() =>
            Make(h).EmbedAsync(["a"], EmbeddingTask.RetrievalQuery, default));
        Assert.Equal(status, ex.Status);

        var ex2 = await Assert.ThrowsAsync<GeminiUnavailableException>(async () =>
        {
            await foreach (var _ in Make(h).StreamGenerateAsync(new GeminiRequest("s", [], new JsonArray()), default)) { }
        });
        Assert.Equal(status, ex2.Status);
    }

    [Fact]
    public async Task Network_error_throws_unavailable_without_status()
    {
        var h = new StubHandler((_, _) => throw new HttpRequestException("boom"));
        var ex = await Assert.ThrowsAsync<GeminiUnavailableException>(() =>
            Make(h).EmbedAsync(["a"], EmbeddingTask.RetrievalQuery, default));
        Assert.Null(ex.Status);
    }

    [Fact]
    public async Task Empty_key_throws_not_configured()
    {
        var h = new StubHandler((_, _) => Json("{}"));
        await Assert.ThrowsAsync<GeminiNotConfiguredException>(() =>
            Make(h, key: "").EmbedAsync(["a"], EmbeddingTask.RetrievalQuery, default));
        Assert.Empty(h.Calls);
    }

    [Fact]
    public async Task Status_400_throws_unavailable_with_body_snippet()
    {
        var h = new StubHandler((_, _) => Json("""{"error":{"message":"Invalid argument xyz"}}""", HttpStatusCode.BadRequest));
        var ex = await Assert.ThrowsAsync<GeminiUnavailableException>(() =>
            Make(h).EmbedAsync(["a"], EmbeddingTask.RetrievalQuery, default));
        Assert.Equal(400, ex.Status);
        Assert.Contains("Invalid argument xyz", ex.Message);
        Assert.DoesNotContain("test-key", ex.Message);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    public async Task Status_401_403_throw_not_configured(HttpStatusCode code)
    {
        var h = new StubHandler((_, _) => Json("{}", code));
        await Assert.ThrowsAsync<GeminiNotConfiguredException>(() =>
            Make(h).EmbedAsync(["a"], EmbeddingTask.RetrievalQuery, default));
    }

    private static async Task<List<GeminiChunk>> StreamAsync(string sse, GeminiRequest? req = null)
    {
        var h = new StubHandler((_, _) => new HttpResponseMessage(HttpStatusCode.OK)
            { Content = new StringContent(sse, Encoding.UTF8, "text/event-stream") });
        var list = new List<GeminiChunk>();
        await foreach (var c in Make(h).StreamGenerateAsync(req ?? new GeminiRequest("s", [], new JsonArray()), default)) list.Add(c);
        return list;
    }

    [Fact]
    public async Task Stream_skips_non_json_data_lines()
    {
        var chunks = await StreamAsync("data: [DONE]\n\ndata: {\"candidates\":[{\"content\":{\"parts\":[{\"text\":\"a\"}]}}]}\n\n");
        Assert.Equal("a", (string?)Assert.Single(chunks).Parts[0]["text"]);
    }

    [Fact]
    public async Task Stream_joins_event_split_across_data_lines()
    {
        var chunks = await StreamAsync("data: {\"candidates\":[{\"content\":\ndata: {\"parts\":[{\"text\":\"b\"}]}}]}\n\n");
        Assert.Equal("b", (string?)Assert.Single(chunks).Parts[0]["text"]);
    }

    [Fact]
    public async Task Stream_parses_final_event_without_trailing_newline()
    {
        var chunks = await StreamAsync("data: {\"candidates\":[{\"content\":{\"parts\":[{\"text\":\"c\"}]},\"finishReason\":\"STOP\"}]}");
        Assert.Equal("STOP", Assert.Single(chunks).FinishReason);
    }

    [Fact]
    public async Task Stream_malformed_json_event_throws_unavailable()
    {
        await Assert.ThrowsAsync<GeminiUnavailableException>(() => StreamAsync("data: {\"candidates\":[\n\n"));
    }

    [Fact]
    public async Task Stream_omits_tools_when_no_function_declarations()
    {
        var h = new StubHandler((_, _) => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("") });
        await foreach (var _ in Make(h).StreamGenerateAsync(new GeminiRequest("s", [], new JsonArray()), default)) { }
        Assert.Null(JsonNode.Parse(h.Calls[0].Body!)!["tools"]);
    }

    [Fact]
    public async Task Embed_count_mismatch_throws_unavailable()
    {
        var h = new StubHandler((_, _) => Json(EmbedResponse(1)));
        await Assert.ThrowsAsync<GeminiUnavailableException>(() =>
            Make(h).EmbedAsync(["a", "b"], EmbeddingTask.RetrievalDocument, default));
    }

    /// <summary>Luồng không bao giờ trả byte nào (Gemini treo giữa chừng); chỉ dừng khi token bị hủy.</summary>
    private sealed class StalledStream : Stream
    {
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => 0; set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException("chỉ đọc bất đồng bộ");
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken ct)
        {
            await Task.Delay(Timeout.Infinite, ct);
            return 0;
        }
        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken ct) =>
            ReadAsync(buffer.AsMemory(offset, count), ct).AsTask();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    private static StubHandler StalledHandler() => new((_, _) => new HttpResponseMessage(HttpStatusCode.OK)
        { Content = new StreamContent(new StalledStream()) });

    private static GeminiRequest SimpleRequest() => new("sys",
        [new JsonObject { ["role"] = "user", ["parts"] = new JsonArray(new JsonObject { ["text"] = "hi" }) }], []);

    [Fact]
    public async Task Stream_stalled_mid_body_throws_unavailable_after_idle_timeout()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        await Assert.ThrowsAsync<GeminiUnavailableException>(async () =>
        {
            await foreach (var _ in Make(StalledHandler(), idleSeconds: 1).StreamGenerateAsync(SimpleRequest(), default)) { }
        });
        Assert.InRange(sw.Elapsed.TotalSeconds, 0.9, 2.5);
    }

    [Fact]
    public async Task Stream_caller_cancellation_is_not_mapped_to_unavailable()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));
        var ex = await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await foreach (var _ in Make(StalledHandler(), idleSeconds: 30).StreamGenerateAsync(SimpleRequest(), cts.Token)) { }
        });
        Assert.IsNotType<GeminiUnavailableException>(ex);
    }
}
