using Syncify.Application.YouTube;
using Syncify.Domain.Playlists;

namespace Syncify.Api.Endpoints;

/// <summary>Development-only endpoints for checking external integrations against real data.</summary>
internal static class DevEndpoints
{
    public static IEndpointRouteBuilder MapDevEndpoints(this IEndpointRouteBuilder app)
    {
        var dev = app.MapGroup("/dev").WithTags("Dev");

        dev.MapGet("/youtube/playlists/{link}", GetYouTubePlaylist)
            .WithSummary("Reads a YouTube playlist from a playlist ID or a URL-encoded link.");

        return app;
    }

    private static async Task<IResult> GetYouTubePlaylist(string link, IYouTubeClient youTube, CancellationToken cancellationToken)
    {
        // Route values keep an encoded "/" as %2F, so a pasted link arrives only partly decoded.
        if (!YouTubePlaylistId.TryParse(Uri.UnescapeDataString(link), out var playlistId))
        {
            return Results.Problem("Not a YouTube playlist ID or link.", statusCode: StatusCodes.Status400BadRequest);
        }

        var result = await youTube.GetPlaylistAsync(playlistId, cancellationToken);
        if (!result.IsSuccess)
        {
            return result.Error switch
            {
                YouTubeError.NotFound => Results.Problem("The playlist does not exist or is private.", statusCode: StatusCodes.Status404NotFound),
                YouTubeError.NotAccessible => Results.Problem("The playlist's videos cannot be read.", statusCode: StatusCodes.Status403Forbidden),
                YouTubeError.QuotaExceeded => Results.Problem("The YouTube API quota is used up for today.", statusCode: StatusCodes.Status503ServiceUnavailable),
                _ => throw new InvalidOperationException($"Unhandled YouTube error {result.Error}."),
            };
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
}
