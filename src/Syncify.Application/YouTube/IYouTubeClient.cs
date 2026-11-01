using Syncify.Domain.Playlists;

namespace Syncify.Application.YouTube;

public interface IYouTubeClient
{
    /// <summary>
    /// Reads a public or unlisted playlist with all its videos, in playlist order.
    /// Costs 1 quota unit plus 2 per 50 videos.
    /// </summary>
    Task<YouTubePlaylistResult> GetPlaylistAsync(YouTubePlaylistId playlistId, CancellationToken cancellationToken);
}
