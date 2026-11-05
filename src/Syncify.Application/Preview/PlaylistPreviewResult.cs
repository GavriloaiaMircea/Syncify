using System.Diagnostics.CodeAnalysis;
using Syncify.Application.YouTube;

namespace Syncify.Application.Preview;

public sealed record PlaylistPreviewResult
{
    private PlaylistPreviewResult(PlaylistPreview? preview, YouTubeError? youTubeError, TimeSpan? spotifyRetryAfter)
    {
        Preview = preview;
        YouTubeError = youTubeError;
        SpotifyRetryAfter = spotifyRetryAfter;
    }

    public PlaylistPreview? Preview { get; }

    public YouTubeError? YouTubeError { get; }

    /// <summary>Set when Spotify rate limited the search for longer than the client waits.</summary>
    public TimeSpan? SpotifyRetryAfter { get; }

    [MemberNotNullWhen(true, nameof(Preview))]
    public bool IsSuccess => Preview is not null;

    public static PlaylistPreviewResult Success(PlaylistPreview preview) => new(preview, null, null);

    public static PlaylistPreviewResult Failure(YouTubeError error) => new(null, error, null);

    public static PlaylistPreviewResult SpotifyRateLimited(TimeSpan retryAfter) => new(null, null, retryAfter);
}
