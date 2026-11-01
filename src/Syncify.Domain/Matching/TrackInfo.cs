namespace Syncify.Domain.Matching;

/// <summary>A track as any platform describes it. The version is read from the title ("Tu (Live)").</summary>
public sealed record TrackInfo(string Title, IReadOnlyList<string> Artists, TimeSpan? Duration = null);
