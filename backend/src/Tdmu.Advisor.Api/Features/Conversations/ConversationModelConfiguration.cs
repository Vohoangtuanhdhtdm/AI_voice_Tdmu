using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Tdmu.Advisor.Api.Features.Conversations;

public static class ConversationModelConfiguration
{
    public static void Configure(ModelBuilder mb)
    {
        var json = new ValueConverter<JsonNode?, string?>(
            v => v == null ? null : v.ToJsonString(null),
            v => v == null ? null : JsonNode.Parse(v, null, default));

        mb.Entity<Conversation>(e =>
        {
            e.ToTable("conversation");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.DongYGiongNoi).HasDefaultValue(false);
        });

        mb.Entity<Message>(e =>
        {
            e.ToTable("message");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.ToolCalls).HasColumnType("jsonb").HasConversion(json);
            e.Property(x => x.LatencyMs).HasColumnType("jsonb").HasConversion(json);
            e.Property(x => x.DanhGia).HasDefaultValue((short)0);
            e.HasOne<Conversation>().WithMany().HasForeignKey(x => x.ConversationId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => new { x.ConversationId, x.TaoLuc });
        });

        mb.Entity<Ticket>(e =>
        {
            e.ToTable("ticket");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.TrangThai).HasDefaultValue("moi");
            e.HasOne<Conversation>().WithMany().HasForeignKey(x => x.ConversationId).OnDelete(DeleteBehavior.SetNull);
        });
    }
}
