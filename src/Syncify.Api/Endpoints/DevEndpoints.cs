using System.Globalization;
using Syncify.Application.Preview;
using Syncify.Application.YouTube;
using Syncify.Domain.Playlists;

namespace Syncify.Api.Endpoints;

/// <summary>Development-only endpoints for checking external integrations against real data.</summary>
internal static class DevEndpoints
{
    public static IEndpointRouteBuilder MapDevEndpoints(this IEndpointRouteBuilder app)
    {
        var dev = app.MapGroup("/dev").WithTags("Dev");

        dev.MapGet("/youtube/playlist", GetYouTubePlaylist)
            .WithSummary("Reads a YouTube playlist from a playlist ID or link, e.g. ?link=PLOp8kZQdZGQs.");

        dev.MapGet("/preview", GetPreview)
            .WithSummary("Matches every video of a YouTube playlist against Spotify and reports the result, e.g. ?link=PLOp8kZQdZGQs.");

        return app;
    }

    private static async Task<IResult> GetYouTubePlaylist(string link, IYouTubeClient youTube, CancellationToken cancellationToken)
    {
        if (!YouTubePlaylistId.TryParse(link, out var playlistId))
        {
            return InvalidLink();
        }

        var result = await youTube.GetPlaylistAsync(playlistId, cancellationToken);
        if (!result.IsSuccess)
        {
            return YouTubeProblem(result.Error.Value);
        }

        var playlist = result.Playlist;
        return Results.Ok(new
        {
            Id = playlist.Id.Value,
            playlist.Title,
            playlist.ChannelTitle,
            VideoCount = playlist.Videos.Count,
            UnavailableCount = playlist.Videos.Count(video => video.Availability != VideoAvailability.Available),
            playlist.Videos,
        });
    }

    private static async Task<IResult> GetPreview(
        string link,
        PlaylistPreviewService previewService,
        HttpResponse response,
        CancellationToken cancellationToken)
    {
        if (!YouTubePlaylistId.TryParse(link, out var playlistId))
        {
            return InvalidLink();
        }

        var result = await previewService.PreviewAsync(playlistId, cancellationToken);
        if (result.SpotifyRetryAfter is { } retryAfter)
        {
            response.Headers.RetryAfter = Math.Ceiling(retryAfter.TotalSeconds).ToString(CultureInfo.InvariantCulture);
            return Results.Problem("Spotify is rate limiting searches; try again later.", statusCode: StatusCodes.Status503ServiceUnavailable);
        }

        if (!result.IsSuccess)
        {
            return YouTubeProblem(result.YouTubeError!.Value);
        }

        var preview = result.Preview;
        return Results.Ok(new
        {
            Id = preview.Playlist.Id.Value,
            preview.Playlist.Title,
            preview.Playlist.ChannelTitle,
            Summary = new
            {
                Total = preview.Tracks.Count,
                Matched = preview.Count(PreviewStatus.Matched),
                Ambiguous = preview.Count(PreviewStatus.Ambiguous),
                NotFound = preview.Count(PreviewStatus.NotFound),
                Skipped = preview.Count(PreviewStatus.Skipped),
            },
            Tracks = preview.Tracks.Select(track => new
            {
                track.Video.Position,
                track.Video.VideoId,
                VideoTitle = track.Video.Title,
                track.Video.Duration,
                track.Status,
                track.Parsed,
                Confidence = track.Search?.Match is { } match ? Math.Round(match.Confidence, 3) : (double?)null,
                track.Search?.Match?.Reasons,
                Spotify = track.Search?.Track,
                track.Search?.Queries,
            }),
        });
    }

    private static IResult InvalidLink() =>
        Results.Problem("Not a YouTube playlist ID or link.", statusCode: StatusCodes.Status400BadRequest);

    private static IResult YouTubeProblem(YouTubeError error) => error switch
    {
        YouTubeError.NotFound => Results.Problem("The playlist does not exist or is private.", statusCode: StatusCodes.Status404NotFound),
        YouTubeError.NotAccessible => Results.Problem("The playlist's videos cannot be read.", statusCode: StatusCodes.Status403Forbidden),
        YouTubeError.QuotaExceeded => Results.Problem("The YouTube API quota is used up for today.", statusCode: StatusCodes.Status503ServiceUnavailable),
        YouTubeError.Unsupported => Results.Problem("Mixes and YouTube Music radio lists cannot be imported.", statusCode: StatusCodes.Status422UnprocessableEntity),
        _ => throw new InvalidOperationException($"Unhandled YouTube error {error}."),
    };
}
