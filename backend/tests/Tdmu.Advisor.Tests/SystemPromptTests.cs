using Tdmu.Advisor.Api.Features.Agent.Chat;

namespace Tdmu.Advisor.Tests;

public class SystemPromptTests
{
    [Fact]
    public void Build_fills_placeholders_and_keeps_core_rules()
    {
        var p = SystemPrompt.Build("chat", 2026);
        Assert.Contains("Trường Đại học Thủ Dầu Một", p);
        Assert.Contains("2026", p);
        Assert.DoesNotContain("{{", p);
        Assert.Contains("Mọi con số", p);
        Assert.Contains("cach_doc", p);
        Assert.Contains("chuyen_tiep_can_bo", p);
        Assert.Contains("Để chuyển cán bộ, xin sự đồng ý và họ tên + số điện thoại/email trong cùng lượt; nếu lịch sử chỉ còn [SĐT] hoặc [EMAIL], hãy hỏi lại thông tin liên hệ, không truyền ký hiệu che vào công cụ.", p);
    }

    [Fact]
    public void Build_varies_by_channel_and_unknown_year()
    {
        var live = SystemPrompt.Build("live", null);
        Assert.Contains("chưa xác định", live);
        Assert.Contains("2–4 câu", live);
        Assert.DoesNotContain("{{", live);
        Assert.NotEqual(live, SystemPrompt.Build("chat", null));
    }
}
