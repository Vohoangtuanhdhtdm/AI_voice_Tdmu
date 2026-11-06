using System.Text.Json.Nodes;

namespace Tdmu.Advisor.Api.Features.Agent.Tools;

/// <summary>Tool chỉ đọc mà mô hình được gọi. Đăng ký scoped trong DI; <see cref="ToolRegistry"/> gom tất cả.</summary>
public interface IAgentTool
{
    string Name { get; }
    /// <summary><c>{ name, description, parameters }</c> theo FunctionDeclaration của Gemini.</summary>
    JsonObject Declaration { get; }
    Task<ToolResult> InvokeAsync(JsonObject args, ToolContext ctx, CancellationToken ct);
}
