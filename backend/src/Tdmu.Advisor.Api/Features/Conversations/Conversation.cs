namespace Tdmu.Advisor.Api.Features.Conversations;

public class Conversation
{
    public Guid Id { get; set; }
    public string KenhDauTien { get; set; } = "chat";
    public DateTime BatDau { get; set; } = DateTime.UtcNow;
    public string? UserAgent { get; set; }
    public Guid? ReleaseId { get; set; }
    public bool LaHoiThu { get; set; }
    public bool DongYGiongNoi { get; set; }
}
