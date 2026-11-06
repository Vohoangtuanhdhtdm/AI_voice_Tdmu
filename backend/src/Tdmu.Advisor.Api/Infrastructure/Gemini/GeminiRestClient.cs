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
            foreach (var e in json["embeddings"]!.AsArray())
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
            ["tools"] = new JsonArray(new JsonObject { ["functionDeclarations"] = req.FunctionDeclarations.DeepClone() }),
            ["generationConfig"] = new JsonObject { ["temperature"] = 0.3 },
        };
        var model = req.Model ?? agent.Value.ChatModel;
        using var res = await SendAsync($"models/{model}:streamGenerateContent?alt=sse", body, HttpCompletionOption.ResponseHeadersRead, ct);
        await using var stream = await res.Content.ReadAsStreamAsync(ct);
        using var reader = new StreamReader(stream, Encoding.UTF8);
        while (true)
        {
            string? line;
            try { line = await reader.ReadLineAsync(ct); }
            catch (IOException ex) { throw new GeminiUnavailableException(null, ex); }
            if (line is null) yield break;
            if (!line.StartsWith("data:", StringComparison.Ordinal)) continue;
            var payload = line["data:".Length..].Trim();
            if (payload.Length == 0) continue;
            var candidate = JsonNode.Parse(payload)?["candidates"]?[0];
            var parts = candidate?["content"]?["parts"]?.AsArray()
                .Select(p => p!.AsObject().DeepClone().AsObject()).ToList() ?? [];
            yield return new GeminiChunk(parts, (string?)candidate?["finishReason"]);
        }
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

        var status = (int)res.StatusCode;
        if (res.StatusCode == HttpStatusCode.TooManyRequests || status >= 500)
        {
            res.Dispose();
            throw new GeminiUnavailableException(status);
        }
        if (!res.IsSuccessStatusCode)
        {
            res.Dispose();
            throw new HttpRequestException($"Gemini trả về lỗi {status}.", null, res.StatusCode);
        }
        return res;
    }

    private static async Task<string> ReadBodyAsync(HttpResponseMessage res, CancellationToken ct)
    {
        try { return await res.Content.ReadAsStringAsync(ct); }
        catch (IOException ex) { throw new GeminiUnavailableException(null, ex); }
    }
}
