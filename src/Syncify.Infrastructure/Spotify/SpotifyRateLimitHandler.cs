using System.Net;
using System.Net.Http.Headers;

namespace Syncify.Infrastructure.Spotify;

/// <summary>Sends Spotify API requests through the shared <see cref="SpotifyRequestGate"/>.</summary>
internal sealed class SpotifyRateLimitHandler(SpotifyRequestGate gate, TimeProvider timeProvider) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (await gate.WaitForTurnAsync(cancellationToken) is { } remainingPause)
        {
            // Answer for Spotify: a request sent during a long pause would only be rejected and prolong it.
            return new HttpResponseMessage(HttpStatusCode.TooManyRequests)
            {
                RequestMessage = request,
                Headers = { RetryAfter = new RetryConditionHeaderValue(remainingPause) },
            };
        }

        var response = await base.SendAsync(request, cancellationToken);
        if (response.StatusCode == HttpStatusCode.TooManyRequests)
        {
            gate.Pause(SpotifyRetryAfter.Read(response, timeProvider.GetUtcNow()));
        }

        return response;
    }
}
