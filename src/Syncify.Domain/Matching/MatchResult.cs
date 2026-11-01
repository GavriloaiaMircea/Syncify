namespace Syncify.Domain.Matching;

/// <summary>
/// The outcome of matching one track. <see cref="Candidate"/> is the best-scoring candidate whatever the status,
/// and null only when there were no candidates. <see cref="Reasons"/> explain its score.
/// </summary>
public sealed record MatchResult(MatchStatus Status, MatchCandidate? Candidate, double Confidence, IReadOnlyList<MatchReason> Reasons);

public enum MatchStatus
{
    Matched,
    Ambiguous,
    NotFound,
}

public enum MatchReason
{
    NoCandidates,
    TitleMatch,
    TitleSimilar,
    TitleMismatch,
    ArtistMatch,
    ArtistMismatch,
    ArtistFoundInTitle,
    ArtistUnknown,
    DurationMatch,
    DurationMismatch,
    VersionMismatch,
    CloseRunnerUp,
}
