using System.Globalization;
using System.Threading.Channels;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using ArchC.Messaging.Producers;
using Shared.Models;
using Shared.Target;

namespace ArchC.Messaging.Tests;

public class ProducerRetryTests
{
    private const string OpenMeteoPath =
        "/v1/forecast?latitude=46.05&longitude=14.51&current=temperature_2m,relative_humidity_2m,wind_speed_10m";

    private const string OpenMeteoResponse =
        """{"current":{"temperature_2m":22.5,"relative_humidity_2m":65,"wind_speed_10m":12.3}}""";

    private static IConfigurationRoot OpenMeteoConfig() => new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["OpenMeteo:Latitude"] = "46.05",
            ["OpenMeteo:Longitude"] = "14.51",
        })
        .Build();

    private static async Task<List<Metric>> DrainAsync(ChannelReader<Metric> reader)
    {
        var metrics = new List<Metric>();
        await foreach (var metric in reader.ReadAllAsync())
            metrics.Add(metric);
        return metrics;
    }

    [Fact]
    public async Task Producer_SourceFailsOnceThenRecovers_MetricsStillWrittenToChannel()
    {
        var handler = new FlakyHttpHandler(
            new Dictionary<string, string> { [OpenMeteoPath] = OpenMeteoResponse },
            new Dictionary<string, int> { [OpenMeteoPath] = 1 });

        var producer = new OpenMeteoProducer(OpenMeteoConfig(), NullLoggerFactory.Instance, handler);
        var channel = Channel.CreateUnbounded<Metric>();

        await producer.ProduceAsync(channel.Writer);
        channel.Writer.Complete();

        var metrics = await DrainAsync(channel.Reader);

        Assert.Equal(3, metrics.Count);
        Assert.All(metrics, m => Assert.StartsWith("weather.", m.Name));
        // First request failed, retry succeeded
        Assert.Equal(2, handler.RequestCount);
    }

    [Fact]
    public async Task Producer_SourceFailsAllAttempts_FinishesWithoutExceptionAndWritesNothing()
    {
        var handler = new FlakyHttpHandler(
            new Dictionary<string, string> { [OpenMeteoPath] = OpenMeteoResponse },
            new Dictionary<string, int> { [OpenMeteoPath] = int.MaxValue });

        var producer = new OpenMeteoProducer(OpenMeteoConfig(), NullLoggerFactory.Instance, handler);
        var channel = Channel.CreateUnbounded<Metric>();

        await producer.ProduceAsync(channel.Writer);
        channel.Writer.Complete();

        var metrics = await DrainAsync(channel.Reader);

        Assert.Empty(metrics);
        // Exactly three attempts, then controlled give-up
        Assert.Equal(3, handler.RequestCount);
    }

    [Fact]
    public async Task FullPipeline_OneSourceRecoversAfterTransientFailure_AllMetricsSent()
    {
        const string stackOverflowPath = "/2.3/me?site=stackoverflow&key=test-key&access_token=test-token&filter=default";

        static string MakeWorldBankResponse(double value) =>
            "[{\"page\":1,\"pages\":1,\"total\":1},[{\"value\":" + value.ToString("G", CultureInfo.InvariantCulture) + "}]]";

        var responses = new Dictionary<string, string>
        {
            ["/user"] = """{"public_repos":5,"followers":10,"public_gists":2}""",
            ["/user/repos?per_page=100&type=owner"] = """[{"stargazers_count":3,"forks_count":1},{"stargazers_count":7,"forks_count":2}]""",
            [stackOverflowPath] = """{"items":[{"reputation":1234,"badge_counts":{"gold":1,"silver":5,"bronze":20}}]}""",
            ["/v2/country/SVN/indicator/NY.GDP.MKTP.CD?format=json&per_page=1&mrv=1"] = MakeWorldBankResponse(54000000000),
            ["/v2/country/SVN/indicator/SP.POP.TOTL?format=json&per_page=1&mrv=1"] = MakeWorldBankResponse(2100000),
            ["/v2/country/SVN/indicator/SP.DYN.LE00.IN?format=json&per_page=1&mrv=1"] = MakeWorldBankResponse(81.2),
            [OpenMeteoPath] = OpenMeteoResponse,
        };

        // StackOverflow is transiently down: first request fails, retry succeeds
        var handler = new FlakyHttpHandler(responses, new Dictionary<string, int> { [stackOverflowPath] = 1 });

        var config = new ConfigurationBuilder()
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
        // 5 GitHub + 4 StackOverflow + 3 WorldBank + 3 OpenMeteo = 15, nothing lost
        Assert.Equal(15, mockTarget.SentMetrics.Count);
        Assert.Contains(mockTarget.SentMetrics, m => m.Name.StartsWith("stackoverflow."));
    }
}
