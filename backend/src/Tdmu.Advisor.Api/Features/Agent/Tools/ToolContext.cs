using Tdmu.Advisor.Api.Features.KnowledgeBase;

namespace Tdmu.Advisor.Api.Features.Agent.Tools;

/// <summary><see cref="ConversationId"/> = <see cref="Guid.Empty"/> khi chưa gắn hội thoại (ví dụ test).</summary>
public record ToolContext(KbScope Scope, Guid ConversationId);
