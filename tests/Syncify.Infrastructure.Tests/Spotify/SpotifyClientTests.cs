using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Syncify.Application.Spotify;

namespace Syncify.Infrastructure.Tests.Spotify;

public sealed class SpotifyClientTests : IDisposable
{
    private const string Query = "leo de la izvoare daca dragostea era pe bani";

    private readonly SpotifyApiFake api = new();

    public void Dispose() => api.Dispose();

    [Fact]
    public async Task SearchTracksAsync_RecordedResponse_ReturnsTracksInSpotifyOrder()
    {
        var result = await api.CreateClient().SearchTracksAsync(Query, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Tracks.Count.ShouldBe(10);
        var first = result.Tracks[0];
        first.Id.ShouldBe("5Cmg8c4WWohkaXwIYGL20n");
        first.Name.ShouldBe("Dacă dragostea era pe bani");
        first.Artists.ShouldBe(["Leo de la Izvoare", "Theo Rose"]);
        first.Duration.ShouldBe(TimeSpan.FromMilliseconds(130063));
        first.AlbumName.ShouldBe("Dacă dragostea era pe bani");
        result.Tracks[1].Name.ShouldBe("Daca dragostea dispare");
    }

    [Fact]
    public void SpotifyTrack_Uri_IsTheSpotifyTrackUri()
    {
        var track = new SpotifyTrack("5Cmg8c4WWohkaXwIYGL20n", "Song", ["Artist"], TimeSpan.FromMinutes(3), "Album");

        track.Uri.ShouldBe("spotify:track:5Cmg8c4WWohkaXwIYGL20n");
    }

    [Fact]
    public async Task SearchTracksAsync_StrictQuery_IsSentEscapedWithTypeAndLimit()
    {
        api.RespondToSearch("search-strict.json");

        var result = await api.CreateClient().SearchTracksAsync("track:daca dragostea artist:leo de la izvoare", CancellationToken.None);

        result.Tracks!.ShouldHaveSingleItem().Id.ShouldBe("5Cmg8c4WWohkaXwIYGL20n");
        var request = api.RequestsTo(SpotifyApiFake.SearchPath).ShouldHaveSingleItem();
        SpotifyApiFake.QueryValue(request, "q").ShouldBe("track:daca dragostea artist:leo de la izvoare");
        SpotifyApiFake.QueryValue(request, "type").ShouldBe("track");
        SpotifyApiFake.QueryValue(request, "limit").ShouldBe("10");
    }

    [Theory]
    [InlineData("RO", "RO")]
    [InlineData(null, null)]
    public async Task SearchTracksAsync_Market_IsSentOnlyWhenConfigured(string? market, string? expected)
    {
        await api.CreateClient(market: market).SearchTracksAsync(Query, CancellationToken.None);

        SpotifyApiFake.QueryValue(api.RequestsTo(SpotifyApiFake.SearchPath).Single(), "market").ShouldBe(expected);
    }

    [Fact]
    public async Task SearchTracksAsync_TrackNotPlayableInMarket_IsLeftOut()
    {
        var response = JsonNode.Parse(SpotifyApiFake.Fixture("search-free-text.json"))!;
        response["tracks"]!["items"]![0]!["is_playable"] = false;
        api.RespondToSearchWithJson(response.ToJsonString());

        var result = await api.CreateClient(market: "RO").SearchTracksAsync(Query, CancellationToken.None);

        result.Tracks!.Count.ShouldBe(9);
        result.Tracks.ShouldNotContain(track => track.Id == "5Cmg8c4WWohkaXwIYGL20n");
    }

    [Fact]
    public async Task SearchTracksAsync_NoResults_ReturnsEmptyList()
    {
        api.RespondToSearchWithJson("""{ "tracks": { "items": [] } }""");

        var result = await api.CreateClient().SearchTracksAsync("zzzz qqqq", CancellationToken.None);

        result.Tracks!.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task SearchTracksAsync_BlankQuery_ReturnsEmptyWithoutCallingApi(string query)
    {
        var result = await api.CreateClient().SearchTracksAsync(query, CancellationToken.None);

        result.Tracks!.ShouldBeEmpty();
        api.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task SearchTracksAsync_AnyRequest_UsesClientCredentialsTokenAsBearer()
    {
        await api.CreateClient().SearchTracksAsync(Query, CancellationToken.None);

        var tokenRequest = api.RequestsTo(SpotifyApiFake.TokenPath).ShouldHaveSingleItem();
        var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{SpotifyApiFake.ClientId}:{SpotifyApiFake.ClientSecret}"));
        SpotifyApiFake.Header(tokenRequest, "Authorization").ShouldBe($"Basic {credentials}");
        tokenRequest.Body.ShouldBe("grant_type=client_credentials");

        var searchRequest = api.RequestsTo(SpotifyApiFake.SearchPath).ShouldHaveSingleItem();
        SpotifyApiFake.Header(searchRequest, "Authorization").ShouldBe($"Bearer {SpotifyApiFake.AccessToken}");
        searchRequest.AbsoluteUrl.ShouldNotContain(SpotifyApiFake.ClientSecret);
    }

    [Fact]
    public async Task SearchTracksAsync_ConcurrentSearches_ShareOneToken()
    {
        var client = api.CreateClient();

        await Task.WhenAll(Enumerable.Range(0, 10).Select(i => client.SearchTracksAsync($"{Query} {i}", CancellationToken.None)));

        api.RequestsTo(SpotifyApiFake.TokenPath).Count.ShouldBe(1);
        api.RequestsTo(SpotifyApiFake.SearchPath).Count.ShouldBe(10);
    }

    [Fact]
    public async Task SearchTracksAsync_TokenAboutToExpire_RequestsANewOne()
    {
        // The recorded token lasts an hour and is renewed a minute early.
        var time = new FakeTimeProvider();
        var client = api.CreateClient(timeProvider: time);

        await client.SearchTracksAsync(Query, CancellationToken.None);
        time.Advance(TimeSpan.FromMinutes(58));
        await client.SearchTracksAsync(Query, CancellationToken.None);
        api.RequestsTo(SpotifyApiFake.TokenPath).Count.ShouldBe(1);

        time.Advance(TimeSpan.FromMinutes(1));
        await client.SearchTracksAsync(Query, CancellationToken.None);
        api.RequestsTo(SpotifyApiFake.TokenPath).Count.ShouldBe(2);
    }

    [Fact]
    public async Task SearchTracksAsync_InvalidCredentials_ThrowsWithoutLeakingSecret()
    {
        api.RespondToToken("Errors/invalid-client.json", 400);

        var exception = await Should.ThrowAsync<HttpRequestException>(
            () => api.CreateClient().SearchTracksAsync(Query, CancellationToken.None));

        exception.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        exception.Message.ShouldContain("invalid_client");
        exception.Message.ShouldNotContain(SpotifyApiFake.ClientSecret);
        api.RequestsTo(SpotifyApiFake.SearchPath).ShouldBeEmpty();
    }

    [Fact]
    public async Task SearchTracksAsync_BadRequest_ThrowsWithSpotifyMessage()
    {
        api.RespondToSearch("Errors/invalid-limit.json", 400);

        var exception = await Should.ThrowAsync<HttpRequestException>(
            () => api.CreateClient().SearchTracksAsync(Query, CancellationToken.None));

        exception.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        exception.Message.ShouldContain("Invalid limit");
    }

    [Theory]
    [InlineData(500)]
    [InlineData(503)]
    public async Task SearchTracksAsync_TransientServerError_RetriesAndSucceeds(int statusCode)
    {
        api.FailFirstSearch(statusCode);

        var result = await api.CreateClient().SearchTracksAsync(Query, CancellationToken.None);

        result.Tracks!.Count.ShouldBe(10);
        api.RequestsTo(SpotifyApiFake.SearchPath).Count.ShouldBe(2);
    }

    [Fact]
    public async Task SearchTracksAsync_ShortRetryAfter_WaitsThatLongThenRetries()
    {
        var time = new FakeTimeProvider();
        api.RateLimitFirstSearch("10");

        var search = api.CreateClient(timeProvider: time).SearchTracksAsync(Query, CancellationToken.None);
        await WaitUntilAsync(() => api.RequestsTo(SpotifyApiFake.SearchPath).Count == 1);
        await Task.Delay(TimeSpan.FromMilliseconds(200));

        time.Advance(TimeSpan.FromSeconds(9));
        await Task.Delay(TimeSpan.FromMilliseconds(200));
        search.IsCompleted.ShouldBeFalse();
        api.RequestsTo(SpotifyApiFake.SearchPath).Count.ShouldBe(1);

        time.Advance(TimeSpan.FromSeconds(1));
        var result = await search.WaitAsync(TimeSpan.FromSeconds(10));

        result.Tracks!.Count.ShouldBe(10);
        api.RequestsTo(SpotifyApiFake.SearchPath).Count.ShouldBe(2);
    }

    [Fact]
    public async Task SearchTracksAsync_RetryAfterLongerThanMaxWait_ReturnsRateLimitedWithoutWaiting()
    {
        // What Spotify sent after a burst of about 1,100 searches.
        api.RateLimitSearches("85992");

        var result = await api.CreateClient(maxRateLimitWait: TimeSpan.FromSeconds(30))
            .SearchTracksAsync(Query, CancellationToken.None)
            .WaitAsync(TimeSpan.FromSeconds(10));

        result.IsSuccess.ShouldBeFalse();
        result.RetryAfter.ShouldBe(TimeSpan.FromSeconds(85992));
        api.RequestsTo(SpotifyApiFake.SearchPath).Count.ShouldBe(1);
    }

    [Fact]
    public async Task SearchTracksAsync_RateLimitedOnEveryRetry_GivesUpAfterThreeRetries()
    {
        api.RateLimitSearches("0");

        var result = await api.CreateClient().SearchTracksAsync(Query, CancellationToken.None);

        result.RetryAfter.ShouldBe(TimeSpan.Zero);
        api.RequestsTo(SpotifyApiFake.SearchPath).Count.ShouldBe(4);
    }

    [Fact]
    public async Task SearchTracksAsync_RateLimitedWithoutRetryAfter_AssumesFiveSeconds()
    {
        api.RateLimitSearches(null);

        var result = await api.CreateClient(maxRateLimitWait: TimeSpan.FromSeconds(1))
            .SearchTracksAsync(Query, CancellationToken.None);

        result.RetryAfter.ShouldBe(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task SearchTracksAsync_ManySearchesAtOnce_AreSentAtTheConfiguredPace()
    {
        var time = new FakeTimeProvider();
        var client = api.CreateClient(timeProvider: time, maxRequestsPerSecond: 2);

        var searches = Enumerable.Range(0, 3).Select(i => client.SearchTracksAsync($"{Query} {i}", CancellationToken.None)).ToList();
        await SettleAsync();
        api.RequestsTo(SpotifyApiFake.SearchPath).Count.ShouldBe(1);

        time.Advance(TimeSpan.FromMilliseconds(499));
        await SettleAsync();
        api.RequestsTo(SpotifyApiFake.SearchPath).Count.ShouldBe(1);

        time.Advance(TimeSpan.FromMilliseconds(1));
        await WaitUntilAsync(() => api.RequestsTo(SpotifyApiFake.SearchPath).Count == 2);

        time.Advance(TimeSpan.FromMilliseconds(500));
        await Task.WhenAll(searches).WaitAsync(TimeSpan.FromSeconds(10));
        api.RequestsTo(SpotifyApiFake.SearchPath).Count.ShouldBe(3);
    }

    [Fact]
    public async Task SearchTracksAsync_AnotherClientWasRateLimited_WaitsForTheSamePause()
    {
        var time = new FakeTimeProvider();
        var services = api.CreateServices(timeProvider: time);
        api.RateLimitFirstSearch("10");

        var first = services.GetRequiredService<ISpotifyClient>().SearchTracksAsync(Query, CancellationToken.None);
        await WaitUntilAsync(() => api.RequestsTo(SpotifyApiFake.SearchPath).Count == 1);
        await SettleAsync();
        var second = services.GetRequiredService<ISpotifyClient>().SearchTracksAsync($"{Query} 2", CancellationToken.None);
        await SettleAsync();

        api.RequestsTo(SpotifyApiFake.SearchPath).Count.ShouldBe(1);
        second.IsCompleted.ShouldBeFalse();

        time.Advance(TimeSpan.FromSeconds(10));
        await SettleAsync();
        // After the pause the two requests still go out one pacing slot apart.
        time.Advance(TimeSpan.FromSeconds(1));
        var results = await Task.WhenAll(first, second).WaitAsync(TimeSpan.FromSeconds(10));

        results.ShouldAllBe(result => result.IsSuccess);
        api.RequestsTo(SpotifyApiFake.SearchPath).Count.ShouldBe(3);
    }

    [Fact]
    public async Task SearchTracksAsync_DuringALongPause_ReturnsRateLimitedWithoutCallingSpotify()
    {
        var time = new FakeTimeProvider();
        var client = api.CreateClient(timeProvider: time);
        api.RateLimitSearches("85992");
        await client.SearchTracksAsync(Query, CancellationToken.None);

        time.Advance(TimeSpan.FromHours(1));
        var result = await client.SearchTracksAsync(Query, CancellationToken.None);

        result.RetryAfter.ShouldBe(TimeSpan.FromSeconds(85992) - TimeSpan.FromHours(1));
        api.RequestsTo(SpotifyApiFake.SearchPath).Count.ShouldBe(1);
    }

    [Fact]
    public async Task SearchTracksAsync_TokenRejectedBeforeExpiry_GetsANewTokenAndRetriesOnce()
    {
        api.FailFirstSearch(401);

        var result = await api.CreateClient().SearchTracksAsync(Query, CancellationToken.None);

        result.Tracks!.Count.ShouldBe(10);
        api.RequestsTo(SpotifyApiFake.TokenPath).Count.ShouldBe(2);
        api.RequestsTo(SpotifyApiFake.SearchPath).Count.ShouldBe(2);
    }

    [Fact]
    public async Task SearchTracksAsync_NewTokenRejectedToo_Throws()
    {
        api.RespondToSearchWithStatus(401);

        var exception = await Should.ThrowAsync<HttpRequestException>(
            () => api.CreateClient().SearchTracksAsync(Query, CancellationToken.None));

        exception.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        api.RequestsTo(SpotifyApiFake.TokenPath).Count.ShouldBe(2);
        api.RequestsTo(SpotifyApiFake.SearchPath).Count.ShouldBe(2);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void AddInfrastructure_MissingCredentials_FailsWhenClientIsResolved(string? clientId)
    {
        Should.Throw<OptionsValidationException>(() => api.CreateClient(clientId));
    }

    [Fact]
    public void AddInfrastructure_InvalidMarket_FailsWhenClientIsResolved()
    {
        Should.Throw<OptionsValidationException>(() => api.CreateClient(market: "Romania"));
    }

    // Lets requests that are not held back reach the fake server before asserting that others are.
    private static Task SettleAsync() => Task.Delay(TimeSpan.FromMilliseconds(200));

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        while (!condition())
        {
            await Task.Delay(TimeSpan.FromMilliseconds(10), timeout.Token);
        }
    }
}
