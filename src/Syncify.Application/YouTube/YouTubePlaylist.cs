using Syncify.Domain.Playlists;

namespace Syncify.Application.YouTube;

public sealed record YouTubePlaylist(YouTubePlaylistId Id, string Title, string ChannelTitle, IReadOnlyList<YouTubeVideo> Videos);
