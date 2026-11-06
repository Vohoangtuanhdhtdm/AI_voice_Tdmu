namespace Tdmu.Advisor.Api.Features.Agent.Search;

public record SearchHit(string NoiDung, string NguonTen, string? NguonUrl, string? ViTri, double Score);

/// <summary>Kind: "faq", "tai_lieu" hoặc "khong_co".</summary>
public record SearchResult(string Kind, string? FaqAnswer, IReadOnlyList<SearchHit> Hits);

public interface IKnowledgeSearch
{
    Task<SearchResult> SearchAsync(string cauHoi, KnowledgeBase.KbScope scope, CancellationToken ct);
}
