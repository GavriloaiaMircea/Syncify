using Syncify.Application.YouTube;
using Syncify.Domain.Playlists;

namespace Syncify.Application.Tests.Fakes;

internal sealed class FakeYouTubeClient(YouTubePlaylistResult result) : IYouTubeClient
{
    public Task<YouTubePlaylistResult> GetPlaylistAsync(YouTubePlaylistId playlistId, CancellationToken cancellationToken) =>
        Task.FromResult(result);

    public static YouTubePlaylistId PlaylistId { get; } =
        YouTubePlaylistId.TryParse("PLOp8kZQdZGQs", out var id) ? id : throw new InvalidOperationException();

    public static FakeYouTubeClient WithVideos(params YouTubeVideo[] videos) =>
        new(YouTubePlaylistResult.Success(new YouTubePlaylist(PlaylistId, "Test playlist", "Me", videos)));

    public static YouTubeVideo Video(int position, string title, string? channel = "Label", int seconds = 180) =>
        new($"video{position:D7}", title, channel, TimeSpan.FromSeconds(seconds), position, VideoAvailability.Available);

    public static YouTubeVideo DeletedVideo(int position) =>
        new($"video{position:D7}", "Deleted video", null, null, position, VideoAvailability.Unavailable);
}
