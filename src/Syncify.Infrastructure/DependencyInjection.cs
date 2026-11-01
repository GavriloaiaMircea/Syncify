using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Options;
using Polly;
using Syncify.Application.Spotify;
using Syncify.Application.YouTube;
using Syncify.Infrastructure.Spotify;
using Syncify.Infrastructure.YouTube;

namespace Syncify.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);

        AddYouTube(services);
        AddSpotify(services);

        return services;
    }

    private static void AddYouTube(IServiceCollection services)
    {
        // Not validated on start: the API should still boot (health checks, docs) on a machine without a key.
        services.AddOptions<YouTubeOptions>()
            .BindConfiguration(YouTubeOptions.SectionName)
            .ValidateDataAnnotations();

        services.AddHttpClient<IYouTubeClient, YouTubeClient>((provider, http) =>
        {
            var options = provider.GetRequiredService<IOptions<YouTubeOptions>>().Value;
            http.BaseAddress = options.BaseAddress;
            http.DefaultRequestHeaders.Add("X-Goog-Api-Key", options.ApiKey);
        })
        // Google APIs return occasional 5xx "backendError"; one failed page must not fail a scheduled sync.
        // Errors with a meaning (400, 403 quota, 404) are not retried.
        .AddStandardResilienceHandler();
    }

    private static void AddSpotify(IServiceCollection services)
    {
        // Like YouTube, not validated on start.
        services.AddOptions<SpotifyOptions>()
            .BindConfiguration(SpotifyOptions.SectionName)
            .ValidateDataAnnotations();

        services.AddSingleton<SpotifyTokenProvider>();
        services.AddTransient<SpotifyAuthenticationHandler>();

        services.AddHttpClient(SpotifyTokenProvider.HttpClientName, (provider, http) =>
            http.BaseAddress = provider.GetRequiredService<IOptions<SpotifyOptions>>().Value.AccountsBaseAddress)
            .AddStandardResilienceHandler();

        services.AddHttpClient<ISpotifyClient, SpotifyClient>((provider, http) =>
            http.BaseAddress = provider.GetRequiredService<IOptions<SpotifyOptions>>().Value.ApiBaseAddress)
            // Outside the resilience handler, so retries reuse the token instead of asking for one each time.
            .AddHttpMessageHandler<SpotifyAuthenticationHandler>()
            .AddStandardResilienceHandler(options =>
            {
                // SpotifyClient handles 429 itself: it waits out a short Retry-After and reports a long one as a
                // result. Retrying here would sleep through long waits, and counting 429s would open the breaker.
                options.Retry.ShouldHandle = args => ValueTask.FromResult(IsTransientExceptRateLimit(args.Outcome));
                options.CircuitBreaker.ShouldHandle = args => ValueTask.FromResult(IsTransientExceptRateLimit(args.Outcome));
            });
    }

    private static bool IsTransientExceptRateLimit(Outcome<HttpResponseMessage> outcome) =>
        outcome.Result?.StatusCode != HttpStatusCode.TooManyRequests && HttpClientResiliencePredicates.IsTransient(outcome);
}
