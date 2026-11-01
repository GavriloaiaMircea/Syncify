namespace Syncify.Application.YouTube;

/// <summary>
/// One playlist entry. Unavailable entries keep YouTube's placeholder title ("Deleted video", "Private video")
/// and have no channel or duration.
/// </summary>
public sealed record YouTubeVideo(
    string VideoId,
    string Title,
    string? ChannelTitle,
    TimeSpan? Duration,
    int Position,
    VideoAvailability Availability);

public enum VideoAvailability
{
    Available,
    Private,

    /// <summary>Deleted, removed for a violation, or otherwise no longer served.</summary>
    Unavailable,
}
