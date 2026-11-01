namespace Syncify.Domain.Matching;

/// <summary>What a video title says about the song. <see cref="Artists"/> is empty when the title does not name them.</summary>
public sealed record ParsedTitle(IReadOnlyList<string> Artists, string Song)
{
    public TrackInfo ToTrack(TimeSpan? duration = null) => new(Song, Artists, duration);
}
