namespace Syncify.Domain.Matching;

/// <summary>
/// Picks the candidate that is the same recording as the source track, whichever platform each side comes from.
/// </summary>
public static class TrackMatcher
{
    public const double MatchThreshold = 0.85;
    public const double AmbiguousThreshold = 0.6;
    public const double MinimumMargin = 0.05;

    private const double TitleWeight = 0.6;
    private const double ArtistWeight = 0.3;
    private const double DurationWeight = 0.1;

    private const double ArtistMatchThreshold = 0.8;

    // Without a known artist a perfect title only reaches 0.8, so the track is never matched on title alone.
    private const double UnknownArtistFactor = 0.8;

    // A doubtful artist ("Florin Salam" vs "Florin Cercel") is treated like an unknown one: at best ambiguous.
    private const double DoubtfulArtistCap = 0.8;
    private const double ArtistMismatchCap = 0.4;
    private const double VersionMismatchPenalty = 0.2;

    public static MatchResult Match(TrackInfo source, IReadOnlyList<MatchCandidate> candidates)
    {
        if (candidates.Count == 0)
        {
            return new MatchResult(MatchStatus.NotFound, null, 0, [MatchReason.NoCandidates]);
        }

        var sourceVersion = VersionDetector.Detect(source.Title);
        var ranked = candidates
            .Select(candidate => Score(source, sourceVersion, candidate))
            .OrderByDescending(score => score.Confidence)
            .ToList();
        var best = ranked[0];

        if (best.Confidence < AmbiguousThreshold)
        {
            return new MatchResult(MatchStatus.NotFound, best.Candidate, best.Confidence, best.Reasons);
        }

        // The same recording often appears twice (single and album), which must not count as a rival.
        var runnerUp = ranked.Skip(1).FirstOrDefault(score => !IsSameRecording(best.Candidate.Track, score.Candidate.Track));
        if (runnerUp is not null && best.Confidence - runnerUp.Confidence < MinimumMargin)
        {
            return new MatchResult(MatchStatus.Ambiguous, best.Candidate, best.Confidence, [.. best.Reasons, MatchReason.CloseRunnerUp]);
        }

        var status = best.Confidence >= MatchThreshold ? MatchStatus.Matched : MatchStatus.Ambiguous;
        return new MatchResult(status, best.Candidate, best.Confidence, best.Reasons);
    }

    private static ScoredCandidate Score(TrackInfo source, TrackVersion sourceVersion, MatchCandidate candidate)
    {
        var target = candidate.Track;
        var reasons = new List<MatchReason>();
        var titleScore = TitleScore(source.Title, target.Title);
        double? artistScore = null;

        if (source.Artists.Count > 0 && target.Artists.Count > 0)
        {
            artistScore = source.Artists.Max(sourceArtist => target.Artists.Max(targetArtist => TextSimilarity.Compare(sourceArtist, targetArtist)));
            reasons.Add(artistScore >= ArtistMatchThreshold ? MatchReason.ArtistMatch : MatchReason.ArtistMismatch);
        }
        else if (source.Artists.Count == 0
            && TryStripArtists(source.Title, target.Artists, out var strippedTitle)
            && TitleScore(strippedTitle, target.Title) is var strippedScore
            && strippedScore >= titleScore)
        {
            // A title without a separator may still credit the artist ("Theo Rose x Valentin Dinu Pacatul Meu").
            // It only counts when removing the name is what makes the titles agree; otherwise the name is part of
            // the song itself ("Mi-e dor de tine" is not a song by an artist called "Tine").
            titleScore = strippedScore;
            artistScore = 1;
            reasons.Add(MatchReason.ArtistFoundInTitle);
        }
        else
        {
            reasons.Add(MatchReason.ArtistUnknown);
        }

        reasons.Add(titleScore switch
        {
            >= 0.95 => MatchReason.TitleMatch,
            >= 0.75 => MatchReason.TitleSimilar,
            _ => MatchReason.TitleMismatch,
        });

        var durationScore = ScoreDuration(source.Duration, target.Duration, reasons);

        var weighted = TitleWeight * titleScore;
        var totalWeight = TitleWeight;

        if (artistScore is { } artist)
        {
            weighted += ArtistWeight * artist;
            totalWeight += ArtistWeight;
        }

        if (durationScore is { } duration)
        {
            weighted += DurationWeight * duration;
            totalWeight += DurationWeight;
        }

        var confidence = weighted / totalWeight;

        if (artistScore is null)
        {
            confidence *= UnknownArtistFactor;
        }
        else if (artistScore < 0.5)
        {
            confidence = Math.Min(confidence, ArtistMismatchCap);
        }
        else if (artistScore < ArtistMatchThreshold)
        {
            confidence = Math.Min(confidence, DoubtfulArtistCap);
        }

        if (titleScore < 0.5)
        {
            confidence = Math.Min(confidence, titleScore);
        }

        if (sourceVersion != VersionDetector.Detect(target.Title))
        {
            confidence -= VersionMismatchPenalty;
            reasons.Add(MatchReason.VersionMismatch);
        }

        return new ScoredCandidate(candidate, Math.Clamp(confidence, 0, 1), reasons);
    }

