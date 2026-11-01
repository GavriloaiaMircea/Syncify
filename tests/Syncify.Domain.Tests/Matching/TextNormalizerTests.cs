using Syncify.Domain.Matching;

namespace Syncify.Domain.Tests.Matching;

public sealed class TextNormalizerTests
{
    [Theory]
    [InlineData("Bună, ce mai zici?", "buna ce mai zici")]
    [InlineData("ȘȚĂÎÂ şţ", "staia st")]
    [InlineData("B.U.G. Mafia", "bug mafia")]
    [InlineData("Carla’s Dreams", "carlas dreams")]
    [InlineData("What`s UP", "whats up")]
    [InlineData("What´s UP", "whats up")]
    [InlineData("NANE - COCKTAIL 🍹", "nane cocktail")]
    [InlineData("Te-astept diseara", "te astept diseara")]
    [InlineData("  lots   of\tspace ", "lots of space")]
    [InlineData("Турбуленция", "турбуленция")]
    [InlineData("𝐂𝐨𝐥𝐚 𝐒𝐨𝐧𝐠", "cola song")]
    [InlineData("ＩＮＮＡ", "inna")]
    [InlineData("", "")]
    public void Normalize_VariousText_ReturnsLowercaseWordsWithoutDiacritics(string input, string expected) =>
        TextNormalizer.Normalize(input).ShouldBe(expected);

    [Fact]
    public void Tokenize_TextWithPunctuation_ReturnsWords() =>
        TextNormalizer.Tokenize("Puya - Striga (feat. Inna)").ShouldBe(["puya", "striga", "feat", "inna"]);

    [Fact]
    public void Tokenize_OnlySymbols_ReturnsEmpty() =>
        TextNormalizer.Tokenize("🍹 - !").ShouldBeEmpty();
}
