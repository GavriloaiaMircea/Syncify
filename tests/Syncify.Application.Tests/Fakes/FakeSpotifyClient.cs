using System.Collections.Concurrent;
using Syncify.Application.Spotify;

namespace Syncify.Application.Tests.Fakes;

/// <summary>Answers each query with the result set up for it, and with no tracks otherwise.</summary>
internal sealed class FakeSpotifyClient : ISpotifyClient
{
    private readonly ConcurrentDictionary<string, SpotifySearchResult> results = new(StringComparer.Ordinal);
    private readonly ConcurrentQueue<string> queries = new();

    public IReadOnlyList<string> Queries => [.. queries];

    public FakeSpotifyClient Returns(string query, params SpotifyTrack[] tracks)
    {
        results[query] = SpotifySearchResult.Success(tracks);
        return this;
    }

    public FakeSpotifyClient RateLimits(string query, TimeSpan retryAfter)
    {
        results[query] = SpotifySearchResult.RateLimited(retryAfter);
        return this;
    }

    public Task<SpotifySearchResult> SearchTracksAsync(string query, CancellationToken cancellationToken)
    {
        queries.Enqueue(query);
        return Task.FromResult(results.GetValueOrDefault(query) ?? SpotifySearchResult.Success([]));
    }

    public static SpotifyTrack Track(string id, string name, string artist, int seconds = 180, string? album = null) =>
        new(id, name, [artist], TimeSpan.FromSeconds(seconds), album ?? name);
}
