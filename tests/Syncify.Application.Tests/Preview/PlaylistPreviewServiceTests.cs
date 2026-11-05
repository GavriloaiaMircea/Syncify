using Syncify.Application.Preview;
using Syncify.Application.Spotify;
using Syncify.Application.Tests.Fakes;
using Syncify.Application.YouTube;
using static Syncify.Application.Tests.Fakes.FakeSpotifyClient;
using static Syncify.Application.Tests.Fakes.FakeYouTubeClient;

namespace Syncify.Application.Tests.Preview;

public sealed class PlaylistPreviewServiceTests
{
    [Theory]
    [InlineData(YouTubeError.NotFound)]
    [InlineData(YouTubeError.QuotaExceeded)]
    [InlineData(YouTubeError.Unsupported)]
    public async Task PreviewAsync_PlaylistCannotBeRead_ReturnsYouTubeError(YouTubeError error)
    {
        var spotify = new FakeSpotifyClient();
        var service = new PlaylistPreviewService(new FakeYouTubeClient(YouTubePlaylistResult.Failure(error)), new SpotifyTrackFinder(spotify));

        var result = await service.PreviewAsync(PlaylistId, CancellationToken.None);

        result.IsSuccess.ShouldBeFalse();
        result.YouTubeError.ShouldBe(error);
        spotify.Queries.ShouldBeEmpty();
    }

    [Fact]
    public async Task PreviewAsync_MixedPlaylist_ReportsEveryVideoInPlaylistOrder()
    {
        var youTube = WithVideos(
            Video(0, "Otilia - Bilionera (official video)"),
            DeletedVideo(1),
            Video(2, "Delia - Ce are ea"),
            Video(3, "Nimeni - Nimic"));
        var spotify = new FakeSpotifyClient()
            .Returns("track:bilionera artist:otilia", Track("bilionera", "Bilionera", "Otilia"))
            .Returns("delia ce are ea", Track("other", "Ce are ea - Remix", "Delia"));

        var result = await Preview(youTube, spotify);

        var preview = result.Preview!;
        preview.Playlist.Title.ShouldBe("Test playlist");
        preview.Tracks.Select(track => track.Video.Position).ShouldBe([0, 1, 2, 3]);
        preview.Tracks.Select(track => track.Status)
            .ShouldBe([PreviewStatus.Matched, PreviewStatus.Skipped, PreviewStatus.Ambiguous, PreviewStatus.NotFound]);
        preview.Tracks[0].Search!.Track!.Id.ShouldBe("bilionera");
        preview.Tracks[0].Parsed!.Song.ShouldBe("Bilionera");
    }

    [Fact]
    public async Task PreviewAsync_UnavailableVideo_IsSkippedWithoutParsingOrSearching()
    {
        var spotify = new FakeSpotifyClient();

        var result = await Preview(WithVideos(DeletedVideo(0)), spotify);

        var track = result.Preview!.Tracks.ShouldHaveSingleItem();
        track.Status.ShouldBe(PreviewStatus.Skipped);
        track.Parsed.ShouldBeNull();
        track.Search.ShouldBeNull();
        spotify.Queries.ShouldBeEmpty();
    }

    [Fact]
    public async Task PreviewAsync_TopicChannel_SearchesWithTheChannelArtist()
    {
        var spotify = new FakeSpotifyClient();

        await Preview(WithVideos(Video(0, "Bilionera", channel: "Otilia - Topic")), spotify);

        spotify.Queries.ShouldBe(["track:bilionera artist:otilia", "otilia bilionera"]);
    }

    [Fact]
    public async Task PreviewAsync_VideoDuration_IsUsedForMatching()
    {
        var spotify = new FakeSpotifyClient().Returns(
            "track:bilionera artist:otilia",
            Track("long", "Bilionera", "Otilia", seconds: 400),
            Track("right", "Bilionera", "Otilia", seconds: 181));

        var result = await Preview(WithVideos(Video(0, "Otilia - Bilionera", seconds: 180)), spotify);

        result.Preview!.Tracks[0].Search!.Track!.Id.ShouldBe("right");
    }

    [Fact]
    public async Task PreviewAsync_SpotifyRateLimits_ReturnsRetryAfterInsteadOfAPartialPreview()
    {
        var youTube = WithVideos([.. Enumerable.Range(0, 20).Select(i => Video(i, $"Artist {i} - Song {i}"))]);
        var spotify = new FakeSpotifyClient().RateLimits("track:song 5 artist:artist 5", TimeSpan.FromHours(24));

        var result = await Preview(youTube, spotify);

        result.IsSuccess.ShouldBeFalse();
        result.SpotifyRetryAfter.ShouldBe(TimeSpan.FromHours(24));
        result.YouTubeError.ShouldBeNull();
        spotify.Queries.Count.ShouldBeLessThan(40);
    }

    [Fact]
    public async Task PreviewAsync_Cancelled_Throws()
    {
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        var service = new PlaylistPreviewService(WithVideos(Video(0, "Otilia - Bilionera")), new SpotifyTrackFinder(new FakeSpotifyClient()));

        await Should.ThrowAsync<OperationCanceledException>(() => service.PreviewAsync(PlaylistId, cancellation.Token));
    }

    [Fact]
    public void Count_CountsTracksWithStatus()
    {
        var preview = new PlaylistPreview(
            new YouTubePlaylist(PlaylistId, "Test", "Me", []),
            [new TrackPreview(DeletedVideo(0), null, null), new TrackPreview(DeletedVideo(1), null, null)]);

        preview.Count(PreviewStatus.Skipped).ShouldBe(2);
        preview.Count(PreviewStatus.Matched).ShouldBe(0);
    }

    private static Task<PlaylistPreviewResult> Preview(FakeYouTubeClient youTube, FakeSpotifyClient spotify) =>
        new PlaylistPreviewService(youTube, new SpotifyTrackFinder(spotify)).PreviewAsync(PlaylistId, CancellationToken.None);
}
