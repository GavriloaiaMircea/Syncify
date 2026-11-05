using System.ComponentModel.DataAnnotations;

namespace Syncify.Infrastructure.Spotify;

public sealed class SpotifyOptions
{
    public const string SectionName = "Spotify";

    [Required]
    public string ClientId { get; set; } = "";

    [Required]
    public string ClientSecret { get; set; } = "";

    [Required]
    public Uri AccountsBaseAddress { get; set; } = new("https://accounts.spotify.com/");

    [Required]
    public Uri ApiBaseAddress { get; set; } = new("https://api.spotify.com/v1/");

    /// <summary>
    /// ISO 3166-1 alpha-2 country whose catalog is searched. Client-credentials tokens have no user country, so
    /// without a market Spotify returns tracks that may not be playable where the playlist is listened to.
    /// </summary>
    [RegularExpression("^[A-Z]{2}$")]
    public string? Market { get; set; }

    /// <summary>The longest Retry-After the client waits out on a 429 before reporting the request as rate limited.</summary>
    public TimeSpan MaxRateLimitWait { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Requests per second across the whole app. Spotify does not publish its limit; at about 10 per second the app
    /// hit short 429s every ~100 requests and was then locked out for a day.
    /// </summary>
    [Range(0.1, 1000)]
    public double MaxRequestsPerSecond { get; set; } = 2;
}
