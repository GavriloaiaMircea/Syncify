using System.Web;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Syncify.Application.Spotify;
using WireMock;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace Syncify.Infrastructure.Tests.Spotify;

/// <summary>A WireMock stand-in for the Spotify accounts service and Web API that serves recorded responses from <c>Fixtures/</c>.</summary>
internal sealed class SpotifyApiFake : IDisposable
{
    public const string ClientId = "test-client-id";
    public const string ClientSecret = "test-client-secret";
    public const string AccessToken = "test-access-token";
    public const string TokenPath = "/accounts/api/token";
    public const string SearchPath = "/v1/search";

    // Lower wins, so a test can replace the default answer.
    private const int OverridePriority = 1;
    private const int DefaultPriority = 10;

    private readonly WireMockServer server = WireMockServer.Start();
    private readonly List<ServiceProvider> providers = [];

    public SpotifyApiFake()
    {
        server.Given(Request.Create().WithPath(TokenPath).UsingPost())
            .AtPriority(DefaultPriority)
            .RespondWith(JsonResponse(200).WithBody(Fixture("token.json")));

        server.Given(Request.Create().WithPath(SearchPath).UsingGet())
            .AtPriority(DefaultPriority)
            .RespondWith(JsonResponse(200).WithBody(Fixture("search-free-text.json")));
    }

    public IReadOnlyList<IRequestMessage> Requests => server.LogEntries.Select(entry => entry.RequestMessage!).ToList();

    public IReadOnlyList<IRequestMessage> RequestsTo(string path) => Requests.Where(request => request.Path == path).ToList();

    public ISpotifyClient CreateClient(
        string? clientId = ClientId,
        string? market = null,
        TimeProvider? timeProvider = null,
        TimeSpan? maxRateLimitWait = null,
        double maxRequestsPerSecond = 1000) =>
        CreateServices(clientId, market, timeProvider, maxRateLimitWait, maxRequestsPerSecond).GetRequiredService<ISpotifyClient>();

    /// <summary>
    /// A container like the app's; clients resolved from the same one share the token and the request gate.
    /// Pacing defaults to 1000 requests per second so tests that do not check it stay fast.
    /// </summary>
    public IServiceProvider CreateServices(
        string? clientId = ClientId,
        string? market = null,
        TimeProvider? timeProvider = null,
        TimeSpan? maxRateLimitWait = null,
        double maxRequestsPerSecond = 1000)
    {
        var settings = new Dictionary<string, string?>
        {
            ["Spotify:ClientId"] = clientId,
            ["Spotify:ClientSecret"] = ClientSecret,
            ["Spotify:AccountsBaseAddress"] = server.Url + "/accounts/",
            ["Spotify:ApiBaseAddress"] = server.Url + "/v1/",
            ["Spotify:Market"] = market,
            ["Spotify:MaxRequestsPerSecond"] = maxRequestsPerSecond.ToString(System.Globalization.CultureInfo.InvariantCulture),
        };

        // A null value would bind as TimeSpan.Zero instead of leaving the default.
        if (maxRateLimitWait is { } wait)
        {
            settings["Spotify:MaxRateLimitWait"] = wait.ToString();
        }

        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();

        var services = new ServiceCollection()
            .AddSingleton<IConfiguration>(configuration);

        if (timeProvider is not null)
        {
            services.AddSingleton(timeProvider);
        }

        services.AddInfrastructure();

        // Keep the real retry policy but without the backoff, so retry tests take milliseconds.
        services.ConfigureAll<HttpStandardResilienceOptions>(options => options.Retry.Delay = TimeSpan.Zero);

        var provider = services.BuildServiceProvider();
        providers.Add(provider);

        return provider;
    }

    public void RespondToSearch(string fixture, int statusCode = 200) =>
        server.Given(Request.Create().WithPath(SearchPath).UsingGet())
            .AtPriority(OverridePriority)
            .RespondWith(JsonResponse(statusCode).WithBody(Fixture(fixture)));

    public void RespondToSearchWithJson(string json) =>
        server.Given(Request.Create().WithPath(SearchPath).UsingGet())
            .AtPriority(OverridePriority)
            .RespondWith(JsonResponse(200).WithBody(json));

    public void RespondToToken(string fixture, int statusCode) =>
        server.Given(Request.Create().WithPath(TokenPath).UsingPost())
            .AtPriority(OverridePriority)
            .RespondWith(JsonResponse(statusCode).WithBody(Fixture(fixture)));

    /// <summary>Answers every search with 429 and the given Retry-After header, or none when null.</summary>
    public void RateLimitSearches(string? retryAfter) =>
        server.Given(Request.Create().WithPath(SearchPath).UsingGet())
            .AtPriority(OverridePriority)
            .RespondWith(RateLimitResponse(retryAfter));

    /// <summary>Answers the first search with 429, later searches as before.</summary>
    public void RateLimitFirstSearch(string retryAfter) =>
        server.Given(Request.Create().WithPath(SearchPath).UsingGet())
            .InScenario("search rate limited once")
            .WillSetStateTo("recovered")
            .AtPriority(OverridePriority)
            .RespondWith(RateLimitResponse(retryAfter));

    public void RespondToSearchWithStatus(int statusCode) =>
        server.Given(Request.Create().WithPath(SearchPath).UsingGet())
            .AtPriority(OverridePriority)
            .RespondWith(Response.Create().WithStatusCode(statusCode));

    public void FailFirstSearch(int statusCode) =>
        server.Given(Request.Create().WithPath(SearchPath).UsingGet())
            .InScenario("search fails once")
            .WillSetStateTo("recovered")
            .AtPriority(OverridePriority)
            .RespondWith(Response.Create().WithStatusCode(statusCode));

    public static string? QueryValue(IRequestMessage request, string name) =>
        HttpUtility.ParseQueryString(new Uri(request.AbsoluteUrl).Query)[name];

    public static string? Header(IRequestMessage request, string name) =>
        request.Headers?.FirstOrDefault(header => string.Equals(header.Key, name, StringComparison.OrdinalIgnoreCase)).Value?.FirstOrDefault();

    public static string Fixture(string name) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Spotify", "Fixtures", name));

    public void Dispose()
    {
        providers.ForEach(provider => provider.Dispose());
        server.Stop();
        server.Dispose();
    }

    private static IResponseBuilder RateLimitResponse(string? retryAfter)
    {
        var response = Response.Create().WithStatusCode(429);
        return retryAfter is null ? response : response.WithHeader("Retry-After", retryAfter);
    }

    private static IResponseBuilder JsonResponse(int statusCode) =>
        Response.Create().WithStatusCode(statusCode).WithHeader("Content-Type", "application/json; charset=utf-8");
}
