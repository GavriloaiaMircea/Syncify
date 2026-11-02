using Syncify.Application.Spotify;
using Syncify.Application.YouTube;
using Syncify.Domain.Matching;
using Syncify.Domain.Playlists;

namespace Syncify.Application.Preview;

/// <summary>Matches every video of a YouTube playlist against Spotify without changing anything on either side.</summary>
public sealed class PlaylistPreviewService(IYouTubeClient youTube, SpotifyTrackFinder finder)
{
    // The Spotify client paces requests app-wide; two in flight only hide each other's latency.
    private const int MaxConcurrentSearches = 2;

    public async Task<PlaylistPreviewResult> PreviewAsync(YouTubePlaylistId playlistId, CancellationToken cancellationToken)
    {
        var playlistResult = await youTube.GetPlaylistAsync(playlistId, cancellationToken);
        if (!playlistResult.IsSuccess)
        {
            return PlaylistPreviewResult.Failure(playlistResult.Error.Value);
        }

        var videos = playlistResult.Playlist.Videos;
        var tracks = new TrackPreview[videos.Count];
        TimeSpan? retryAfter = null;

        using var stopSearching = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var options = new ParallelOptions { MaxDegreeOfParallelism = MaxConcurrentSearches, CancellationToken = stopSearching.Token };

        try
        {
            await Parallel.ForEachAsync(Enumerable.Range(0, videos.Count), options, async (index, token) =>
            {
                var video = videos[index];
                if (video.Availability != VideoAvailability.Available)
                {
                    tracks[index] = new TrackPreview(video, null, null);
                    return;
                }

                var parsed = YouTubeTitleParser.Parse(video.Title, video.ChannelTitle);
                var search = await finder.FindAsync(parsed.ToTrack(video.Duration), token);

                if (!search.IsSuccess)
                {
                    // Every further request would be rate limited too, so stop and report instead.
                    retryAfter = search.RetryAfter;
                    await stopSearching.CancelAsync();
                    return;
                }

                tracks[index] = new TrackPreview(video, parsed, search);
            });
        }
        catch (OperationCanceledException) when (retryAfter is not null && !cancellationToken.IsCancellationRequested)
        {
        }

        return retryAfter is { } wait
            ? PlaylistPreviewResult.SpotifyRateLimited(wait)
            : PlaylistPreviewResult.Success(new PlaylistPreview(playlistResult.Playlist, tracks));
    }
}
