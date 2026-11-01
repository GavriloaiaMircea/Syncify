using System.Text.Json.Nodes;
using System.Web;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Syncify.Application.YouTube;
using WireMock;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace Syncify.Infrastructure.Tests.YouTube;

/// <summary>A WireMock stand-in for the YouTube Data API that serves recorded responses from <c>Fixtures/</c>.</summary>
internal sealed class YouTubeApiFake : IDisposable
{
    public const string ApiKey = "test-api-key";
    private const string ApiPath = "/youtube/v3/";

    // Lower wins, so a test can replace one recorded call with an error response.
    private const int OverridePriority = 1;
    private const int RecordedPriority = 10;

    private readonly WireMockServer server = WireMockServer.Start();
    private readonly List<ServiceProvider> providers = [];

    public IReadOnlyList<IRequestMessage> Requests => server.LogEntries.Select(entry => entry.RequestMessage!).ToList();

    public IReadOnlyList<IRequestMessage> RequestsTo(string resource) =>
        Requests.Where(request => request.Path == ApiPath + resource).ToList();

    public IYouTubeClient CreateClient(string? apiKey = ApiKey, string apiPath = ApiPath)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["YouTube:ApiKey"] = apiKey,
                ["YouTube:BaseAddress"] = server.Url + apiPath,
            })
            .Build();

        var services = new ServiceCollection()
            .AddSingleton<IConfiguration>(configuration)
            .AddInfrastructure();

        // Keep the real retry policy but without the backoff, so retry tests take milliseconds.
        services.ConfigureAll<HttpStandardResilienceOptions>(options => options.Retry.Delay = TimeSpan.Zero);

        var provider = services.BuildServiceProvider();
        providers.Add(provider);

        return provider.GetRequiredService<IYouTubeClient>();
    }

    /// <summary>Serves a playlist recorded as playlists.json, playlistItems-N.json and videos-N.json.</summary>
    public void AddRecordedPlaylist(string playlistId)
    {
        var directory = Path.Combine(FixturesDirectory, playlistId);

        server.Given(Request.Create().WithPath(ApiPath + "playlists").UsingGet())
            .AtPriority(RecordedPriority)
            .RespondWith(JsonResponse(200).WithBody(File.ReadAllText(Path.Combine(directory, "playlists.json"))));

        var pagesByToken = new Dictionary<string, string>();
        var previousToken = "";
        for (var page = 1; File.Exists(Path.Combine(directory, $"playlistItems-{page}.json")); page++)
        {
            var json = File.ReadAllText(Path.Combine(directory, $"playlistItems-{page}.json"));
            pagesByToken[previousToken] = json;
            previousToken = JsonNode.Parse(json)!["nextPageToken"]?.GetValue<string>() ?? "";
        }

        var videosById = Directory.GetFiles(directory, "videos-*.json")
            .SelectMany(file => JsonNode.Parse(File.ReadAllText(file))!["items"]!.AsArray())
            .ToDictionary(video => video!["id"]!.GetValue<string>(), video => video!);

        server.Given(Request.Create().WithPath(ApiPath + "playlistItems").UsingGet())
            .AtPriority(RecordedPriority)
            .RespondWith(JsonResponse(200).WithBody(request => pagesByToken[QueryValue(request, "pageToken") ?? ""]));

        // videos.list returns only the requested IDs it still serves, which is how deleted videos disappear.
        server.Given(Request.Create().WithPath(ApiPath + "videos").UsingGet())
            .AtPriority(RecordedPriority)
            .RespondWith(JsonResponse(200).WithBody(request =>
            {
                var items = QueryValue(request, "id")!.Split(',')
                    .Where(videosById.ContainsKey)
                    .Select(id => videosById[id].DeepClone());
                return new JsonObject { ["items"] = new JsonArray([.. items]) }.ToJsonString();
            }));
    }

    public void RespondWithJson(string resource, string json, int statusCode = 200) =>
        server.Given(Request.Create().WithPath(ApiPath + resource).UsingGet())
            .AtPriority(OverridePriority)
            .RespondWith(JsonResponse(statusCode).WithBody(json));

    public void RespondWithFixture(string resource, string fixture, int statusCode) =>
        RespondWithJson(resource, File.ReadAllText(Path.Combine(FixturesDirectory, fixture)), statusCode);

    /// <summary>Answers the first call to <paramref name="resource"/> with an error, later calls as before.</summary>
    public void FailOnce(string resource, int statusCode) =>
        server.Given(Request.Create().WithPath(ApiPath + resource).UsingGet())
            .InScenario($"{resource} fails once")
            .WillSetStateTo("recovered")
            .AtPriority(OverridePriority)
            .RespondWith(Response.Create().WithStatusCode(statusCode));

    public void RespondWithText(string resource, int statusCode, string body) =>
        server.Given(Request.Create().WithPath(ApiPath + resource).UsingGet())
            .AtPriority(OverridePriority)
            .RespondWith(Response.Create().WithStatusCode(statusCode).WithBody(body));

    public static string? QueryValue(IRequestMessage request, string name) =>
        HttpUtility.ParseQueryString(new Uri(request.AbsoluteUrl).Query)[name];

    public static string? Header(IRequestMessage request, string name) =>
        request.Headers?.FirstOrDefault(header => string.Equals(header.Key, name, StringComparison.OrdinalIgnoreCase)).Value?.FirstOrDefault();

    public void Dispose()
    {
        providers.ForEach(provider => provider.Dispose());
        server.Stop();
        server.Dispose();
    }

    private static string FixturesDirectory => Path.Combine(AppContext.BaseDirectory, "YouTube", "Fixtures");

    private static IResponseBuilder JsonResponse(int statusCode) =>
        Response.Create().WithStatusCode(statusCode).WithHeader("Content-Type", "application/json; charset=UTF-8");
}
