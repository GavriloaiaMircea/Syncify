using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Syncify.Application.YouTube;
using Syncify.Infrastructure.YouTube;

namespace Syncify.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
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
        });

        return services;
    }
}
