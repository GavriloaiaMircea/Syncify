using Syncify.Domain.Matching;

namespace Syncify.Domain.Tests.Matching;

public sealed class ArtistSplitterTests
{
    [Theory]
    [InlineData("INNA", "INNA")]
    [InlineData("INNA feat. Yandel", "INNA|Yandel")]
    [InlineData("Akcent feat Lidia Buble & DDY Nunes", "Akcent|Lidia Buble|DDY Nunes")]
    [InlineData("Tom Boxer ft Antonia", "Tom Boxer|Antonia")]
    [InlineData("Corina + Mira + Skizzo Skillz", "Corina|Mira|Skizzo Skillz")]
    [InlineData("Puya cu Doddy, Posset, Mahia si Alex Velea", "Puya|Doddy|Posset|Mahia|Alex Velea")]
    [InlineData("LeLe și Narcisa", "LeLe|Narcisa")]
    [InlineData("Mario Fresh X Dorian Popa", "Mario Fresh|Dorian Popa")]
    [InlineData("Grasu XXL | Maximilian | Spike", "Grasu XXL|Maximilian|Spike")]
    [InlineData("Tzanca Uraganu ❌ Florin Salam ❌ Mr Juve", "Tzanca Uraganu|Florin Salam|Mr Juve")]
    [InlineData("Theo Rose 💔 Florin Salam", "Theo Rose|Florin Salam")]
    [InlineData("Guess Who ✘ @MariusMoga_", "Guess Who|MariusMoga")]
    [InlineData("Mariano 〽️🌍", "Mariano")]
    [InlineData("B.U.G. Mafia", "B.U.G. Mafia")]
    [InlineData("INNA feat. Inna", "INNA")]
    public void Split_ArtistCredit_ReturnsIndividualArtists(string credit, string expected) =>
        ArtistSplitter.Split(credit).ShouldBe(expected.Split('|'));

    [Fact]
    public void Split_Empty_ReturnsNoArtists() =>
        ArtistSplitter.Split("  ").ShouldBeEmpty();
}
