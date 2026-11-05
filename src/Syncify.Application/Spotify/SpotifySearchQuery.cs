using Syncify.Domain.Matching;

namespace Syncify.Application.Spotify;

/// <summary>Builds the Spotify search queries for a track, most precise first.</summary>
public static class SpotifySearchQuery
{
    /// <summary>
    /// With a known artist: a field-filtered query on the bare song name, then free text that keeps version words
    /// such as "remix" or "live". Without one, the whole title as free text, since it may still contain the artist.
    /// Terms are normalized, so quotes and colons in titles cannot be read as query syntax.
    /// </summary>
    public static IReadOnlyList<string> For(TrackInfo track)
    {
        var song = TextNormalizer.Normalize(track.Title);
        var coreSong = TitleNoise.CoreTitle(track.Title) is { Length: > 0 } core ? core : song;
        var artist = track.Artists.Select(TextNormalizer.Normalize).FirstOrDefault(name => name.Length > 0);

        if (song.Length == 0)
        {
            return [];
        }

        return artist is null
            ? [song]
            : [$"track:{coreSong} artist:{artist}", $"{artist} {song}"];
    }
}
