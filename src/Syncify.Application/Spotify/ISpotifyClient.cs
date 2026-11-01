namespace Syncify.Application.Spotify;

public interface ISpotifyClient
{
    /// <summary>
    /// Searches the Spotify catalog for tracks, best match first. Accepts Spotify's query syntax, including field
    /// filters such as <c>track:</c> and <c>artist:</c>. Tracks that cannot be played in the configured market are left out.
    /// </summary>
    Task<SpotifySearchResult> SearchTracksAsync(string query, CancellationToken cancellationToken);
}
