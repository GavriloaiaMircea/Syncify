using System.Text.RegularExpressions;

namespace Syncify.Domain.Matching;

/// <summary>
/// Tells remixes, live recordings and other alternate versions apart from the original recording.
/// </summary>
public static class VersionDetector
{
    // Checked in order, so "Live Acoustic" is acoustic and "Slowed Remix" is slowed.
    private static readonly (TrackVersion Version, string[] Keywords)[] Rules =
    [
        (TrackVersion.Karaoke, ["karaoke"]),
        (TrackVersion.Instrumental, ["instrumental"]),
        (TrackVersion.SpedUp, ["sped up", "speed up", "spedup", "nightcore"]),
        (TrackVersion.Slowed, ["slowed", "reverb"]),
        (TrackVersion.Acoustic, ["acoustic", "acustic", "acustica", "unplugged"]),
        (TrackVersion.Live, ["live", "concert"]),
        (TrackVersion.Remix, ["remix", "rmx", "bootleg"]),
        (TrackVersion.Cover, ["cover"]),
    ];

    /// <summary>
    /// Detects the version of a song title such as "Tu (Live)" or "Tu - Remix". Only brackets and
    /// dash suffixes are inspected, so a song called "Live Your Life" stays original.
    /// </summary>
    public static TrackVersion Detect(string songTitle)
    {
        foreach (var fragment in Fragments(songTitle))
        {
            var version = Classify(fragment);
            if (version != TrackVersion.Original)
            {
                return version;
            }
        }

        return TrackVersion.Original;
    }

    /// <summary>Classifies a single decoration, e.g. the "Official Video Live" inside a bracket.</summary>
    public static TrackVersion Classify(string fragment)
    {
        var padded = $" {TextNormalizer.Normalize(fragment)} ";

        foreach (var (version, keywords) in Rules)
        {
            if (keywords.Any(keyword => padded.Contains($" {keyword} ", StringComparison.Ordinal)))
            {
                return version;
            }
        }

        return TrackVersion.Original;
    }

    public static string Label(TrackVersion version) => version switch
    {
        TrackVersion.SpedUp => "Sped Up",
        _ => version.ToString(),
    };

    private static IEnumerable<string> Fragments(string songTitle)
    {
        foreach (Match match in TitleNoise.Bracket().Matches(songTitle))
        {
            yield return match.Groups["content"].Value;
        }

        foreach (var suffix in TitleNoise.DashSeparator().Split(songTitle).Skip(1))
        {
            yield return suffix;
        }
    }
}
