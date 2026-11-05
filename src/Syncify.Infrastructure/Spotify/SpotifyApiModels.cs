using System.Text.Json.Serialization;

namespace Syncify.Infrastructure.Spotify;

// Only the fields the client reads.

internal sealed record TokenResponse(
    [property: JsonPropertyName("access_token")] string AccessToken,
    [property: JsonPropertyName("expires_in")] int ExpiresIn);

internal sealed record TokenErrorResponse([property: JsonPropertyName("error")] string? Error);

internal sealed record SearchResponse(TrackPage Tracks);

internal sealed record TrackPage(IReadOnlyList<TrackObject> Items);

internal sealed record TrackObject(
    string Id,
    string Name,
    [property: JsonPropertyName("duration_ms")] int DurationMs,
    [property: JsonPropertyName("is_playable")] bool? IsPlayable,
    IReadOnlyList<ArtistObject> Artists,
    AlbumObject Album);

internal sealed record ArtistObject(string Name);

internal sealed record AlbumObject(string Name);

internal sealed record ApiErrorResponse(ApiError? Error);

internal sealed record ApiError(string? Message);
