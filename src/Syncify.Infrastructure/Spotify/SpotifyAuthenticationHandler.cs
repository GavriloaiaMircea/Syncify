using System.Net;
using System.Net.Http.Headers;

namespace Syncify.Infrastructure.Spotify;

internal sealed class SpotifyAuthenticationHandler(SpotifyTokenProvider tokens) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var token = await tokens.GetTokenAsync(cancellationToken);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await base.SendAsync(request, cancellationToken);
        if (response.StatusCode != HttpStatusCode.Unauthorized)
        {
            return response;
        }

        // Spotify can revoke a token before it expires; otherwise every request would fail until the planned renewal.
        // Retried once: a second 401 means the app itself is rejected.
        response.Dispose();
        tokens.Invalidate(token);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", await tokens.GetTokenAsync(cancellationToken));
        return await base.SendAsync(request, cancellationToken);
    }
}
