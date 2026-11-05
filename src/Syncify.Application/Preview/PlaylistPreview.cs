using Syncify.Application.YouTube;

namespace Syncify.Application.Preview;

/// <summary>What importing a YouTube playlist into Spotify would produce, track by track in playlist order.</summary>
public sealed record PlaylistPreview(YouTubePlaylist Playlist, IReadOnlyList<TrackPreview> Tracks)
{
    public int Count(PreviewStatus status) => Tracks.Count(track => track.Status == status);
}
