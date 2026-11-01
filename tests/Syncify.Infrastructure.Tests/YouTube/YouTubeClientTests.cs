using System.Net;
using Microsoft.Extensions.Options;
using Syncify.Application.YouTube;
using Syncify.Domain.Playlists;

namespace Syncify.Infrastructure.Tests.YouTube;

public sealed class YouTubeClientTests : IDisposable
{
    private const string RecordedPlaylistId = "PLWJCFxS01ji1GMkShOn-snOVu_3RaWWlm";
    private const int RecordedVideoCount = 316;
    private const int RecordedPageCount = 7;

    private readonly YouTubeApiFake api = new();

    public void Dispose() => api.Dispose();

    [Fact]
    public async Task GetPlaylistAsync_RecordedPlaylist_ReturnsPlaylistDetails()
    {
        var playlist = await GetRecordedPlaylistAsync();

        playlist.Id.Value.ShouldBe(RecordedPlaylistId);
        playlist.Title.ShouldBe("Să bată Vântu și ploaia");
        playlist.ChannelTitle.ShouldBe("Cristina Lacovei");
    }

    [Fact]
    public async Task GetPlaylistAsync_RecordedPlaylist_ReturnsEveryVideoFromAllPagesInOrder()
    {
        var playlist = await GetRecordedPlaylistAsync();

        playlist.Videos.Count.ShouldBe(RecordedVideoCount);
        playlist.Videos.Select(video => video.Position).ShouldBe(Enumerable.Range(0, RecordedVideoCount));
        playlist.Videos.Select(video => video.VideoId).ShouldBeUnique();
    }

    [Fact]
    public async Task GetPlaylistAsync_AvailableVideo_HasTitleChannelAndDuration()
    {
        var playlist = await GetRecordedPlaylistAsync();

        playlist.Videos[0].ShouldBe(new YouTubeVideo(
            "-FXv9SflAaQ",
            "Leo de la Izvoare - Dacă dragostea era pe bani | Official Video",
            "Theo Rose",
            TimeSpan.FromSeconds(132),
            0,
            VideoAvailability.Available));
    }

    [Fact]
    public async Task GetPlaylistAsync_DeletedVideos_AreUnavailableWithoutChannelOrDuration()
    {
        var playlist = await GetRecordedPlaylistAsync();

        var unavailable = playlist.Videos.Where(video => video.Availability != VideoAvailability.Available).ToList();

        unavailable.Select(video => video.Position).ShouldBe([60, 62]);
        unavailable.ShouldAllBe(video =>
            video.Availability == VideoAvailability.Unavailable
            && video.Title == "Deleted video"
            && video.ChannelTitle == null
            && video.Duration == null);
    }

    [Fact]
    public async Task GetPlaylistAsync_UnlistedVideos_AreAvailable()
    {
        var playlist = await GetRecordedPlaylistAsync();

        playlist.Videos[4].Availability.ShouldBe(VideoAvailability.Available);
        playlist.Videos[66].Availability.ShouldBe(VideoAvailability.Available);
    }

    [Fact]
    public async Task GetPlaylistAsync_RecordedPlaylist_EveryAvailableVideoHasDuration()
    {
        var playlist = await GetRecordedPlaylistAsync();

        playlist.Videos
            .Where(video => video.Availability == VideoAvailability.Available)
            .ShouldAllBe(video => video.Duration > TimeSpan.Zero);
    }

    [Fact]
    public async Task GetPlaylistAsync_RecordedPlaylist_UsesOneListCallPlusTwoPerPage()
    {
        await GetRecordedPlaylistAsync();

        api.RequestsTo("playlists").Count.ShouldBe(1);
        api.RequestsTo("playlistItems").Count.ShouldBe(RecordedPageCount);
        api.RequestsTo("videos").Count.ShouldBe(RecordedPageCount);
        api.Requests.Count.ShouldBe(1 + (2 * RecordedPageCount));
    }

    [Fact]
    public async Task GetPlaylistAsync_VideoDetails_AreRequestedForAtMostFiftyIdsAtOnce()
    {
        await GetRecordedPlaylistAsync();

        api.RequestsTo("videos")
            .Select(request => YouTubeApiFake.QueryValue(request, "id")!.Split(',').Length)
            .ShouldAllBe(count => count > 0 && count <= 50);
    }

    [Fact]
    public async Task GetPlaylistAsync_AnyRequest_SendsApiKeyAsHeaderAndNeverInUrl()
    {
        await GetRecordedPlaylistAsync();

        api.Requests.ShouldAllBe(request =>
            YouTubeApiFake.Header(request, "X-Goog-Api-Key") == YouTubeApiFake.ApiKey
            && !request.AbsoluteUrl.Contains(YouTubeApiFake.ApiKey));
    }

