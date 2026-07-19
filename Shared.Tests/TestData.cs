using System.Globalization;
using Microsoft.Extensions.Configuration;

namespace Shared.Tests;

/// <summary>
/// Single source of truth for test data: the fake API responses, their URLs,
/// and the test configuration used by every test and measurement project.
/// URLs are stored in the order each source actually requests them, so the
/// first entry is always the first request a source makes.
/// </summary>
public static class TestData
{
    public const int TotalMetrics = 15;

    public static readonly string[] SourceNames = ["GitHub", "StackOverflow", "WorldBank", "OpenMeteo"];

    public static readonly IReadOnlyDictionary<string, int> MetricCountBySource = new Dictionary<string, int>
    {
        ["GitHub"] = 5,
        ["StackOverflow"] = 4,
        ["WorldBank"] = 3,
        ["OpenMeteo"] = 3,
    };

    private static string WorldBankResponse(double value) =>
        "[{\"page\":1,\"pages\":1,\"total\":1},[{\"value\":" + value.ToString("G", CultureInfo.InvariantCulture) + "}]]";

    private static readonly Dictionary<string, (string Endpoint, string Response)[]> Endpoints = new()
    {
        ["GitHub"] =
        [
            ("/user", """{"public_repos":5,"followers":10,"public_gists":2}"""),
            ("/user/repos?per_page=100&type=owner",
                """[{"stargazers_count":3,"forks_count":1},{"stargazers_count":7,"forks_count":2}]"""),
        ],
        ["StackOverflow"] =
        [
            ("/2.3/me?site=stackoverflow&key=test-key&access_token=test-token&filter=default",
                """{"items":[{"reputation":1234,"badge_counts":{"gold":1,"silver":5,"bronze":20}}]}"""),
        ],
        ["WorldBank"] =
        [
            ("/v2/country/SVN/indicator/NY.GDP.MKTP.CD?format=json&per_page=1&mrv=1", WorldBankResponse(54000000000)),
            ("/v2/country/SVN/indicator/SP.POP.TOTL?format=json&per_page=1&mrv=1", WorldBankResponse(2100000)),
            ("/v2/country/SVN/indicator/SP.DYN.LE00.IN?format=json&per_page=1&mrv=1", WorldBankResponse(81.2)),
        ],
        ["OpenMeteo"] =
        [
            ("/v1/forecast?latitude=46.05&longitude=14.51&current=temperature_2m,relative_humidity_2m,wind_speed_10m",
                """{"current":{"temperature_2m":22.5,"relative_humidity_2m":65,"wind_speed_10m":12.3}}"""),
        ],
    };

    private static readonly Dictionary<string, (string Key, string Value)[]> ConfigKeys = new()
    {
        ["GitHub"] = [("GitHub:Token", "test-gh-token")],
        ["StackOverflow"] = [("StackOverflow:AccessToken", "test-token"), ("StackOverflow:ApiKey", "test-key")],
        ["WorldBank"] = [("WorldBank:CountryCode", "SVN")],
        ["OpenMeteo"] = [("OpenMeteo:Latitude", "46.05"), ("OpenMeteo:Longitude", "14.51")],
    };

    public static string FirstEndpoint(string source) => Endpoints[source][0].Endpoint;

    public static Dictionary<string, string> AllResponses() => ResponsesFor(SourceNames);

    public static Dictionary<string, string> ResponsesFor(params string[] sources)
    {
        var responses = new Dictionary<string, string>();
        foreach (var source in sources)
            foreach (var (endpoint, response) in Endpoints[source])
                responses[endpoint] = response;
        return responses;
    }

    public static Dictionary<string, string> ResponsesExcept(string failingSource) =>
        ResponsesFor(SourceNames.Where(s => s != failingSource).ToArray());

    public static IConfigurationRoot Config() => ConfigFor(SourceNames);

    public static IConfigurationRoot ConfigFor(params string[] sources)
    {
        var values = new Dictionary<string, string?>();
        foreach (var source in sources)
            foreach (var (key, value) in ConfigKeys[source])
                values[key] = value;
        return new ConfigurationBuilder().AddInMemoryCollection(values).Build();
    }
}
