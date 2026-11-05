using Syncify.Application.Spotify;
using Syncify.Domain.Matching;

namespace Syncify.Application.Tests.Spotify;

public sealed class SpotifySearchQueryTests
{
    [Fact]
    public void For_KnownArtist_ReturnsStrictThenFreeTextQuery()
    {
        SpotifySearchQuery.For(Track("Bilionera", "Otilia"))
            .ShouldBe(["track:bilionera artist:otilia", "otilia bilionera"]);
    }

    [Fact]
    public void For_DiacriticsAndCapitals_AreNormalized()
    {
        SpotifySearchQuery.For(Track("Dacă dragostea era pe BANI", "Leo de la Izvoare"))
            .ShouldBe(["track:daca dragostea era pe bani artist:leo de la izvoare", "leo de la izvoare daca dragostea era pe bani"]);
    }

    [Fact]
    public void For_VersionInBrackets_IsOnlyInFreeTextQuery()
    {
        // Spotify writes versions as "Strig La Cer - Live", so the strict query must not require the brackets.
        SpotifySearchQuery.For(Track("Strig la cer (Live)", "Adrian Minune"))
            .ShouldBe(["track:strig la cer artist:adrian minune", "adrian minune strig la cer live"]);
    }

    [Fact]
    public void For_SeveralArtists_FiltersOnTheFirst()
    {
        SpotifySearchQuery.For(Track("Mai Stai", "ASU", "Vali Vijelie", "Boby"))
            .ShouldBe(["track:mai stai artist:asu", "asu mai stai"]);
    }

    [Fact]
    public void For_UnknownArtist_SearchesTheWholeTitleOnce()
    {
        // The title may still name the artist ("Theo Rose x Valentin Dinu Pacatul Meu"), which free text finds.
        SpotifySearchQuery.For(Track("Theo Rose x Valentin Dinu Pacatul Meu"))
            .ShouldBe(["theo rose x valentin dinu pacatul meu"]);
    }

    [Theory]
    [InlineData("Senorita (\"Beau, beau ...\")", "Dl. Problema")]
    [InlineData("Remix: partea a doua", "Artist: X")]
    public void For_QuotesAndColonsInTitle_CannotBecomeQuerySyntax(string title, string artist)
    {
        var queries = SpotifySearchQuery.For(Track(title, artist));

        queries.ShouldAllBe(query => !query.Contains('"'));
        queries[0].Count(c => c == ':').ShouldBe(2);
        queries[1].ShouldNotContain(':');
    }

    [Fact]
    public void For_TitleIsOnlyABracket_UsesItRatherThanAnEmptyFilter()
    {
        SpotifySearchQuery.For(Track("(Intro)", "Delia"))
            .ShouldBe(["track:intro artist:delia", "delia intro"]);
    }

    [Theory]
    [InlineData("")]
    [InlineData("❤️🔥")]
    public void For_NothingSearchable_ReturnsNoQueries(string title)
    {
        SpotifySearchQuery.For(Track(title, "Delia")).ShouldBeEmpty();
    }

    [Fact]
    public void For_BlankArtist_IsTreatedAsUnknown()
    {
        SpotifySearchQuery.For(Track("Bilionera", " ")).ShouldBe(["bilionera"]);
    }

    private static TrackInfo Track(string title, params string[] artists) => new(title, artists);
}
