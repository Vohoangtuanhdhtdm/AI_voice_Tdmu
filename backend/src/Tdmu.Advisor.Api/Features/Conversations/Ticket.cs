namespace Tdmu.Advisor.Api.Features.Conversations;

public class Ticket
{
    public Guid Id { get; set; }
    public Guid? ConversationId { get; set; }
    public string HoTen { get; set; } = "";
    public string LienHe { get; set; } = "";
    public string LyDo { get; set; } = "";
    public string TrangThai { get; set; } = "moi";
    public DateTime TaoLuc { get; set; } = DateTime.UtcNow;
}
