namespace Tdmu.Advisor.Api.Infrastructure.Gemini;

public interface IGeminiClient
{
    Task<IReadOnlyList<float[]>> EmbedAsync(IReadOnlyList<string> texts, EmbeddingTask task, CancellationToken ct);
    IAsyncEnumerable<GeminiChunk> StreamGenerateAsync(GeminiRequest req, CancellationToken ct);
    Task<string> PdfToMarkdownAsync(byte[] pdf, CancellationToken ct);
}
