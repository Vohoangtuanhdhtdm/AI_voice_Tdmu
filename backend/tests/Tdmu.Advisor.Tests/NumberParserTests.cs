using Tdmu.Advisor.Api.Features.KnowledgeBase.Import;

namespace Tdmu.Advisor.Tests;

public class NumberParserTests
{
    [Theory]
    [InlineData("21,5", "21.5")]
    [InlineData("21.5", "21.5")]
    [InlineData(" 21,50 ", "21.50")]
    [InlineData("850", "850")]
    [InlineData("-1,5", "-1.5")]
    [InlineData(" 21,5 ", "21.5")]
    public void Accepts(string raw, string expected)
    {
        Assert.True(NumberParser.TryParseDecimal(raw, out var v));
        Assert.Equal(decimal.Parse(expected, System.Globalization.CultureInfo.InvariantCulture), v);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    [InlineData("abc")]
    [InlineData("1.234,5")]
    [InlineData("1,234.5")]
    [InlineData("1,2,3")]
    [InlineData("21,")]
    [InlineData("1e5")]
    public void Rejects(string? raw)
    {
        Assert.False(NumberParser.TryParseDecimal(raw, out _));
    }
}
