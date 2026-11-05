namespace Tdmu.Advisor.Api.Features.KnowledgeBase.Entities;

public class Release
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public int SoHieu { get; set; }
    public string GhiChu { get; set; } = "";
    public string TaoBoi { get; set; } = "";
    public DateTimeOffset TaoLuc { get; set; } = DateTimeOffset.UtcNow;
    public bool IsActive { get; set; }
}
