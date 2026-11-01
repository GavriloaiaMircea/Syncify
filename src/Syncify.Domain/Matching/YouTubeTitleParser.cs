using System.Text.RegularExpressions;

namespace Syncify.Domain.Matching;

/// <summary>
/// Extracts artists and song name from a YouTube video title such as
/// "INNA - Cola Song (feat. J Balvin) | Official Music Video".
/// </summary>
public static partial class YouTubeTitleParser
{
    private const string TopicSuffix = " - Topic";

    public static ParsedTitle Parse(string title, string? channelTitle = null)
    {
        var text = ArtistSplitter.NormalizeJoiners(title.Trim());

        // Auto-generated "Artist - Topic" channels upload bare song names, so the artist comes from the channel.
        if (channelTitle is not null && channelTitle.EndsWith(TopicSuffix, StringComparison.Ordinal))
        {
            return Build(ArtistSplitter.Split(channelTitle[..^TopicSuffix.Length]), text);
        }

        var (artistPart, songPart) = SplitArtistAndSong(text);
        return Build(artistPart is null ? [] : ArtistSplitter.Split(artistPart), songPart);
    }

    private static (string? Artists, string Song) SplitArtistAndSong(string text)
    {
        // Pipes before the separator join artists ("Grasu XXL | Maximilian | Spike - Didaia"); after it they start promo tags.
        var segments = Pipe().Split(text);

        for (var i = 0; i < segments.Length; i++)
        {
            if (FindSeparator(segments[i]) is not { } separator)
            {
                continue;
            }

            var artists = string.Join(" | ", segments[..i].Append(segments[i][..separator.Index]));
            return (artists, segments[i][(separator.Index + separator.Length)..]);
        }

        var first = segments[0];

        var quoted = QuotedSong().Match(first);
        if (quoted.Success)
        {
            return (quoted.Groups["artists"].Value, quoted.Groups["song"].Value + quoted.Groups["rest"].Value);
        }

        var emojiSeparated = EmojiSeparated().Match(first);
        if (emojiSeparated.Success)
        {
            return (emojiSeparated.Groups["artists"].Value, emojiSeparated.Groups["song"].Value);
        }

        return (null, first);
    }

    private static Match? FindSeparator(string text) =>
        ArtistSongSeparator().Matches(text).FirstOrDefault(match => IsOutsideBrackets(text, match.Index));

    private static bool IsOutsideBrackets(string text, int index)
    {
        var depth = 0;

        foreach (var c in text.AsSpan(0, index))
        {
            depth += c switch
            {
                '(' or '[' or '{' => 1,
                ')' or ']' or '}' => -1,
                _ => 0,
            };
        }

        return depth <= 0;
    }

    private static ParsedTitle Build(IReadOnlyList<string> artists, string songPart)
    {
        var featured = new List<string>();

        var song = BracketedFeature().Replace(songPart, match =>
        {
            featured.AddRange(ArtistSplitter.Split(match.Groups["artists"].Value));
            return " ";
        });

        song = TitleNoise.Clean(song);

        var trailing = TrailingFeature().Match(song);
        if (trailing.Success)
        {
            featured.AddRange(ArtistSplitter.Split(trailing.Groups["artists"].Value));
            song = $"{song[..trailing.Index]} {trailing.Groups["rest"].Value}";
        }

        var allArtists = artists.Concat(featured).DistinctBy(TextNormalizer.Normalize).ToList();
        return new ParsedTitle(allArtists, Unquote(TextNormalizer.CollapseWhitespace(song)));
    }

    private static string Unquote(string song) =>
        song.Length >= 2 && IsQuote(song[0]) && IsQuote(song[^1]) ? song[1..^1].Trim() : song;

    private static bool IsQuote(char c) => c is '"' or '“' or '”' or '„' or '«' or '»';

    [GeneratedRegex(@"\s*\|+\s*")]
    private static partial Regex Pipe();

    // "Ruby- Stinge lumina" counts as a separator, "Da-te-n dragostea mea" does not.
    [GeneratedRegex(@"\s+[-–—•]\s+|(?<=\w)[-–—]\s+")]
    private static partial Regex ArtistSongSeparator();

    [GeneratedRegex(@"^(?<artists>[^""“”]+?)\s+[""“](?<song>[^""“”]+)[""”](?<rest>.*)$")]
    private static partial Regex QuotedSong();

    [GeneratedRegex(@"^(?<artists>.+?)\s+(?:\p{So}|\p{Sk}|\p{Cs}|️|‍)+\s+(?<song>.+)$")]
    private static partial Regex EmojiSeparated();

    [GeneratedRegex(@"[(\[]\s*(?:" + ArtistSplitter.FeatureMarker + @"|with|special\s+guest:?)\s+(?<artists>[^()\[\]]+)[)\]]", RegexOptions.IgnoreCase)]
    private static partial Regex BracketedFeature();

    // Brackets and dash suffixes after the credit belong to the song, not the artist:
    // "Tu feat. Alex Velea (Live)", "Tu feat. Alex Velea - Remix".
    [GeneratedRegex(@"\s" + ArtistSplitter.FeatureMarker + @"\s+(?<artists>[^()\[\]]+?)\s*(?<rest>(?:[(\[]|\s[-–—]\s).*)?$", RegexOptions.IgnoreCase)]
    private static partial Regex TrailingFeature();
}