    private static double TitleScore(string sourceTitle, string targetTitle) =>
        Math.Max(
            TextSimilarity.Compare(sourceTitle, targetTitle),
            TextSimilarity.Compare(TitleNoise.CoreTitle(sourceTitle), TitleNoise.CoreTitle(targetTitle)));

    // Music videos often carry intros or outros, so a few seconds off is normal and only a large gap is suspicious.
    private static double? ScoreDuration(TimeSpan? source, TimeSpan? target, List<MatchReason> reasons)
    {
        if (source is null || target is null)
        {
            return null;
        }

        var difference = Math.Abs((source.Value - target.Value).TotalSeconds);

        if (difference <= 10)
        {
            reasons.Add(MatchReason.DurationMatch);
        }
        else if (difference > 30)
        {
            reasons.Add(MatchReason.DurationMismatch);
        }

        return difference switch
        {
            <= 3 => 1,
            <= 10 => 0.8,
            <= 30 => 0.5,
            _ => 0,
        };
    }

    private static bool TryStripArtists(string title, IReadOnlyList<string> artists, out string strippedTitle)
    {
        var tokens = TextNormalizer.Tokenize(title).ToList();
        var found = false;

        foreach (var artist in artists)
        {
            var artistTokens = TextNormalizer.Tokenize(artist);
            var index = IndexOf(tokens, artistTokens);

            if (index >= 0)
            {
                tokens.RemoveRange(index, artistTokens.Count);
                found = true;
            }
        }

        // Drop the joiners left behind by removed names, on either side: "theo rose x ... pacatul meu",
        // "pacatul meu feat theo rose". Joiners in the middle stay, since they may be part of the song ("Tu si Eu").
        while (tokens.Count > 0 && ArtistSplitter.JoinerWords.Contains(tokens[0]))
        {
            tokens.RemoveAt(0);
        }

        while (tokens.Count > 0 && ArtistSplitter.JoinerWords.Contains(tokens[^1]))
        {
            tokens.RemoveAt(tokens.Count - 1);
        }

        strippedTitle = string.Join(' ', tokens);
        return found && tokens.Count > 0;
    }

    private static int IndexOf(List<string> tokens, IReadOnlyList<string> phrase)
    {
        if (phrase.Count == 0)
        {
            return -1;
        }

        for (var start = 0; start + phrase.Count <= tokens.Count; start++)
        {
            if (phrase.Select((token, offset) => tokens[start + offset] == token).All(equal => equal))
            {
                return start;
            }
        }

        return -1;
    }

    private static bool IsSameRecording(TrackInfo first, TrackInfo second)
    {
        var sameDuration = first.Duration is null || second.Duration is null
            || Math.Abs((first.Duration.Value - second.Duration.Value).TotalSeconds) <= 3;
        var sharedArtist = first.Artists.Select(TextNormalizer.Normalize).Intersect(second.Artists.Select(TextNormalizer.Normalize)).Any();

        return sameDuration
            && sharedArtist
            && TitleNoise.CoreTitle(first.Title) == TitleNoise.CoreTitle(second.Title)
            && VersionDetector.Detect(first.Title) == VersionDetector.Detect(second.Title);
    }

    private sealed record ScoredCandidate(MatchCandidate Candidate, double Confidence, IReadOnlyList<MatchReason> Reasons);
}
