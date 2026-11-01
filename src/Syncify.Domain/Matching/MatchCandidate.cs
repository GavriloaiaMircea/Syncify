namespace Syncify.Domain.Matching;

/// <summary>A track on the target platform, identified by that platform's ID.</summary>
public sealed record MatchCandidate(string Id, TrackInfo Track);
