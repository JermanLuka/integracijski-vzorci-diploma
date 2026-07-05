using System.Globalization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using ArchC.Messaging;
using ArchC.Messaging.Producers;
using Shared.Target;
using Shared.Tests;

namespace ArchC.Messaging.Tests;

public class MessagingOrchestratorTests
{
    private static string MakeWorldBankResponse(double value)
    {
        var v = value.ToString("G", CultureInfo.InvariantCulture);
        return "[{\"page\":1,\"pages\":1,\"total\":1},[{\"value\":" + v + "}]]";
    }

    private const string OpenMeteoResponse =
        """{"current":{"temperature_2m":22.5,"relative_humidity_2m":65,"wind_speed_10m":12.3}}""";

    private static Dictionary<string, string> AllFakeResponses() => new()
    {
        ["/user"] = """{"public_repos":5,"followers":10,"public_gists":2}""",
        ["/user/repos?per_page=100&type=owner"] = """[{"stargazers_count":3,"forks_count":1},{"stargazers_count":7,"forks_count":2}]""",
        ["/2.3/me?site=stackoverflow&key=test-key&access_token=test-token&filter=default"] =
            """{"items":[{"reputation":1234,"badge_counts":{"gold":1,"silver":5,"bronze":20}}]}""",
        ["/v2/country/SVN/indicator/NY.GDP.MKTP.CD?format=json&per_page=1&mrv=1"] = MakeWorldBankResponse(54000000000),
        ["/v2/country/SVN/indicator/SP.POP.TOTL?format=json&per_page=1&mrv=1"] = MakeWorldBankResponse(2100000),
        ["/v2/country/SVN/indicator/SP.DYN.LE00.IN?format=json&per_page=1&mrv=1"] = MakeWorldBankResponse(81.2),
        ["/v1/forecast?latitude=46.05&longitude=14.51&current=temperature_2m,relative_humidity_2m,wind_speed_10m"] = OpenMeteoResponse,
    };

