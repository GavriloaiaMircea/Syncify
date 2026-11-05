using Microsoft.Extensions.DependencyInjection;
using Syncify.Application.Preview;
using Syncify.Application.Spotify;

namespace Syncify.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddTransient<SpotifyTrackFinder>();
        services.AddTransient<PlaylistPreviewService>();

        return services;
    }
}
