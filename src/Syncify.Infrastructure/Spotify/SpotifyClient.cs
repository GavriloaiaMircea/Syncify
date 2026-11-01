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

    // Spotify documents Retry-After on every 429, so this only covers a proxy that drops the header.
    private static readonly TimeSpan DefaultRetryAfter = TimeSpan.FromSeconds(5);

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
                var retryAfter = RetryAfter(response);
                if (attempt == MaxRateLimitRetries || retryAfter > settings.MaxRateLimitWait)
                {
                    return SpotifySearchResult.RateLimited(retryAfter);
                }

                await Task.Delay(retryAfter, timeProvider, cancellationToken);
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

    private TimeSpan RetryAfter(HttpResponseMessage response) => response.Headers.RetryAfter switch
    {
        { Delta: { } delta } => delta,
        { Date: { } date } => TimeSpan.FromTicks(Math.Max(0, (date - timeProvider.GetUtcNow()).Ticks)),
        _ => DefaultRetryAfter,
    };

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
