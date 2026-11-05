using Microsoft.Extensions.Options;

namespace Syncify.Infrastructure.Spotify;

/// <summary>
/// Paces every Spotify API request the app makes, whichever caller sends it, and holds them all back while Spotify
/// has asked to wait. A singleton: limits per caller still add up to a burst when several callers run at once.
/// </summary>
/// <remarks>
/// Spotify locked the app out for a day after repeated short 429s that parallel searches each waited out on their
/// own and then kept going. Hand-rolled rather than System.Threading.RateLimiting so pacing and the shared pause
/// follow one <see cref="TimeProvider"/> and can be tested with a fake clock.
/// </remarks>
internal sealed class SpotifyRequestGate(IOptions<SpotifyOptions> options, TimeProvider timeProvider)
{
    private readonly Lock sync = new();
    private DateTimeOffset nextSlot = DateTimeOffset.MinValue;
    private DateTimeOffset pausedUntil = DateTimeOffset.MinValue;

    /// <summary>
    /// Waits until a request may be sent. Returns null once it may, or the remaining pause without waiting when
    /// Spotify asked for a longer wait than <see cref="SpotifyOptions.MaxRateLimitWait"/>.
    /// </summary>
    public async Task<TimeSpan?> WaitForTurnAsync(CancellationToken cancellationToken)
    {
        while (true)
        {
            TimeSpan wait;
            lock (sync)
            {
                var now = timeProvider.GetUtcNow();
                var pause = pausedUntil - now;
                if (pause > options.Value.MaxRateLimitWait)
                {
                    return pause;
                }

                var turn = Max(now, Max(nextSlot, pausedUntil));
                if (turn <= now)
                {
                    nextSlot = now + TimeSpan.FromSeconds(1 / options.Value.MaxRequestsPerSecond);
                    return null;
                }

                wait = turn - now;
            }

            // No slot is reserved while waiting, so a pause that starts meanwhile still holds this request back.
            await Task.Delay(wait, timeProvider, cancellationToken);
        }
    }

    public void Pause(TimeSpan retryAfter)
    {
        lock (sync)
        {
            pausedUntil = Max(pausedUntil, timeProvider.GetUtcNow() + retryAfter);
        }
    }

    private static DateTimeOffset Max(DateTimeOffset first, DateTimeOffset second) => first > second ? first : second;
}
