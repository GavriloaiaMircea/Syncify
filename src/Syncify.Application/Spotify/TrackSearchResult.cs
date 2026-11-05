using System.Diagnostics.CodeAnalysis;
using Syncify.Domain.Matching;

namespace Syncify.Application.Spotify;

/// <summary>
/// The best Spotify track for one source track. <see cref="Track"/> is the Spotify side of
/// <see cref="MatchResult.Candidate"/>; <see cref="Queries"/> are the searches that were run, in order.
/// </summary>
public sealed record TrackSearchResult
{
    private TrackSearchResult(MatchResult? match, SpotifyTrack? track, IReadOnlyList<string> queries, TimeSpan? retryAfter)
    {
        Match = match;
        Track = track;
        Queries = queries;
        RetryAfter = retryAfter;
    }

    public MatchResult? Match { get; }

    public SpotifyTrack? Track { get; }

    public IReadOnlyList<string> Queries { get; }

    public TimeSpan? RetryAfter { get; }

    [MemberNotNullWhen(true, nameof(Match))]
    [MemberNotNullWhen(false, nameof(RetryAfter))]
    public bool IsSuccess => Match is not null;

    public static TrackSearchResult Found(MatchResult match, SpotifyTrack? track, IReadOnlyList<string> queries) =>
        new(match, track, queries, null);

    public static TrackSearchResult RateLimited(TimeSpan retryAfter, IReadOnlyList<string> queries) =>
        new(null, null, queries, retryAfter);
}
