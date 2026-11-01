using System.Diagnostics.CodeAnalysis;

namespace Syncify.Application.YouTube;

public sealed record YouTubePlaylistResult
{
    private YouTubePlaylistResult(YouTubePlaylist? playlist, YouTubeError? error)
    {
        Playlist = playlist;
        Error = error;
    }

    public YouTubePlaylist? Playlist { get; }

    public YouTubeError? Error { get; }

    [MemberNotNullWhen(true, nameof(Playlist))]
    [MemberNotNullWhen(false, nameof(Error))]
    public bool IsSuccess => Playlist is not null;

    public static YouTubePlaylistResult Success(YouTubePlaylist playlist) => new(playlist, null);

    public static YouTubePlaylistResult Failure(YouTubeError error) => new(null, error);
}

public enum YouTubeError
{
    /// <summary>The playlist does not exist or is private; with an API key YouTube cannot tell the two apart.</summary>
    NotFound,

    /// <summary>The playlist exists but its items cannot be read.</summary>
    NotAccessible,

    /// <summary>The daily API quota is used up; it resets at midnight Pacific time.</summary>
    QuotaExceeded,

    /// <summary>
    /// A list YouTube generates (Mixes and YouTube Music radio, IDs starting with "RD"). The API pages through
    /// them in a loop, so they cannot be read reliably.
    /// </summary>
    Unsupported,
}
