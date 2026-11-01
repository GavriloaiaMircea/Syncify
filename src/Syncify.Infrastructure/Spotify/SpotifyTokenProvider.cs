using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using Microsoft.Extensions.Options;

namespace Syncify.Infrastructure.Spotify;

/// <summary>
/// Gets app tokens with the client-credentials flow, which is enough for catalog search and needs no user login.
/// A singleton, so all Spotify requests share one token until shortly before it expires.
/// </summary>
internal sealed class SpotifyTokenProvider(IHttpClientFactory httpClientFactory, IOptions<SpotifyOptions> options, TimeProvider timeProvider)
    : IDisposable
{
    public const string HttpClientName = "SpotifyAccounts";

    // Renew early so a token never expires between being handed out and reaching Spotify.
    private static readonly TimeSpan RefreshMargin = TimeSpan.FromMinutes(1);

    private readonly SemaphoreSlim refreshLock = new(1, 1);
    private volatile CachedToken? cached;

    public async ValueTask<string> GetTokenAsync(CancellationToken cancellationToken)
    {
        if (cached is { } token && timeProvider.GetUtcNow() < token.RefreshAt)
        {
            return token.Value;
        }

        await refreshLock.WaitAsync(cancellationToken);
        try
        {
            // Another request may have renewed it while this one waited.
            if (cached is { } renewed && timeProvider.GetUtcNow() < renewed.RefreshAt)
            {
                return renewed.Value;
            }

            var fresh = await RequestTokenAsync(cancellationToken);
            cached = fresh;
            return fresh.Value;
        }
        finally
        {
            refreshLock.Release();
        }
    }

    public void Dispose() => refreshLock.Dispose();

    private async Task<CachedToken> RequestTokenAsync(CancellationToken cancellationToken)
    {
        var settings = options.Value;
        var http = httpClientFactory.CreateClient(HttpClientName);
        var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{settings.ClientId}:{settings.ClientSecret}"));

        using var request = new HttpRequestMessage(HttpMethod.Post, "api/token")
        {
            Content = new FormUrlEncodedContent([new("grant_type", "client_credentials")]),
            Headers = { Authorization = new AuthenticationHeaderValue("Basic", credentials) },
        };

        var requestedAt = timeProvider.GetUtcNow();
        using var response = await http.SendAsync(request, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            // Wrong credentials come back as 400 "invalid_client": a configuration problem, not an outcome.
            var error = await ReadErrorAsync(response, cancellationToken);
            throw new HttpRequestException(
                $"Spotify token request failed with {(int)response.StatusCode} ({error ?? "no error code"}). Check Spotify:ClientId and Spotify:ClientSecret.",
                inner: null,
                response.StatusCode);
        }

        var body = await response.Content.ReadFromJsonAsync<TokenResponse>(cancellationToken)
            ?? throw new InvalidOperationException("Spotify returned an empty token response.");

        return new CachedToken(body.AccessToken, requestedAt + TimeSpan.FromSeconds(body.ExpiresIn) - RefreshMargin);
    }

    private static async Task<string?> ReadErrorAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            var body = await response.Content.ReadFromJsonAsync<TokenErrorResponse>(cancellationToken);
            return body?.Error;
        }
        catch (System.Text.Json.JsonException)
        {
            return null;
        }
    }

    private sealed record CachedToken(string Value, DateTimeOffset RefreshAt);
}
