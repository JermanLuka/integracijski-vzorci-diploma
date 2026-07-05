using System.Globalization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Shared.Target;
using Shared.Tests;

namespace Measurements;

public sealed class FaultToleranceRunner
{
    private static string MakeWorldBankResponse(double value)
    {
        var v = value.ToString("G", CultureInfo.InvariantCulture);
        return "[{\"page\":1,\"pages\":1,\"total\":1},[{\"value\":" + v + "}]]";
    }

    private static readonly Dictionary<string, string> GitHubResponses = new()
    {
        ["/user"] = """{"public_repos":5,"followers":10,"public_gists":2}""",
        ["/user/repos?per_page=100&type=owner"] = """[{"stargazers_count":3,"forks_count":1},{"stargazers_count":7,"forks_count":2}]""",
    };

    private static readonly Dictionary<string, string> StackOverflowResponses = new()
    {
        ["/2.3/me?site=stackoverflow&key=test-key&access_token=test-token&filter=default"] =
            """{"items":[{"reputation":1234,"badge_counts":{"gold":1,"silver":5,"bronze":20}}]}""",
    };

    private static readonly Dictionary<string, string> WorldBankResponses = new()
    {
        ["/v2/country/SVN/indicator/NY.GDP.MKTP.CD?format=json&per_page=1&mrv=1"] = MakeWorldBankResponse(54000000000),
        ["/v2/country/SVN/indicator/SP.POP.TOTL?format=json&per_page=1&mrv=1"] = MakeWorldBankResponse(2100000),
        ["/v2/country/SVN/indicator/SP.DYN.LE00.IN?format=json&per_page=1&mrv=1"] = MakeWorldBankResponse(81.2),
    };

    private static readonly Dictionary<string, string> OpenMeteoResponses = new()
    {
        ["/v1/forecast?latitude=46.05&longitude=14.51&current=temperature_2m,relative_humidity_2m,wind_speed_10m"] =
            """{"current":{"temperature_2m":22.5,"relative_humidity_2m":65,"wind_speed_10m":12.3}}""",
    };

    // Expected metric counts per source
    private const int GitHubMetrics = 5;
    private const int StackOverflowMetrics = 4;
    private const int WorldBankMetrics = 3;
    private const int OpenMeteoMetrics = 3;
    private const int TotalMetrics = GitHubMetrics + StackOverflowMetrics + WorldBankMetrics + OpenMeteoMetrics;

    private static readonly string[] SourceNames = ["GitHub", "StackOverflow", "WorldBank", "OpenMeteo"];

    private static IConfigurationRoot BuildConfig() => new ConfigurationBuilder()
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

    private static Dictionary<string, string> AllResponsesExcept(string failingSource)
    {
        var responses = new Dictionary<string, string>();

        if (failingSource != "GitHub")
            foreach (var kv in GitHubResponses) responses[kv.Key] = kv.Value;

        if (failingSource != "StackOverflow")
            foreach (var kv in StackOverflowResponses) responses[kv.Key] = kv.Value;

        if (failingSource != "WorldBank")
            foreach (var kv in WorldBankResponses) responses[kv.Key] = kv.Value;

        if (failingSource != "OpenMeteo")
            foreach (var kv in OpenMeteoResponses) responses[kv.Key] = kv.Value;

        return responses;
    }

    private static int ExpectedMetricsWhenFailing(string failingSource) => failingSource switch
    {
        "GitHub" => TotalMetrics - GitHubMetrics,
        "StackOverflow" => TotalMetrics - StackOverflowMetrics,
        "WorldBank" => TotalMetrics - WorldBankMetrics,
        "OpenMeteo" => TotalMetrics - OpenMeteoMetrics,
        _ => throw new ArgumentException($"Unknown source: {failingSource}"),
    };

    public record ExperimentResult(
        string Architecture,
        string FailingSource,
        int ExpectedMetrics,
        int ActualMetrics,
        double Percentage,
        bool Continued);

    public async Task<List<ExperimentResult>> RunAllAsync()
    {
        var results = new List<ExperimentResult>();

        foreach (var failingSource in SourceNames)
        {
            results.Add(await RunMonolithAsync(failingSource, failFast: false));
            results.Add(await RunMonolithAsync(failingSource, failFast: true));
            results.Add(await RunHexagonalAsync(failingSource));
            results.Add(await RunMessagingAsync(failingSource));
        }

        return results;
    }

