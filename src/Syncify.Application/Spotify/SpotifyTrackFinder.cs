using Syncify.Domain.Matching;

namespace Syncify.Application.Spotify;

/// <summary>Finds the Spotify recording of a track, searching more loosely only while no candidate is a confident match.</summary>
public sealed class SpotifyTrackFinder(ISpotifyClient spotify)
{
    public async Task<TrackSearchResult> FindAsync(TrackInfo track, CancellationToken cancellationToken)
    {
        var tracksById = new Dictionary<string, SpotifyTrack>(StringComparer.Ordinal);
        var candidates = new List<MatchCandidate>();
        var queries = new List<string>();
        var match = TrackMatcher.Match(track, candidates);

        foreach (var query in SpotifySearchQuery.For(track))
        {
            var result = await spotify.SearchTracksAsync(query, cancellationToken);
            queries.Add(query);

            if (!result.IsSuccess)
            {
                return TrackSearchResult.RateLimited(result.RetryAfter.Value, queries);
            }

            // Later queries are matched together with earlier results: a loose search can surface a rival
            // that makes an earlier near-match ambiguous, or the exact recording the strict search missed.
            foreach (var found in result.Tracks.Where(found => tracksById.TryAdd(found.Id, found)))
            {
                candidates.Add(new MatchCandidate(found.Id, new TrackInfo(found.Name, found.Artists, found.Duration)));
            }

            match = TrackMatcher.Match(track, candidates);
            if (match.Status == MatchStatus.Matched)
            {
                break;
            }
        }

        return TrackSearchResult.Found(match, match.Candidate is { } best ? tracksById[best.Id] : null, queries);
    }
}
