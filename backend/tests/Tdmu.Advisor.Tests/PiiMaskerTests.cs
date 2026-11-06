using Tdmu.Advisor.Api.Features.Conversations;

namespace Tdmu.Advisor.Tests;

public class PiiMaskerTests
{
    [Theory]
    [InlineData("gọi em 0912 345 678 nhé", "gọi em [SĐT] nhé")]
    [InlineData("+84912345678", "[SĐT]")]
    [InlineData("mail a.b@tdmu.edu.vn", "mail [EMAIL]")]
    [InlineData("điểm 21,5 năm 2026", "điểm 21,5 năm 2026")]
    [InlineData("mã ngành 7480201", "mã ngành 7480201")]
    public void Mask_replaces_pii(string input, string expected) =>
        Assert.Equal(expected, PiiMasker.Mask(input));
}