    [Fact]
    public async Task GetPlaylistAsync_PrivateVideo_IsPrivate()
    {
        api.RespondWithJson("playlists", """{ "items": [{ "snippet": { "title": "Mix", "channelTitle": "Me" } }] }""");
        api.RespondWithJson("playlistItems", """
            {
              "items": [{
                "snippet": { "title": "Private video", "position": 0, "resourceId": { "videoId": "abcdefghijk" } },
                "status": { "privacyStatus": "private" }
              }]
            }
            """);
        api.RespondWithJson("videos", """{ "items": [] }""");

        var result = await api.CreateClient().GetPlaylistAsync(Id("PLprivatevideos"), CancellationToken.None);

        result.Playlist!.Videos.ShouldHaveSingleItem().Availability.ShouldBe(VideoAvailability.Private);
    }

    [Fact]
    public async Task GetPlaylistAsync_EmptyPlaylist_ReturnsNoVideosWithoutCallingVideos()
    {
        api.RespondWithJson("playlists", """{ "items": [{ "snippet": { "title": "Empty", "channelTitle": "Me" } }] }""");
        api.RespondWithJson("playlistItems", """{ "items": [] }""");

        var result = await api.CreateClient().GetPlaylistAsync(Id("PLemptyplaylist"), CancellationToken.None);

        result.Playlist!.Videos.ShouldBeEmpty();
        api.RequestsTo("videos").ShouldBeEmpty();
    }

    [Fact]
    public async Task GetPlaylistAsync_PlaylistsListIsEmpty_ReturnsNotFoundWithoutReadingItems()
    {
        api.RespondWithFixture("playlists", "Errors/playlists-not-found.json", 200);

        var result = await api.CreateClient().GetPlaylistAsync(Id("PLdoesNotExist0000"), CancellationToken.None);

        result.IsSuccess.ShouldBeFalse();
        result.Error.ShouldBe(YouTubeError.NotFound);
        api.RequestsTo("playlistItems").ShouldBeEmpty();
    }

    [Theory]
    [InlineData("Errors/playlist-not-found.json", 404, YouTubeError.NotFound)]
    [InlineData("Errors/invalid-value.json", 400, YouTubeError.NotFound)]
    [InlineData("Errors/playlist-items-not-accessible.json", 403, YouTubeError.NotAccessible)]
    [InlineData("Errors/quota-exceeded.json", 403, YouTubeError.QuotaExceeded)]
    public async Task GetPlaylistAsync_PlaylistItemsFails_ReturnsError(string fixture, int statusCode, YouTubeError expected)
    {
        api.AddRecordedPlaylist(RecordedPlaylistId);
        api.RespondWithFixture("playlistItems", fixture, statusCode);

        var result = await api.CreateClient().GetPlaylistAsync(Id(RecordedPlaylistId), CancellationToken.None);

        result.Error.ShouldBe(expected);
    }

    [Theory]
    [InlineData("playlists")]
    [InlineData("videos")]
    public async Task GetPlaylistAsync_QuotaRunsOutOnAnyCall_ReturnsQuotaExceeded(string resource)
    {
        api.AddRecordedPlaylist(RecordedPlaylistId);
        api.RespondWithFixture(resource, "Errors/quota-exceeded.json", 403);

        var result = await api.CreateClient().GetPlaylistAsync(Id(RecordedPlaylistId), CancellationToken.None);

        result.Error.ShouldBe(YouTubeError.QuotaExceeded);
    }

    [Fact]
    public async Task GetPlaylistAsync_InvalidApiKey_ThrowsWithoutLeakingKey()
    {
        api.RespondWithFixture("playlists", "Errors/api-key-invalid.json", 400);
        var client = api.CreateClient("wrong-key-123");

        var exception = await Should.ThrowAsync<HttpRequestException>(
            () => client.GetPlaylistAsync(Id(RecordedPlaylistId), CancellationToken.None));

        exception.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        exception.Message.ShouldContain("badRequest");
        exception.Message.ShouldNotContain("wrong-key-123");
    }

    [Fact]
    public async Task GetPlaylistAsync_ServerErrorWithoutJsonBody_Throws()
    {
        api.RespondWithText("playlists", 503, "<html>Service Unavailable</html>");

        var exception = await Should.ThrowAsync<HttpRequestException>(
            () => api.CreateClient().GetPlaylistAsync(Id(RecordedPlaylistId), CancellationToken.None));

        exception.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void AddInfrastructure_MissingApiKey_FailsWhenClientIsResolved(string? apiKey)
    {
        Should.Throw<OptionsValidationException>(() => api.CreateClient(apiKey));
    }

    private async Task<YouTubePlaylist> GetRecordedPlaylistAsync()
    {
        api.AddRecordedPlaylist(RecordedPlaylistId);

        var result = await api.CreateClient().GetPlaylistAsync(Id(RecordedPlaylistId), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        return result.Playlist;
    }

    private static YouTubePlaylistId Id(string value) =>
        YouTubePlaylistId.TryParse(value, out var id) ? id : throw new ArgumentException($"Invalid test playlist ID '{value}'.");
}
