using Syncify.Domain.Matching;

namespace Syncify.Domain.Tests.Matching;

public sealed class TextSimilarityTests
{
    [Theory]
    [InlineData("Bună, ce mai zici?", "Buna ce mai zici")]
    [InlineData("P.O.H.U.I.", "POHUI")]
    [InlineData("Marius Moga", "Moga Marius")]
    [InlineData("", "")]
    public void Compare_EquivalentText_ReturnsOne(string left, string right) =>
        TextSimilarity.Compare(left, right).ShouldBe(1);

    [Theory]
    [InlineData("Connect-R", "Connect R")]
    [InlineData("MariusMoga", "Marius Moga")]
    [InlineData("Niciodata sa nu spui niciodata", "Niciodata sa nu spui niciodat")]
    public void Compare_SmallDifferences_ReturnsHighScore(string left, string right) =>
        TextSimilarity.Compare(left, right).ShouldBeGreaterThan(0.85);

    [Theory]
    [InlineData("Tu", "Tu si Eu")]
    [InlineData("Dalinda", "Panama")]
    [InlineData("Lidia Buble", "Alina Eremia")]
    public void Compare_DifferentText_ReturnsLowScore(string left, string right) =>
        TextSimilarity.Compare(left, right).ShouldBeLessThan(0.5);

    [Fact]
    public void Compare_OneSideEmpty_ReturnsZero() =>
        TextSimilarity.Compare("Tu", "🍹").ShouldBe(0);

    [Fact]
    public void Compare_IsSymmetric() =>
        TextSimilarity.Compare("Mi-e dor de noi", "Mi-e dor de tine")
            .ShouldBe(TextSimilarity.Compare("Mi-e dor de tine", "Mi-e dor de noi"));
}
