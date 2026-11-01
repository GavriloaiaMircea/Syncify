using System.Text.RegularExpressions;

namespace Syncify.Domain.Matching;

/// <summary>
/// Splits an artist credit such as "Puya cu Doddy, Posset, Mahia si Alex Velea" into individual artists.
/// </summary>
public static partial class ArtistSplitter
{
    public static IReadOnlyList<string> Split(string credit) =>
        Joiner().Split(NormalizeJoiners(credit))
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

        while (end > start && !(char.IsLetterOrDigit(text[end - 1]) || text[end - 1] is '.' or '!' or '?' or ')' or '$'))
        {
            end--;
        }

        return text[start..end];
    }

    [GeneratedRegex(@"\s*[,&+;|]\s*|\s+(?:feat\.?|ft\.?|featuring|with|x|si|și|cu|vs\.?)\s+", RegexOptions.IgnoreCase)]
    private static partial Regex Joiner();

    [GeneratedRegex(@"\s*(?:❌|✘|✖|×|💔)️?\s*")]
    private static partial Regex JoinerSymbol();
}
