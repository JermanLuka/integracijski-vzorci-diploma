using System.Globalization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using ArchB.Monolith;
using Shared.Models;
using Shared.Target;
using Shared.Tests;

namespace ArchB.Monolith.Tests;

public class MetricOrchestratorTests
{
    private static string MakeWorldBankResponse(double value)
    {
        var v = value.ToString("G", CultureInfo.InvariantCulture);
        return "[{\"page\":1,\"pages\":1,\"total\":1},[{\"value\":" + v + "}]]";
    }

    private static Dictionary<string, string> AllFakeResponses() => new()
    {
        // GitHub
        ["/user"] = """{"public_repos":5,"followers":10,"public_gists":2}""",
        ["/user/repos?per_page=100&type=owner"] = """[{"stargazers_count":3,"forks_count":1},{"stargazers_count":7,"forks_count":2}]""",
        // StackOverflow
        ["/2.3/me?site=stackoverflow&key=test-key&access_token=test-token&filter=default"] =
            """{"items":[{"reputation":1234,"badge_counts":{"gold":1,"silver":5,"bronze":20}}]}""",
        // WorldBank
        ["/v2/country/SVN/indicator/NY.GDP.MKTP.CD?format=json&per_page=1&mrv=1"] = MakeWorldBankResponse(54000000000),
        ["/v2/country/SVN/indicator/SP.POP.TOTL?format=json&per_page=1&mrv=1"] = MakeWorldBankResponse(2100000),
        ["/v2/country/SVN/indicator/SP.DYN.LE00.IN?format=json&per_page=1&mrv=1"] = MakeWorldBankResponse(81.2),
    };

    private static Dictionary<string, string> WorldBankOnlyResponses() => new()
    {
        ["/v2/country/SVN/indicator/NY.GDP.MKTP.CD?format=json&per_page=1&mrv=1"] = MakeWorldBankResponse(54000000000),
        ["/v2/country/SVN/indicator/SP.POP.TOTL?format=json&per_page=1&mrv=1"] = MakeWorldBankResponse(2100000),
        ["/v2/country/SVN/indicator/SP.DYN.LE00.IN?format=json&per_page=1&mrv=1"] = MakeWorldBankResponse(81.2),
    };

    private static IConfigurationRoot AllTokensConfig() => new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["GitHub:Token"] = "test-gh-token",
            ["StackOverflow:AccessToken"] = "test-token",
            ["StackOverflow:ApiKey"] = "test-key",
            ["WorldBank:CountryCode"] = "SVN",
        })
        .Build();

    private static IConfigurationRoot NoTokensConfig() => new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["WorldBank:CountryCode"] = "SVN",
        })
        .Build();

    [Fact]
    public async Task RunAsync_CollectsAndSendsAllMetrics()
    {
        var handler = new FakeHttpHandler(AllFakeResponses());
        var dataAccess = new DataAccess(AllTokensConfig(), NullLoggerFactory.Instance, handler);
        var mockTarget = new MockTargetClient(NullLogger<MockTargetClient>.Instance);
        var orchestrator = new MetricOrchestrator(dataAccess, mockTarget, NullLogger<MetricOrchestrator>.Instance);

        var success = await orchestrator.RunAsync();

        Assert.True(success);
        // 5 GitHub + 4 StackOverflow + 3 WorldBank = 12 metrics
        Assert.Equal(12, mockTarget.SentMetrics.Count);
        Assert.Equal(1, mockTarget.SendCount);
    }

    [Fact]
    public async Task DataAccess_SkipsSourcesWithMissingTokens()
    {
        var handler = new FakeHttpHandler(WorldBankOnlyResponses());
        var dataAccess = new DataAccess(NoTokensConfig(), NullLoggerFactory.Instance, handler);

        var github = await dataAccess.FetchGitHubAsync();
        var stackoverflow = await dataAccess.FetchStackOverflowAsync();
        var worldbank = await dataAccess.FetchWorldBankAsync();

        Assert.Empty(github);
        Assert.Empty(stackoverflow);
        Assert.Equal(3, worldbank.Count);
        Assert.All(worldbank, m => Assert.StartsWith("worldbank.", m.Name));
    }

    [Fact]
    public async Task DataAccess_ReturnsEmptyWhenSourceFails()
    {
        // Handler with no canned responses -all HTTP calls return 404
        var handler = new FakeHttpHandler(new Dictionary<string, string>());
        var config = AllTokensConfig();
        var dataAccess = new DataAccess(config, NullLoggerFactory.Instance, handler);

        var github = await dataAccess.FetchGitHubAsync();
        var stackoverflow = await dataAccess.FetchStackOverflowAsync();
        var worldbank = await dataAccess.FetchWorldBankAsync();

        Assert.Empty(github);
        Assert.Empty(stackoverflow);
        Assert.Empty(worldbank);
    }

    [Fact]
    public async Task RunAsync_ReturnsFalseWhenNoMetricsCollected()
    {
        var handler = new FakeHttpHandler(new Dictionary<string, string>());
        var dataAccess = new DataAccess(NoTokensConfig(), NullLoggerFactory.Instance, handler);
        var mockTarget = new MockTargetClient(NullLogger<MockTargetClient>.Instance);
        var orchestrator = new MetricOrchestrator(dataAccess, mockTarget, NullLogger<MetricOrchestrator>.Instance);

        var success = await orchestrator.RunAsync();

        Assert.False(success);
        Assert.Equal(0, mockTarget.SendCount);
    }

    [Fact]
    public async Task RunAsync_ReturnsFalseWhenTargetClientFails()
    {
        var handler = new FakeHttpHandler(WorldBankOnlyResponses());
        var dataAccess = new DataAccess(NoTokensConfig(), NullLoggerFactory.Instance, handler);
        var failingTarget = new RealTargetClient(
            new HttpClient(), "http://localhost:0/invalid", NullLogger<RealTargetClient>.Instance);
        var orchestrator = new MetricOrchestrator(dataAccess, failingTarget, NullLogger<MetricOrchestrator>.Instance);

        var success = await orchestrator.RunAsync();

        Assert.False(success);
    }

    [Fact]
    public async Task DataAccess_PartialFailureDoesNotAffectOtherSources()
    {
        // GitHub and WorldBank responses present, StackOverflow will fail
        var responses = WorldBankOnlyResponses();
        responses["/user"] = """{"public_repos":5,"followers":10,"public_gists":2}""";
        responses["/user/repos?per_page=100&type=owner"] = """[{"stargazers_count":3,"forks_count":1}]""";

        var handler = new FakeHttpHandler(responses);
        var dataAccess = new DataAccess(AllTokensConfig(), NullLoggerFactory.Instance, handler);

        var github = await dataAccess.FetchGitHubAsync();
        var stackoverflow = await dataAccess.FetchStackOverflowAsync();
        var worldbank = await dataAccess.FetchWorldBankAsync();

        Assert.Equal(5, github.Count);
        Assert.Empty(stackoverflow); // failed gracefully
        Assert.Equal(3, worldbank.Count);
    }

    [Fact]
    public async Task DataAccess_FailFast_PropagatesException()
    {
        // No canned responses -all HTTP calls will fail
        var handler = new FakeHttpHandler(new Dictionary<string, string>());
        var config = AllTokensConfig();
        var dataAccess = new DataAccess(config, NullLoggerFactory.Instance, handler, failFast: true);

        // With failFast, the exception propagates instead of returning empty
        await Assert.ThrowsAsync<HttpRequestException>(() => dataAccess.FetchGitHubAsync());
    }
}
