using System.Net;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Options;

namespace Tdmu.Advisor.Api.Infrastructure.Gemini;

public class GeminiRestClient(
    HttpClient http,
    IOptions<GeminiOptions> gemini,
    IOptions<EmbeddingOptions> embedding,
    IOptions<AgentOptions> agent) : IGeminiClient
{
    private const string BaseUrl = "https://generativelanguage.googleapis.com/v1beta/";
    private const int EmbedBatchSize = 100;
    private const string PdfPrompt =
        "Chuyển toàn bộ nội dung tài liệu sau sang Markdown tiếng Việt, giữ nguyên tiêu đề, bảng và số liệu. Không thêm bình luận.";

    public async Task<IReadOnlyList<float[]>> EmbedAsync(IReadOnlyList<string> texts, EmbeddingTask task, CancellationToken ct)
    {
        var opt = embedding.Value;
        var taskType = task == EmbeddingTask.RetrievalQuery ? "RETRIEVAL_QUERY" : "RETRIEVAL_DOCUMENT";
        var result = new List<float[]>(texts.Count);
        foreach (var batch in texts.Chunk(EmbedBatchSize))
        {
            var body = new JsonObject
            {
                ["requests"] = new JsonArray(batch.Select(t => (JsonNode)new JsonObject
                {
                    ["model"] = $"models/{opt.Model}",
                    ["content"] = new JsonObject { ["parts"] = new JsonArray(new JsonObject { ["text"] = t }) },
                    ["taskType"] = taskType,
                    ["outputDimensionality"] = opt.Dimensions,
                }).ToArray()),
            };
            using var res = await SendAsync($"models/{opt.Model}:batchEmbedContents", body, HttpCompletionOption.ResponseContentRead, ct);
            var json = JsonNode.Parse(await ReadBodyAsync(res, ct))!;
            var embeddings = json["embeddings"]?.AsArray();
            if (embeddings is null || embeddings.Count != batch.Length)
                throw new GeminiUnavailableException(null, new InvalidOperationException(
                    $"Gemini trả về {embeddings?.Count ?? 0} embedding cho {batch.Length} văn bản."));
            foreach (var e in embeddings)
                result.Add(e!["values"]!.AsArray().Select(v => (float)v!).ToArray());
        }
        return result;
    }

    public async IAsyncEnumerable<GeminiChunk> StreamGenerateAsync(GeminiRequest req, [EnumeratorCancellation] CancellationToken ct)
    {
        var body = new JsonObject
        {
            ["systemInstruction"] = new JsonObject { ["parts"] = new JsonArray(new JsonObject { ["text"] = req.SystemInstruction }) },
            ["contents"] = new JsonArray(req.Contents.Select(c => (JsonNode)c.DeepClone()).ToArray()),
            ["generationConfig"] = new JsonObject { ["temperature"] = 0.3 },
        };
        if (req.FunctionDeclarations.Count > 0)
            body["tools"] = new JsonArray(new JsonObject { ["functionDeclarations"] = req.FunctionDeclarations.DeepClone() });
        var model = req.Model ?? agent.Value.ChatModel;
        using var res = await SendAsync($"models/{model}:streamGenerateContent?alt=sse", body, HttpCompletionOption.ResponseHeadersRead, ct);
        await using var stream = await res.Content.ReadAsStreamAsync(ct);
        using var reader = new StreamReader(stream, Encoding.UTF8);
        var data = new StringBuilder();
        while (true)
        {
            string? line;
            try { line = await reader.ReadLineAsync(ct); }
            catch (IOException ex) { throw new GeminiUnavailableException(null, ex); }
            if (line is not null && line.Length > 0)
            {
                if (line.StartsWith("data:", StringComparison.Ordinal))
                {
                    if (data.Length > 0) data.Append('\n');
                    data.Append(line["data:".Length..].TrimStart());
                }
                continue;
            }
            // Dòng trống hoặc hết luồng: kết thúc một sự kiện SSE.
            if (data.Length > 0)
            {
                var chunk = ParseEvent(data.ToString());
                data.Clear();
                if (chunk is not null) yield return chunk;
            }
            if (line is null) yield break;
        }
    }

    private static GeminiChunk? ParseEvent(string payload)
    {
        payload = payload.Trim();
        if (!payload.StartsWith('{')) return null; // ví dụ "[DONE]"
        try
        {
            var candidate = JsonNode.Parse(payload)?["candidates"]?[0];
            var parts = candidate?["content"]?["parts"]?.AsArray()
                .Select(p => p!.AsObject().DeepClone().AsObject()).ToList() ?? [];
            return new GeminiChunk(parts, (string?)candidate?["finishReason"]);
        }
        catch (System.Text.Json.JsonException ex) { throw new GeminiUnavailableException(null, ex); }
    }

    public async Task<string> PdfToMarkdownAsync(byte[] pdf, CancellationToken ct)
    {
        var body = new JsonObject
        {
            ["contents"] = new JsonArray(new JsonObject
            {
                ["role"] = "user",
                ["parts"] = new JsonArray(
                    new JsonObject { ["inlineData"] = new JsonObject { ["mimeType"] = "application/pdf", ["data"] = Convert.ToBase64String(pdf) } },
                    new JsonObject { ["text"] = PdfPrompt }),
            }),
        };
        using var res = await SendAsync($"models/{agent.Value.ChatModel}:generateContent", body, HttpCompletionOption.ResponseContentRead, ct);
        var json = JsonNode.Parse(await ReadBodyAsync(res, ct))!;
        var parts = json["candidates"]?[0]?["content"]?["parts"]?.AsArray() ?? [];
        return string.Concat(parts.Select(p => (string?)p?["text"]));
    }

    private async Task<HttpResponseMessage> SendAsync(string path, JsonObject body, HttpCompletionOption option, CancellationToken ct)
    {
        var key = gemini.Value.ApiKey;
        if (string.IsNullOrWhiteSpace(key)) throw new GeminiNotConfiguredException();
        using var msg = new HttpRequestMessage(HttpMethod.Post, BaseUrl + path)
        {
            Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json"),
        };
        msg.Headers.Add("x-goog-api-key", key);
        HttpResponseMessage res;
        try { res = await http.SendAsync(msg, option, ct); }
        catch (HttpRequestException ex) { throw new GeminiUnavailableException(null, ex); }
        catch (TaskCanceledException ex) when (!ct.IsCancellationRequested) { throw new GeminiUnavailableException(null, ex); }

        if (res.IsSuccessStatusCode) return res;
        var status = (int)res.StatusCode;
        using (res)
        {
            if (res.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
                throw new GeminiNotConfiguredException("Gemini API key không hợp lệ hoặc không có quyền");
            if (res.StatusCode == HttpStatusCode.TooManyRequests || status >= 500)
                throw new GeminiUnavailableException(status);
            string snippet;
            try { snippet = await res.Content.ReadAsStringAsync(ct); }
            catch (Exception ex) when (ex is IOException or HttpRequestException) { snippet = ""; }
            if (snippet.Length > 500) snippet = snippet[..500];
            throw new GeminiUnavailableException(status, new HttpRequestException(snippet));
        }
    }

    private static async Task<string> ReadBodyAsync(HttpResponseMessage res, CancellationToken ct)
    {
        try { return await res.Content.ReadAsStringAsync(ct); }
        catch (IOException ex) { throw new GeminiUnavailableException(null, ex); }
    }
}
