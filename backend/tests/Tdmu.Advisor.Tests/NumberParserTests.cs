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
    [InlineData("1.050")]
    [InlineData("21.555")]
    [InlineData("1,050")]
    public void Rejects(string? raw)
    {
        Assert.False(NumberParser.TryParseDecimal(raw, out _));
    }

    [Theory]
    [InlineData("450000", 450000)]
    [InlineData(" 450.000 ", 450000)]
    [InlineData("450,000", 450000)]
    [InlineData("1.200.000", 1200000)]
    [InlineData("1,200,000", 1200000)]
    [InlineData("15000000", 15000000)]
    public void Money_accepts(string raw, int expected)
    {
        Assert.True(NumberParser.TryParseMoney(raw, out var v));
        Assert.Equal(expected, v);
    }

    [Theory]
    [InlineData("450.5")]
    [InlineData("450,50")]
    [InlineData("1.200,000")]
    [InlineData("1,200.000")]
    [InlineData("1.20.000")]
    [InlineData("1234.567")]
    [InlineData("-450000")]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("abc")]
    public void Money_rejects(string? raw)
    {
        Assert.False(NumberParser.TryParseMoney(raw, out _));
    }
}
