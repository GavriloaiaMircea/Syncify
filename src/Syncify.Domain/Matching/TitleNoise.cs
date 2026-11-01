using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Syncify.Domain.Matching;

/// <summary>
/// Removes the promotional decoration uploaders put around a song name: "(Official Video)",
/// "[Videoclip Oficial HD]", producer credits, hashtags and emojis.
/// </summary>
public static partial class TitleNoise
{
    private static readonly HashSet<string> NoiseWords =
    [
        "official", "oficial", "oficiala", "original", "video", "videoclip", "clip", "audio", "lyric", "lyrics",
        "visualizer", "hd", "hq", "4k", "prod", "by", "music", "records", "production", "single", "track",
        "version", "hit", "manele",
    ];

    private static readonly Regex[] TrailingNoise =
        [TrailingTag(), TrailingSessionVersion(), TrailingHashtags(), TrailingHit(), TrailingQuality()];

    /// <summary>Cleans the song part of a video title, keeping meaningful brackets like "(TraLaLa)" or "(Live)".</summary>
    public static string Clean(string songPart)
    {
        var result = CreditAfterBracket().Replace(songPart, string.Empty);

        // Each pass only sees innermost brackets, so "[Videoclip Oficial (HD)]" needs a second pass for the outer one.
        string previous;
        do
        {
            previous = result;
            result = Bracket().Replace(result, CleanBracket);
        }
        while (result != previous);

        result = RemoveSymbols(result);

        do
        {
            previous = result;
            foreach (var pattern in TrailingNoise)
            {
                result = pattern.Replace(result, string.Empty);
            }

            result = TrimSeparators(result);
        }
        while (result != previous);

        return result;
    }

    /// <summary>
    /// The normalized title without brackets, dash suffixes or featured credits:
    /// "Cola Song (feat. J Balvin) - Remix" becomes "cola song".
    /// </summary>
    public static string CoreTitle(string title)
    {
        var withoutBrackets = Bracket().Replace(title, " ");
        var cut = CoreCut().Match(withoutBrackets);
        var core = TextNormalizer.Normalize(cut.Success ? withoutBrackets[..cut.Index] : withoutBrackets);
        return core.Length > 0 ? core : TextNormalizer.Normalize(title);
    }

    /// <summary>Drops emojis and other pictographs while keeping letters, digits and punctuation.</summary>
    public static string RemoveSymbols(string text)
    {
        var builder = new StringBuilder(text.Length);

        foreach (var rune in text.EnumerateRunes())
        {
            var category = Rune.GetUnicodeCategory(rune);
            var isSymbol = category is UnicodeCategory.OtherSymbol or UnicodeCategory.ModifierSymbol or UnicodeCategory.Format
                || rune.Value is 0xFE0E or 0xFE0F;

            if (!isSymbol)
            {
                builder.Append(rune.ToString());
            }
        }

        return builder.ToString();
    }

    [GeneratedRegex(@"[(\[{](?<content>[^()\[\]{}]*)[)\]}]")]
    internal static partial Regex Bracket();

    [GeneratedRegex(@"\s+[-–—]\s+")]
    internal static partial Regex DashSeparator();

    private static string CleanBracket(Match match)
    {
        var content = match.Groups["content"].Value;
        var tokens = TextNormalizer.Tokenize(content);

        if (tokens.Count == 0)
        {
            return " ";
        }

        if (!tokens.Any(IsNoise))
        {
            return match.Value;
        }

        // "(Official Video Live)" is still a live recording, so the version survives as "(Live)".
        var version = VersionDetector.Classify(content);
        return version == TrackVersion.Original ? " " : $" ({VersionDetector.Label(version)}) ";
    }

    private static bool IsNoise(string token) => NoiseWords.Contains(token) || IsYear(token);

    private static bool IsYear(string token) =>
        token.Length == 4 && (token.StartsWith("19", StringComparison.Ordinal) || token.StartsWith("20", StringComparison.Ordinal))
        && token.All(char.IsAsciiDigit);

    private static string TrimSeparators(string text) =>
        TextNormalizer.CollapseWhitespace(text).Trim(' ', '-', '–', '—', '|', '*', '•', ':', ',');

    // "(Official Video) by Famous Production": a label credit trailing a bracket.
    [GeneratedRegex(@"(?<=[)\]])\s*by\s+[^()\[\]]+$", RegexOptions.IgnoreCase)]
    private static partial Regex CreditAfterBracket();

    [GeneratedRegex(
        @"(?<=\S)\s*[*•]?\s*\b(?:(?:official|oficial|original)(?:\s+music)?\s+(?:video(?:clip)?|audio|single|track)|video(?:clip)?\s+(?:oficial|official))(?:\s+(?:hd|hq|4k|hit|fara\s+reclame|(?:19|20)\d{2}))*\s*$",
        RegexOptions.IgnoreCase)]
    private static partial Regex TrailingTag();

    [GeneratedRegex(@"(?<=\S)\s+\S*session\S*\s+version\s*$", RegexOptions.IgnoreCase)]
    private static partial Regex TrailingSessionVersion();

    [GeneratedRegex(@"(?<=\S)(?:\s+#\w+)+\s*$")]
    private static partial Regex TrailingHashtags();

    // Case-sensitive on purpose: manele uploads shout "HIT", while a song may legitimately end in "Hit".
    [GeneratedRegex(@"(?<=\S)\s+(?:-\s+)?HIT\s*$")]
    private static partial Regex TrailingHit();

    // "[Official Video] HD": a quality tag left behind once the bracket is gone.
    [GeneratedRegex(@"(?<=\S)\s+(?:HD|HQ|4K)\s*$")]
    private static partial Regex TrailingQuality();

    [GeneratedRegex(@"\s+[-–—]\s+|\s" + ArtistSplitter.FeatureMarker + @"\s", RegexOptions.IgnoreCase)]
    private static partial Regex CoreCut();
}
