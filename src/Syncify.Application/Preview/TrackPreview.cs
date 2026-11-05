using Syncify.Application.Spotify;
using Syncify.Application.YouTube;
using Syncify.Domain.Matching;

namespace Syncify.Application.Preview;

/// <summary>One playlist video and the Spotify track it would become. Unavailable videos are not parsed or searched.</summary>
public sealed record TrackPreview(YouTubeVideo Video, ParsedTitle? Parsed, TrackSearchResult? Search)
{
    public PreviewStatus Status => Search?.Match?.Status switch
    {
        null => PreviewStatus.Skipped,
        MatchStatus.Matched => PreviewStatus.Matched,
        MatchStatus.Ambiguous => PreviewStatus.Ambiguous,
        _ => PreviewStatus.NotFound,
    };
}

public enum PreviewStatus
{
    Matched,
    Ambiguous,
    NotFound,

    /// <summary>The video is deleted or private, so there is nothing to search for.</summary>
    Skipped,
}
