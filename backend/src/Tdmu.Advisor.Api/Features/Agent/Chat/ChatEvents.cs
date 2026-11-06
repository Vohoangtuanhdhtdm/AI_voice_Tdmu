using Tdmu.Advisor.Api.Features.Agent.Tools;

namespace Tdmu.Advisor.Api.Features.Agent.Chat;

/// <summary>Sự kiện mà <see cref="ChatOrchestrator"/> phát ra; Task 8 chuyển thành SSE.</summary>
public interface ChatEvent;

public record Delta(string Text) : ChatEvent;
/// <param name="State">"start" hoặc "end".</param>
public record ToolStatus(string Name, string State) : ChatEvent;
public record Citations(IReadOnlyList<NguonRef> Items) : ChatEvent;
public record Done(Guid ConversationId, Guid MessageId) : ChatEvent;
public record ChatError(string Title) : ChatEvent;