    private async Task<ExperimentResult> RunMonolithAsync(string failingSource, bool failFast)
    {
        var handler = new FakeHttpHandler(AllResponsesExcept(failingSource));
        var config = BuildConfig();
        var loggerFactory = NullLoggerFactory.Instance;

        var dataAccess = new ArchB.Monolith.DataAccess(config, loggerFactory, handler, failFast: failFast);
        var target = new MockTargetClient(NullLogger<MockTargetClient>.Instance);
        var orchestrator = new ArchB.Monolith.MetricOrchestrator(
            dataAccess, target, NullLogger<ArchB.Monolith.MetricOrchestrator>.Instance);

        var archLabel = failFast ? "Monolith (fail-fast)" : "Monolith";

        try
        {
            await orchestrator.RunAsync();
        }
        catch
        {
            // fail-fast mode: exception propagated, pipeline crashed
            return new ExperimentResult(archLabel, failingSource, ExpectedMetricsWhenFailing(failingSource), 0, 0, false);
        }

        var expected = ExpectedMetricsWhenFailing(failingSource);
        var actual = target.SentMetrics.Count;
        var pct = expected > 0 ? (double)actual / expected * 100 : 0;

        return new ExperimentResult(archLabel, failingSource, expected, actual, pct, actual > 0);
    }

    private async Task<ExperimentResult> RunHexagonalAsync(string failingSource)
    {
        var handler = new FakeHttpHandler(AllResponsesExcept(failingSource));
        var config = BuildConfig();
        var loggerFactory = NullLoggerFactory.Instance;

        var sources = new ArchA.Hexagonal.Core.IMetricSource[]
        {
            new ArchA.Hexagonal.Adapters.GitHubAdapter(config, loggerFactory, handler),
            new ArchA.Hexagonal.Adapters.StackOverflowAdapter(config, loggerFactory, handler),
            new ArchA.Hexagonal.Adapters.WorldBankAdapter(config, loggerFactory, handler),
            new ArchA.Hexagonal.Adapters.OpenMeteoAdapter(config, loggerFactory, handler),
        };

        var target = new MockTargetClient(NullLogger<MockTargetClient>.Instance);
        var sink = new ArchA.Hexagonal.Adapters.TargetSinkAdapter(target);
        var orchestrator = new ArchA.Hexagonal.Core.MetricOrchestrator(
            sources, sink, NullLogger<ArchA.Hexagonal.Core.MetricOrchestrator>.Instance);

        await orchestrator.RunAsync();

        var expected = ExpectedMetricsWhenFailing(failingSource);
        var actual = target.SentMetrics.Count;
        var pct = expected > 0 ? (double)actual / expected * 100 : 0;

        return new ExperimentResult("Hexagonal", failingSource, expected, actual, pct, actual > 0);
    }

    private async Task<ExperimentResult> RunMessagingAsync(string failingSource)
    {
        var handler = new FakeHttpHandler(AllResponsesExcept(failingSource));
        var config = BuildConfig();
        var loggerFactory = NullLoggerFactory.Instance;

        var github = new ArchC.Messaging.Producers.GitHubProducer(config, loggerFactory, handler);
        var stackoverflow = new ArchC.Messaging.Producers.StackOverflowProducer(config, loggerFactory, handler);
        var worldbank = new ArchC.Messaging.Producers.WorldBankProducer(config, loggerFactory, handler);
        var openmeteo = new ArchC.Messaging.Producers.OpenMeteoProducer(config, loggerFactory, handler);

        var target = new MockTargetClient(NullLogger<MockTargetClient>.Instance);
        var consumer = new ArchC.Messaging.MetricConsumer(target, NullLogger<ArchC.Messaging.MetricConsumer>.Instance);
        var orchestrator = new ArchC.Messaging.MessagingOrchestrator(
            github, stackoverflow, worldbank, openmeteo, consumer,
            NullLogger<ArchC.Messaging.MessagingOrchestrator>.Instance);

        await orchestrator.RunAsync();

        var expected = ExpectedMetricsWhenFailing(failingSource);
        var actual = target.SentMetrics.Count;
        var pct = expected > 0 ? (double)actual / expected * 100 : 0;

        return new ExperimentResult("Messaging", failingSource, expected, actual, pct, actual > 0);
    }

    public static void WriteCsv(List<ExperimentResult> results, string csvPath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(csvPath)!);

        using var writer = new StreamWriter(csvPath);
        writer.WriteLine("Architecture,FailingSource,ExpectedMetrics,ActualMetrics,Percentage,Continued");
        foreach (var r in results)
        {
            writer.WriteLine(string.Format(CultureInfo.InvariantCulture,
                "{0},{1},{2},{3},{4:F1},{5}",
                r.Architecture, r.FailingSource, r.ExpectedMetrics, r.ActualMetrics,
                r.Percentage, r.Continued));
        }
    }
}
