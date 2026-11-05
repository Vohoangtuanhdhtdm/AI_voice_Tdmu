using Tdmu.Advisor.Api.Features.KnowledgeBase.Import;

namespace Tdmu.Advisor.Tests;

public class ToHopReadingTests
{
    [Theory]
    [InlineData("A00", "A không không")]
    [InlineData("D14", "D một bốn")]
    [InlineData("C19", "C một chín")]
    public void Generate_reads_each_digit(string ma, string expected) =>
        Assert.Equal(expected, ToHopReading.Generate(ma));
}
