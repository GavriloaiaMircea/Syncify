using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

namespace Syncify.Domain.Playlists;

/// <summary>
/// A YouTube playlist ID, read from a raw ID or from any youtube.com / youtu.be link with a <c>list</c> parameter.
/// </summary>
public sealed partial record YouTubePlaylistId
{
    private static readonly string[] YouTubeHosts = ["youtube.com", "www.youtube.com", "m.youtube.com", "music.youtube.com", "youtu.be"];

    private YouTubePlaylistId(string value) => Value = value;

    public string Value { get; }

    public static bool TryParse([NotNullWhen(true)] string? input, [NotNullWhen(true)] out YouTubePlaylistId? playlistId)
    {
        playlistId = null;
        if (string.IsNullOrWhiteSpace(input))
        {
            return false;
        }

        var text = input.Trim();
        var candidate = text.IndexOfAny(['/', '?', '&']) >= 0 ? FromLink(text) : text;
        if (candidate is null || !IdPattern().IsMatch(candidate))
        {
            return false;
        }

        playlistId = new YouTubePlaylistId(candidate);
        return true;
    }

    public override string ToString() => Value;

    private static string? FromLink(string link)
    {
        // People paste links without a scheme ("youtube.com/playlist?list=...").
        var withScheme = link.Contains("://", StringComparison.Ordinal) ? link : "https://" + link;
        if (!Uri.TryCreate(withScheme, UriKind.Absolute, out var uri)
            || !YouTubeHosts.Contains(uri.Host, StringComparer.OrdinalIgnoreCase))
        {
            return null;
        }

        return uri.Query.TrimStart('?')
            .Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(pair => pair.Split('=', 2))
            .Where(pair => pair is ["list", _])
            .Select(pair => Uri.UnescapeDataString(pair[1]))
            .FirstOrDefault();
    }

    // Most IDs are "PL" + 32 characters, but older playlists have short ones such as "PLOp8kZQdZGQs".
    [GeneratedRegex("^[A-Za-z0-9_-]{10,64}$")]
    private static partial Regex IdPattern();
}
