using Syncify.Application.Spotify;
using Syncify.Application.Tests.Fakes;
using Syncify.Domain.Matching;
using static Syncify.Application.Tests.Fakes.FakeSpotifyClient;

namespace Syncify.Application.Tests.Spotify;

public sealed class SpotifyTrackFinderTests
{
    private const string Strict = "track:bilionera artist:otilia";
    private const string FreeText = "otilia bilionera";

    private static readonly TrackInfo Bilionera = new("Bilionera", ["Otilia"], TimeSpan.FromSeconds(180));

    [Fact]
    public async Task FindAsync_StrictQueryMatches_DoesNotSearchAgain()
    {
        var spotify = new FakeSpotifyClient().Returns(Strict, Track("1", "Bilionera", "Otilia", album: "Bilionera (Single)"));

        var result = await new SpotifyTrackFinder(spotify).FindAsync(Bilionera, CancellationToken.None);

        result.Match!.Status.ShouldBe(MatchStatus.Matched);
        result.Track!.Id.ShouldBe("1");
        result.Track.AlbumName.ShouldBe("Bilionera (Single)");
        result.Queries.ShouldBe([Strict]);
        spotify.Queries.ShouldBe([Strict]);
    }

    [Fact]
    public async Task FindAsync_StrictQueryFindsNothing_FallsBackToFreeText()
    {
        var spotify = new FakeSpotifyClient().Returns(FreeText, Track("2", "Bilionera", "Otilia"));

        var result = await new SpotifyTrackFinder(spotify).FindAsync(Bilionera, CancellationToken.None);

        result.Match!.Status.ShouldBe(MatchStatus.Matched);
        result.Track!.Id.ShouldBe("2");
        result.Queries.ShouldBe([Strict, FreeText]);
    }

    [Fact]
    public async Task FindAsync_StrictQueryFindsOnlyAnotherVersion_KeepsSearchingAndPicksTheOriginal()
    {
        var spotify = new FakeSpotifyClient()
            .Returns(Strict, Track("remix", "Bilionera - Remix", "Otilia"))
            .Returns(FreeText, Track("remix", "Bilionera - Remix", "Otilia"), Track("original", "Bilionera", "Otilia"));

        var result = await new SpotifyTrackFinder(spotify).FindAsync(Bilionera, CancellationToken.None);

        result.Match!.Status.ShouldBe(MatchStatus.Matched);
        result.Track!.Id.ShouldBe("original");
    }

    [Fact]
    public async Task FindAsync_NoQueryMatches_ReturnsBestCandidateFromAllQueries()
    {
        var spotify = new FakeSpotifyClient()
            .Returns(Strict, Track("other", "Prima Noapte", "Otilia"))
            .Returns(FreeText, Track("remix", "Bilionera - Remix", "Otilia"));

        var result = await new SpotifyTrackFinder(spotify).FindAsync(Bilionera, CancellationToken.None);

        result.Match!.Status.ShouldBe(MatchStatus.Ambiguous);
        result.Track!.Id.ShouldBe("remix");
        result.Queries.ShouldBe([Strict, FreeText]);
    }

    [Fact]
    public async Task FindAsync_NothingFound_ReturnsNotFoundWithoutTrack()
    {
        var result = await new SpotifyTrackFinder(new FakeSpotifyClient()).FindAsync(Bilionera, CancellationToken.None);

        result.Match!.Status.ShouldBe(MatchStatus.NotFound);
        result.Match.Reasons.ShouldBe([MatchReason.NoCandidates]);
        result.Track.ShouldBeNull();
        result.Queries.ShouldBe([Strict, FreeText]);
    }

    [Fact]
    public async Task FindAsync_UnknownArtist_SearchesOnceAndIsNeverMatchedOnTitleAlone()
    {
        var spotify = new FakeSpotifyClient().Returns("bilionera", Track("1", "Bilionera", "Otilia"));

        var result = await new SpotifyTrackFinder(spotify).FindAsync(new TrackInfo("Bilionera", []), CancellationToken.None);

        result.Match!.Status.ShouldBe(MatchStatus.Ambiguous);
        result.Track!.Id.ShouldBe("1");
        spotify.Queries.ShouldBe(["bilionera"]);
    }

    [Fact]
    public async Task FindAsync_ArtistNamedInTitle_IsMatchedFromFreeTextSearch()
    {
        var spotify = new FakeSpotifyClient().Returns(
            "theo rose x valentin dinu pacatul meu",
            new SpotifyTrack("1", "Pacatul Meu", ["Theo Rose", "Valentin Dinu"], TimeSpan.FromMinutes(3), "Pacatul Meu"));

        var result = await new SpotifyTrackFinder(spotify)
            .FindAsync(new TrackInfo("Theo Rose x Valentin Dinu Pacatul Meu", []), CancellationToken.None);

        result.Match!.Status.ShouldBe(MatchStatus.Matched);
        result.Match.Reasons.ShouldContain(MatchReason.ArtistFoundInTitle);
    }

    [Fact]
    public async Task FindAsync_RateLimited_StopsAndReportsRetryAfter()
    {
        var spotify = new FakeSpotifyClient()
            .Returns(Strict, Track("other", "Prima Noapte", "Otilia"))
            .RateLimits(FreeText, TimeSpan.FromMinutes(5));

        var result = await new SpotifyTrackFinder(spotify).FindAsync(Bilionera, CancellationToken.None);

        result.IsSuccess.ShouldBeFalse();
        result.RetryAfter.ShouldBe(TimeSpan.FromMinutes(5));
        result.Track.ShouldBeNull();
        result.Queries.ShouldBe([Strict, FreeText]);
    }

    [Fact]
    public async Task FindAsync_NothingSearchable_ReturnsNotFoundWithoutSearching()
    {
        var spotify = new FakeSpotifyClient();

        var result = await new SpotifyTrackFinder(spotify).FindAsync(new TrackInfo("🔥", ["Otilia"]), CancellationToken.None);

        result.Match!.Status.ShouldBe(MatchStatus.NotFound);
        spotify.Queries.ShouldBeEmpty();
    }
}
