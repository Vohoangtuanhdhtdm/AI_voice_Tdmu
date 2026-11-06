using System.Text.Json.Nodes;

namespace Tdmu.Advisor.Api.Features.Conversations;

public class Message
{
    public Guid Id { get; set; }
    public Guid ConversationId { get; set; }
    public string VaiTro { get; set; } = "user";
    public string Kenh { get; set; } = "chat";
    public string NoiDung { get; set; } = "";
    public JsonNode? ToolCalls { get; set; }
    public JsonNode? LatencyMs { get; set; }
    public short DanhGia { get; set; }
    public bool CoLoi { get; set; }
    public DateTime TaoLuc { get; set; } = DateTime.UtcNow;
}
