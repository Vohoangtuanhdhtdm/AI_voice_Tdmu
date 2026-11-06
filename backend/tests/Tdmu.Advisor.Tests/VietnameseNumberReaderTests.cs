using Tdmu.Advisor.Api.Features.Agent.Tools;

namespace Tdmu.Advisor.Tests;

public class VietnameseNumberReaderTests
{
    [Theory]
    [InlineData("21.5", "hai mươi mốt phẩy năm")]
    [InlineData("25", "hai mươi lăm")]
    [InlineData("14", "mười bốn")]
    [InlineData("105", "một trăm lẻ năm")]
    [InlineData("1200", "một nghìn hai trăm")]
    [InlineData("2026", "hai nghìn không trăm hai mươi sáu")]
    [InlineData("24.75", "hai mươi bốn phẩy bảy mươi lăm")]
    [InlineData("0", "không")]
    // Ngoài bảng của brief: numeric(7,2) trả về "21.50" nên phải bỏ số 0 ở cuối; số 0 đầu phần thập phân đọc riêng.
    [InlineData("21.50", "hai mươi mốt phẩy năm")]
    [InlineData("8.05", "tám phẩy không năm")]
    [InlineData("15", "mười lăm")]
    [InlineData("10", "mười")]
    [InlineData("1000005", "một triệu không trăm lẻ năm")]
    [InlineData("2500000000", "hai tỷ năm trăm triệu")]
    public void Read(string input, string expected) =>
        Assert.Equal(expected, VietnameseNumberReader.Read(decimal.Parse(input, System.Globalization.CultureInfo.InvariantCulture)));

    [Fact]
    public void ReadMoney() => Assert.Equal("một triệu hai trăm nghìn đồng", VietnameseNumberReader.ReadMoney(1200000));
}
