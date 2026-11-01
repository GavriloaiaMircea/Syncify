using System.Text.RegularExpressions;

namespace Syncify.Domain.Matching;

/// <summary>
/// Splits an artist credit such as "Puya cu Doddy, Posset, Mahia si Alex Velea" into individual artists.
/// </summary>
public static partial class ArtistSplitter
{
    /// <summary>Words that introduce a featured artist. Safe to cut a song title at, unlike "x" or "si".</summary>
    internal const string FeatureMarker = @"(?:feat\.?|ft\.?|featuring)";

    /// <summary>Every joiner word between artists, as normalized tokens.</summary>
    internal static readonly HashSet<string> JoinerWords = ["feat", "ft", "featuring", "with", "x", "si", "cu", "vs"];

    // Brackets in a credit only wrap joiners ("INNA (feat. J Balvin)"), so they become spaces to let the joiner match.
    public static IReadOnlyList<string> Split(string credit) =>
        Joiner().Split(CreditBracket().Replace(NormalizeJoiners(credit), " "))
            .Select(CleanName)
            .Where(name => name.Length > 0)
            .DistinctBy(TextNormalizer.Normalize)
            .ToList();

    // Manele uploads join artists with symbols ("Tzanca Uraganu ❌ Florin Salam") instead of words.
    internal static string NormalizeJoiners(string text) => JoinerSymbol().Replace(text, " x ");

    // Strips handle and emoji leftovers: "@MariusMoga_" becomes "MariusMoga", "Mariano 〽️🌍" becomes "Mariano".
    private static string CleanName(string name)
    {
        var text = TextNormalizer.CollapseWhitespace(TitleNoise.RemoveSymbols(name));
        var start = 0;
        var end = text.Length;

        while (start < end && !(char.IsLetterOrDigit(text[start]) || text[start] == '$'))
        {
            start++;
        }

        while (end > start && !(char.IsLetterOrDigit(text[end - 1]) || text[end - 1] is '.' or '!' or '?' or '$'))
        {
            end--;
        }

        return text[start..end];
    }

    [GeneratedRegex(@"\s*[,&+;|]\s*|\s+(?:" + FeatureMarker + @"|with|x|si|și|cu|vs\.?)\s+", RegexOptions.IgnoreCase)]
    private static partial Regex Joiner();

    [GeneratedRegex(@"[()\[\]{}]")]
    private static partial Regex CreditBracket();

    [GeneratedRegex(@"\s*(?:❌|✘|✖|×|💔)️?\s*")]
    private static partial Regex JoinerSymbol();
}
