using System.Text.Json.Nodes;

namespace Tdmu.Advisor.Api.Infrastructure.Gemini;

public enum EmbeddingTask { RetrievalDocument, RetrievalQuery }

/// <param name="Contents">Phần tử <c>{ role: "user"|"model", parts: [...] }</c> theo đúng JSON của Gemini.</param>
public record GeminiRequest(
    string SystemInstruction,
    IReadOnlyList<JsonObject> Contents,
    JsonArray FunctionDeclarations,
    string? Model = null);

/// <summary>Mỗi part được giữ nguyên văn (kể cả <c>thoughtSignature</c>).</summary>
public record GeminiChunk(IReadOnlyList<JsonObject> Parts, string? FinishReason);