    private static IConfigurationRoot AllTokensConfig() => new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["GitHub:Token"] = "test-gh-token",
            ["StackOverflow:AccessToken"] = "test-token",
            ["StackOverflow:ApiKey"] = "test-key",
            ["WorldBank:CountryCode"] = "SVN",
            ["OpenMeteo:Latitude"] = "46.05",
            ["OpenMeteo:Longitude"] = "14.51",
        })
        .Build();

    [Fact]
    public async Task FullPipeline_AllSourcesSucceed_AllMetricsSent()
    {
        var handler = new FakeHttpHandler(AllFakeResponses());
        var config = AllTokensConfig();
        var loggerFactory = NullLoggerFactory.Instance;

        var github = new GitHubProducer(config, loggerFactory, handler);
        var stackoverflow = new StackOverflowProducer(config, loggerFactory, handler);
        var worldbank = new WorldBankProducer(config, loggerFactory, handler);
        var openmeteo = new OpenMeteoProducer(config, loggerFactory, handler);

        var mockTarget = new MockTargetClient(NullLogger<MockTargetClient>.Instance);
        var consumer = new MetricConsumer(mockTarget, NullLogger<MetricConsumer>.Instance);

        var orchestrator = new MessagingOrchestrator(
            github, stackoverflow, worldbank, openmeteo, consumer,
            NullLogger<MessagingOrchestrator>.Instance);

        var success = await orchestrator.RunAsync();

        Assert.True(success);
        // 5 GitHub + 4 StackOverflow + 3 WorldBank + 3 OpenMeteo = 15
        Assert.Equal(15, mockTarget.SentMetrics.Count);
    }

    [Fact]
    public async Task FaultTolerance_OneSourceFails_OthersContinue()
    {
        // Only WorldBank, GitHub, and OpenMeteo responses, StackOverflow will fail
        var responses = new Dictionary<string, string>
        {
            ["/user"] = """{"public_repos":5,"followers":10,"public_gists":2}""",
            ["/user/repos?per_page=100&type=owner"] = """[{"stargazers_count":3,"forks_count":1}]""",
            ["/v2/country/SVN/indicator/NY.GDP.MKTP.CD?format=json&per_page=1&mrv=1"] = MakeWorldBankResponse(54000000000),
            ["/v2/country/SVN/indicator/SP.POP.TOTL?format=json&per_page=1&mrv=1"] = MakeWorldBankResponse(2100000),
            ["/v2/country/SVN/indicator/SP.DYN.LE00.IN?format=json&per_page=1&mrv=1"] = MakeWorldBankResponse(81.2),
            ["/v1/forecast?latitude=46.05&longitude=14.51&current=temperature_2m,relative_humidity_2m,wind_speed_10m"] = OpenMeteoResponse,
        };

        var handler = new FakeHttpHandler(responses);
        var config = AllTokensConfig();
        var loggerFactory = NullLoggerFactory.Instance;

        var github = new GitHubProducer(config, loggerFactory, handler);
        var stackoverflow = new StackOverflowProducer(config, loggerFactory, handler);
        var worldbank = new WorldBankProducer(config, loggerFactory, handler);
        var openmeteo = new OpenMeteoProducer(config, loggerFactory, handler);

        var mockTarget = new MockTargetClient(NullLogger<MockTargetClient>.Instance);
        var consumer = new MetricConsumer(mockTarget, NullLogger<MetricConsumer>.Instance);

        var orchestrator = new MessagingOrchestrator(
            github, stackoverflow, worldbank, openmeteo, consumer,
            NullLogger<MessagingOrchestrator>.Instance);

        var success = await orchestrator.RunAsync();

        Assert.True(success);
        // 5 GitHub + 0 StackOverflow (failed) + 3 WorldBank + 3 OpenMeteo = 11
        Assert.Equal(11, mockTarget.SentMetrics.Count);
        Assert.DoesNotContain(mockTarget.SentMetrics, m => m.Name.StartsWith("stackoverflow."));
        Assert.Contains(mockTarget.SentMetrics, m => m.Name.StartsWith("github."));
        Assert.Contains(mockTarget.SentMetrics, m => m.Name.StartsWith("worldbank."));
    }

    [Fact]
    public async Task GracefulShutdown_ChannelDrainsAndExits()
    {
        // Only WorldBank and OpenMeteo (no tokens for GitHub/StackOverflow)
        var responses = new Dictionary<string, string>
        {
            ["/v2/country/SVN/indicator/NY.GDP.MKTP.CD?format=json&per_page=1&mrv=1"] = MakeWorldBankResponse(54000000000),
            ["/v2/country/SVN/indicator/SP.POP.TOTL?format=json&per_page=1&mrv=1"] = MakeWorldBankResponse(2100000),
            ["/v2/country/SVN/indicator/SP.DYN.LE00.IN?format=json&per_page=1&mrv=1"] = MakeWorldBankResponse(81.2),
            ["/v1/forecast?latitude=46.05&longitude=14.51&current=temperature_2m,relative_humidity_2m,wind_speed_10m"] = OpenMeteoResponse,
        };

        var handler = new FakeHttpHandler(responses);
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["WorldBank:CountryCode"] = "SVN",
                ["OpenMeteo:Latitude"] = "46.05",
                ["OpenMeteo:Longitude"] = "14.51",
            })
            .Build();
        var loggerFactory = NullLoggerFactory.Instance;

        var github = new GitHubProducer(config, loggerFactory, handler);
        var stackoverflow = new StackOverflowProducer(config, loggerFactory, handler);
        var worldbank = new WorldBankProducer(config, loggerFactory, handler);
        var openmeteo = new OpenMeteoProducer(config, loggerFactory, handler);

        var mockTarget = new MockTargetClient(NullLogger<MockTargetClient>.Instance);
        var consumer = new MetricConsumer(mockTarget, NullLogger<MetricConsumer>.Instance);

        var orchestrator = new MessagingOrchestrator(
            github, stackoverflow, worldbank, openmeteo, consumer,
            NullLogger<MessagingOrchestrator>.Instance);

        // Should complete without hanging
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var success = await orchestrator.RunAsync(cts.Token);

        Assert.True(success);
        // 3 WorldBank + 3 OpenMeteo = 6
        Assert.Equal(6, mockTarget.SentMetrics.Count);
    }

    [Fact]
    public async Task NoMetrics_ReturnsFalse()
    {
        var handler = new FakeHttpHandler(new Dictionary<string, string>());
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["WorldBank:CountryCode"] = "SVN",
                ["OpenMeteo:Latitude"] = "46.05",
                ["OpenMeteo:Longitude"] = "14.51",
            })
            .Build();
        var loggerFactory = NullLoggerFactory.Instance;

        var github = new GitHubProducer(config, loggerFactory, handler);
        var stackoverflow = new StackOverflowProducer(config, loggerFactory, handler);
        var worldbank = new WorldBankProducer(config, loggerFactory, handler);
        var openmeteo = new OpenMeteoProducer(config, loggerFactory, handler);

        var mockTarget = new MockTargetClient(NullLogger<MockTargetClient>.Instance);
        var consumer = new MetricConsumer(mockTarget, NullLogger<MetricConsumer>.Instance);

        var orchestrator = new MessagingOrchestrator(
            github, stackoverflow, worldbank, openmeteo, consumer,
            NullLogger<MessagingOrchestrator>.Instance);

        var success = await orchestrator.RunAsync();

        Assert.False(success);
        Assert.Equal(0, mockTarget.SendCount);
    }
}
