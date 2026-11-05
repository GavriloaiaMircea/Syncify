using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.Options;
using Syncify.Application.Spotify;

namespace Syncify.Infrastructure.Spotify;

/// <summary>Searches the Spotify Web API. <see cref="SpotifyAuthenticationHandler"/> adds the app token.</summary>
internal sealed class SpotifyClient(HttpClient http, IOptions<SpotifyOptions> options, TimeProvider timeProvider) : ISpotifyClient
{
    // The most Spotify accepts for apps in development mode; larger values fail with "Invalid limit".
    private const string PageSize = "10";

    private const int MaxRateLimitRetries = 3;

    public async Task<SpotifySearchResult> SearchTracksAsync(string query, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return SpotifySearchResult.Success([]);
        }

        var settings = options.Value;
        var url = $"search?q={Uri.EscapeDataString(query)}&type=track&limit={PageSize}";
        if (settings.Market is { } market)
        {
            url += $"&market={market}";
        }

        for (var attempt = 0; ; attempt++)
        {
            using var response = await http.GetAsync(url, cancellationToken);

            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                var retryAfter = SpotifyRetryAfter.Read(response, timeProvider.GetUtcNow());
                if (attempt == MaxRateLimitRetries || retryAfter > settings.MaxRateLimitWait)
                {
                    return SpotifySearchResult.RateLimited(retryAfter);
                }

                // SpotifyRateLimitHandler holds the retry, and every other request, until Retry-After has passed.
                continue;
            }

            if (!response.IsSuccessStatusCode)
            {
                var message = await ReadErrorMessageAsync(response, cancellationToken);
                throw new HttpRequestException(
                    $"Spotify search failed with {(int)response.StatusCode} ({message ?? "no message"}).",
                    inner: null,
                    response.StatusCode);
            }

            var body = await response.Content.ReadFromJsonAsync<SearchResponse>(cancellationToken)
                ?? throw new InvalidOperationException("Spotify search returned an empty body.");

            // With a market, tracks that are not licensed there still come back, marked unplayable.
            return SpotifySearchResult.Success(body.Tracks.Items
                .Where(track => track.IsPlayable != false)
                .Select(ToTrack)
                .ToList());
        }
    }

    private static SpotifyTrack ToTrack(TrackObject track) => new(
        track.Id,
        track.Name,
        track.Artists.Select(artist => artist.Name).ToList(),
        TimeSpan.FromMilliseconds(track.DurationMs),
        track.Album.Name);

    private static async Task<string?> ReadErrorMessageAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            var body = await response.Content.ReadFromJsonAsync<ApiErrorResponse>(cancellationToken);
            return body?.Error?.Message;
        }
        catch (System.Text.Json.JsonException)
        {
            return null;
        }
    }
}
