namespace Syncify.Domain.Matching;

/// <summary>Scores how alike two titles or names are, from 0 (nothing in common) to 1 (equal after normalization).</summary>
public static class TextSimilarity
{
    public static double Compare(string left, string right)
    {
        var a = TextNormalizer.Normalize(left);
        var b = TextNormalizer.Normalize(right);

        if (a == b)
        {
            return 1;
        }

        if (a.Length == 0 || b.Length == 0)
        {
            return 0;
        }

        // Sorting tokens forgives reordered words: "Moga Marius" vs "Marius Moga".
        return Math.Max(Ratio(a, b), Ratio(SortTokens(a), SortTokens(b)));
    }

    private static double Ratio(string a, string b) => 1 - (double)Levenshtein(a, b) / Math.Max(a.Length, b.Length);

    private static string SortTokens(string text) => string.Join(' ', text.Split(' ').Order(StringComparer.Ordinal));

    private static int Levenshtein(string a, string b)
    {
        var previous = new int[b.Length + 1];
        var current = new int[b.Length + 1];

        for (var j = 0; j <= b.Length; j++)
        {
            previous[j] = j;
        }

        for (var i = 1; i <= a.Length; i++)
        {
            current[0] = i;

            for (var j = 1; j <= b.Length; j++)
            {
                var substitution = previous[j - 1] + (a[i - 1] == b[j - 1] ? 0 : 1);
                current[j] = Math.Min(substitution, Math.Min(previous[j], current[j - 1]) + 1);
            }

            (previous, current) = (current, previous);
        }

        return previous[b.Length];
    }
}
