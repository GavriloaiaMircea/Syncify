namespace Syncify.Infrastructure.Spotify;

internal static class SpotifyRetryAfter
{
    // Spotify documents Retry-After on every 429, so this only covers a proxy that drops the header.
    public static readonly TimeSpan Default = TimeSpan.FromSeconds(5);

    public static TimeSpan Read(HttpResponseMessage response, DateTimeOffset now) => response.Headers.RetryAfter switch
    {
        { Delta: { } delta } => delta,
        { Date: { } date } => TimeSpan.FromTicks(Math.Max(0, (date - now).Ticks)),
        _ => Default,
    };
}
