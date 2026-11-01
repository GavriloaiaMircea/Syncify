using System.Net;
using System.Net.Http.Json;
using Syncify.Application.YouTube;
using Syncify.Domain.Playlists;

namespace Syncify.Infrastructure.YouTube;

/// <summary>
/// Reads playlists through the YouTube Data API v3. The API key is sent by <see cref="DependencyInjection"/>
/// as a header so it never appears in URLs or request logs.
/// </summary>
internal sealed class YouTubeClient(HttpClient http) : IYouTubeClient
{
    // The maximum for both playlistItems.list and videos.list, so each item page needs exactly one videos call.
    private const string PageSize = "50";

    private const string PlaylistFields = "items(snippet(title,channelTitle))";
    private const string PlaylistItemFields =
        "nextPageToken,items(snippet(title,position,videoOwnerChannelTitle,resourceId(videoId)),status(privacyStatus))";
    private const string VideoFields = "items(id,contentDetails(duration))";

    public async Task<YouTubePlaylistResult> GetPlaylistAsync(YouTubePlaylistId playlistId, CancellationToken cancellationToken)
    {
        var playlists = await GetAsync<PlaylistListResponse>(
            "playlists",
            [("part", "snippet"), ("id", playlistId.Value), ("fields", PlaylistFields)],
            cancellationToken);

        if (playlists.Error is { } playlistError)
        {
            return YouTubePlaylistResult.Failure(playlistError);
        }

        // Private and nonexistent playlists both come back as an empty list.
        if (playlists.Body is not { Items: [var playlist] })
        {
            return YouTubePlaylistResult.Failure(YouTubeError.NotFound);
        }

        var videos = new List<YouTubeVideo>();
        string? pageToken = null;
        do
        {
            var page = await GetAsync<PlaylistItemListResponse>(
                "playlistItems",
                [
                    ("part", "snippet,status"),
                    ("playlistId", playlistId.Value),
                    ("maxResults", PageSize),
                    ("fields", PlaylistItemFields),
                    ("pageToken", pageToken),
                ],
                cancellationToken);

            if (page.Error is { } pageError)
            {
                return YouTubePlaylistResult.Failure(pageError);
            }

            var items = page.Body!.Items;
            var durations = await GetDurationsAsync(items.Select(item => item.Snippet.ResourceId.VideoId), cancellationToken);
            if (durations.Error is { } videoError)
            {
                return YouTubePlaylistResult.Failure(videoError);
            }

            videos.AddRange(items.Select(item => ToVideo(item, durations.Body!)));
            pageToken = page.Body.NextPageToken;
        }
        while (pageToken is not null);

        return YouTubePlaylistResult.Success(
            new YouTubePlaylist(playlistId, playlist.Snippet.Title, playlist.Snippet.ChannelTitle, videos));
    }

    /// <summary>
    /// Durations by video ID. videos.list leaves out deleted and private videos, so a missing ID means unavailable.
    /// </summary>
    private async Task<ApiResponse<IReadOnlyDictionary<string, TimeSpan?>>> GetDurationsAsync(
        IEnumerable<string> videoIds,
        CancellationToken cancellationToken)
    {
        var ids = string.Join(',', videoIds);
        if (ids.Length == 0)
        {
            return new(new Dictionary<string, TimeSpan?>(), null);
        }

        var response = await GetAsync<VideoListResponse>(
            "videos",
            [("part", "contentDetails"), ("id", ids), ("fields", VideoFields)],
            cancellationToken);

        return response.Error is { } error
            ? new(null, error)
            : new(response.Body!.Items.ToDictionary(video => video.Id, video => YouTubeDuration.Parse(video.ContentDetails?.Duration)), null);
    }

    private static YouTubeVideo ToVideo(PlaylistItemResource item, IReadOnlyDictionary<string, TimeSpan?> durations)
    {
        var snippet = item.Snippet;
        var videoId = snippet.ResourceId.VideoId;

        if (durations.TryGetValue(videoId, out var duration))
        {
            return new YouTubeVideo(videoId, snippet.Title, snippet.VideoOwnerChannelTitle, duration, snippet.Position, VideoAvailability.Available);
        }

        var availability = item.Status?.PrivacyStatus == "private" ? VideoAvailability.Private : VideoAvailability.Unavailable;
        return new YouTubeVideo(videoId, snippet.Title, null, null, snippet.Position, availability);
    }

    private async Task<ApiResponse<T>> GetAsync<T>(
        string resource,
        IEnumerable<(string Name, string? Value)> query,
        CancellationToken cancellationToken)
        where T : class
    {
        var queryString = string.Join('&', query
            .Where(parameter => parameter.Value is not null)
            .Select(parameter => $"{parameter.Name}={Uri.EscapeDataString(parameter.Value!)}"));

        using var response = await http.GetAsync($"{resource}?{queryString}", cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadFromJsonAsync<T>(cancellationToken)
                ?? throw new InvalidOperationException($"YouTube {resource}.list returned an empty body.");
            return new(body, null);
        }

        var reason = await ReadErrorReasonAsync(response, cancellationToken);
        var error = ToError(response.StatusCode, reason)
            ?? throw new HttpRequestException(
                $"YouTube {resource}.list failed with {(int)response.StatusCode} ({reason ?? "no reason"}).",
                inner: null,
                response.StatusCode);
        return new(null, error);
    }

    private static YouTubeError? ToError(HttpStatusCode status, string? reason) => (status, reason) switch
    {
        (HttpStatusCode.NotFound, _) => YouTubeError.NotFound,
        // IDs that are not shaped like a real playlist ID are rejected as "invalid" rather than "not found".
        (HttpStatusCode.BadRequest, "invalid") => YouTubeError.NotFound,
        (HttpStatusCode.Forbidden, "playlistItemsNotAccessible") => YouTubeError.NotAccessible,
        (HttpStatusCode.Forbidden, "quotaExceeded") => YouTubeError.QuotaExceeded,
        _ => null,
    };

    private static async Task<string?> ReadErrorReasonAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            var body = await response.Content.ReadFromJsonAsync<ErrorResponse>(cancellationToken);
            return body?.Error?.Errors?.FirstOrDefault()?.Reason;
        }
        catch (System.Text.Json.JsonException)
        {
            return null;
        }
    }

    private readonly record struct ApiResponse<T>(T? Body, YouTubeError? Error);
}
