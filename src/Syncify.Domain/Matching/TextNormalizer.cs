using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Syncify.Domain.Matching;

/// <summary>
/// Reduces text to lowercase ASCII-like words so titles from different platforms compare equal:
/// "Bună, ce mai zici?" and "Buna ce mai zici" both become "buna ce mai zici".
/// </summary>
public static partial class TextNormalizer
{
    public static string Normalize(string text)
    {
        var builder = new StringBuilder(text.Length);
        var pendingSpace = false;

        // FormKD also folds stylized letters ("𝐂𝐨𝐥𝐚", "ＩＮＮＡ") to plain ones. It splits "´" into a space plus
        // an accent, so that apostrophe is swapped first to stay silent.
        foreach (var rune in text.Replace('´', '\'').Normalize(NormalizationForm.FormKD).EnumerateRunes())
        {
            if (Rune.IsLetterOrDigit(rune))
            {
                if (pendingSpace && builder.Length > 0)
                {
                    builder.Append(' ');
                }

                pendingSpace = false;
                builder.Append(Rune.ToLowerInvariant(rune).ToString());
            }
            else if (Rune.GetUnicodeCategory(rune) != UnicodeCategory.NonSpacingMark && !IsSilentPunctuation(rune))
            {
                pendingSpace = true;
            }
        }

        return builder.ToString();
    }

    public static IReadOnlyList<string> Tokenize(string text) =>
        Normalize(text).Split(' ', StringSplitOptions.RemoveEmptyEntries);

    internal static string CollapseWhitespace(string text) => Whitespace().Replace(text, " ").Trim();

    // Dots and apostrophes sit inside words ("B.U.G.", "Carla's"), so dropping them keeps the word whole.
    private static bool IsSilentPunctuation(Rune rune) => rune.IsBmp && (char)rune.Value is '.' or '\'' or '’' or '`';

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();
}
