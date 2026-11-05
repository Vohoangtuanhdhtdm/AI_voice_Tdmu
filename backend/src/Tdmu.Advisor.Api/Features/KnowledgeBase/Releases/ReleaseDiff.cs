namespace Tdmu.Advisor.Api.Features.KnowledgeBase.Releases;

[System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter))]
public enum DiffKind { Added, Modified, Removed }

public record DiffItem(string Entity, Guid LogicalId, DiffKind Kind, string Label);

public record ReleaseDiff(IReadOnlyList<DiffItem> Items);

public class NoChangesException() : Exception("Không có thay đổi để xuất bản");

public class ReleaseNotFoundException() : Exception("Không tìm thấy release");
