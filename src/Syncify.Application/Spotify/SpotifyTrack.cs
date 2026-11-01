namespace Syncify.Application.Spotify;

public sealed record SpotifyTrack(string Id, string Name, IReadOnlyList<string> Artists, TimeSpan Duration, string AlbumName)
{
    public string Uri => $"spotify:track:{Id}";
}
