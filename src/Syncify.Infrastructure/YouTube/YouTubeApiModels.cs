namespace Syncify.Infrastructure.YouTube;

// Only the fields the client requests through the "fields" query parameter.

internal sealed record PlaylistListResponse(IReadOnlyList<PlaylistResource> Items);

internal sealed record PlaylistResource(PlaylistSnippet Snippet);

internal sealed record PlaylistSnippet(string Title, string ChannelTitle);

internal sealed record PlaylistItemListResponse(string? NextPageToken, IReadOnlyList<PlaylistItemResource> Items);

internal sealed record PlaylistItemResource(PlaylistItemSnippet Snippet, PlaylistItemStatus? Status);

internal sealed record PlaylistItemSnippet(string Title, int Position, string? VideoOwnerChannelTitle, ResourceId ResourceId);

internal sealed record ResourceId(string VideoId);

internal sealed record PlaylistItemStatus(string? PrivacyStatus);

internal sealed record VideoListResponse(IReadOnlyList<VideoResource> Items);

internal sealed record VideoResource(string Id, VideoContentDetails? ContentDetails);

internal sealed record VideoContentDetails(string? Duration);

internal sealed record ErrorResponse(ErrorBody? Error);

internal sealed record ErrorBody(int Code, IReadOnlyList<ErrorDetail>? Errors);

internal sealed record ErrorDetail(string? Reason);
