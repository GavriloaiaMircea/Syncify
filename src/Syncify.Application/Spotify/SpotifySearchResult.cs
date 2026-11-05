using System.Diagnostics.CodeAnalysis;

namespace Syncify.Application.Spotify;

public sealed record SpotifySearchResult
{
    private SpotifySearchResult(IReadOnlyList<SpotifyTrack>? tracks, TimeSpan? retryAfter)
    {
        Tracks = tracks;
        RetryAfter = retryAfter;
    }

    public IReadOnlyList<SpotifyTrack>? Tracks { get; }

    /// <summary>How long Spotify asked to wait when it rate limited the request for longer than the client would wait.</summary>
    public TimeSpan? RetryAfter { get; }

    [MemberNotNullWhen(true, nameof(Tracks))]
    [MemberNotNullWhen(false, nameof(RetryAfter))]
    public bool IsSuccess => Tracks is not null;

    public static SpotifySearchResult Success(IReadOnlyList<SpotifyTrack> tracks) => new(tracks, null);

    public static SpotifySearchResult RateLimited(TimeSpan retryAfter) => new(null, retryAfter);
}
