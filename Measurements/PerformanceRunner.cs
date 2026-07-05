using System.Diagnostics;
using System.Globalization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Shared.Target;
using Shared.Tests;

namespace Measurements;

public sealed class PerformanceRunner
{
    private static string MakeWorldBankResponse(double value)
    {
        var v = value.ToString("G", CultureInfo.InvariantCulture);
        return "[{\"page\":1,\"pages\":1,\"total\":1},[{\"value\":" + v + "}]]";
    }

    private readonly FakeHttpHandler _handler;
    private readonly IConfigurationRoot _config;

    public PerformanceRunner()
    {
        var responses = new Dictionary<string, string>
        {
            ["/user"] = """{"public_repos":5,"followers":10,"public_gists":2}""",
            ["/user/repos?per_page=100&type=owner"] = """[{"stargazers_count":3,"forks_count":1},{"stargazers_count":7,"forks_count":2}]""",
            ["/2.3/me?site=stackoverflow&key=test-key&access_token=test-token&filter=default"] =
                """{"items":[{"reputation":1234,"badge_counts":{"gold":1,"silver":5,"bronze":20}}]}""",
            ["/v2/country/SVN/indicator/NY.GDP.MKTP.CD?format=json&per_page=1&mrv=1"] = MakeWorldBankResponse(54000000000),
            ["/v2/country/SVN/indicator/SP.POP.TOTL?format=json&per_page=1&mrv=1"] = MakeWorldBankResponse(2100000),
            ["/v2/country/SVN/indicator/SP.DYN.LE00.IN?format=json&per_page=1&mrv=1"] = MakeWorldBankResponse(81.2),
            ["/v1/forecast?latitude=46.05&longitude=14.51&current=temperature_2m,relative_humidity_2m,wind_speed_10m"] =
                """{"current":{"temperature_2m":22.5,"relative_humidity_2m":65,"wind_speed_10m":12.3}}""",
        };

        _handler = new FakeHttpHandler(responses);

        _config = new ConfigurationBuilder()
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
    }

    public record Result(string Architecture, double MeanMs, double StdDevMs, int Iterations);

    public async Task<List<Result>> RunAllAsync(int iterations = 50)
    {
        // Warmup
        await RunMonolith();
        await RunHexagonal();
        await RunMessaging();

        var results = new List<Result>
        {
            await MeasureAsync("Monolith (Layered)", RunMonolith, iterations),
            await MeasureAsync("Hexagonal (Ports & Adapters)", RunHexagonal, iterations),
            await MeasureAsync("Messaging (Channel-based)", RunMessaging, iterations),
        };

        return results;
    }

    private async Task<Result> MeasureAsync(string name, Func<Task> action, int iterations)
    {
        var times = new double[iterations];
        var sw = new Stopwatch();

        for (int i = 0; i < iterations; i++)
        {
            sw.Restart();
            await action();
            sw.Stop();
            times[i] = sw.Elapsed.TotalMilliseconds;
        }

        var mean = times.Average();
        var stddev = Math.Sqrt(times.Select(t => (t - mean) * (t - mean)).Average());

        return new Result(name, Math.Round(mean, 3), Math.Round(stddev, 3), iterations);
    }

    private async Task RunMonolith()
    {
        var da = new ArchB.Monolith.DataAccess(_config, NullLoggerFactory.Instance, _handler);
        var target = new MockTargetClient(NullLogger<MockTargetClient>.Instance);
        var orch = new ArchB.Monolith.MetricOrchestrator(da, target, NullLogger<ArchB.Monolith.MetricOrchestrator>.Instance);
        await orch.RunAsync();
    }

    private async Task RunHexagonal()
    {
        var sources = new ArchA.Hexagonal.Core.IMetricSource[]
        {
            new ArchA.Hexagonal.Adapters.GitHubAdapter(_config, NullLoggerFactory.Instance, _handler),
            new ArchA.Hexagonal.Adapters.StackOverflowAdapter(_config, NullLoggerFactory.Instance, _handler),
            new ArchA.Hexagonal.Adapters.WorldBankAdapter(_config, NullLoggerFactory.Instance, _handler),
            new ArchA.Hexagonal.Adapters.OpenMeteoAdapter(_config, NullLoggerFactory.Instance, _handler),
        };
        var target = new MockTargetClient(NullLogger<MockTargetClient>.Instance);
        var sink = new ArchA.Hexagonal.Adapters.TargetSinkAdapter(target);
        var orch = new ArchA.Hexagonal.Core.MetricOrchestrator(sources, sink, NullLogger<ArchA.Hexagonal.Core.MetricOrchestrator>.Instance);
        await orch.RunAsync();
    }

    private async Task RunMessaging()
    {
        var gh = new ArchC.Messaging.Producers.GitHubProducer(_config, NullLoggerFactory.Instance, _handler);
        var so = new ArchC.Messaging.Producers.StackOverflowProducer(_config, NullLoggerFactory.Instance, _handler);
        var wb = new ArchC.Messaging.Producers.WorldBankProducer(_config, NullLoggerFactory.Instance, _handler);
        var om = new ArchC.Messaging.Producers.OpenMeteoProducer(_config, NullLoggerFactory.Instance, _handler);
        var target = new MockTargetClient(NullLogger<MockTargetClient>.Instance);
        var consumer = new ArchC.Messaging.MetricConsumer(target, NullLogger<ArchC.Messaging.MetricConsumer>.Instance);
        var orch = new ArchC.Messaging.MessagingOrchestrator(gh, so, wb, om, consumer, NullLogger<ArchC.Messaging.MessagingOrchestrator>.Instance);
        await orch.RunAsync();
    }

    public static void WriteCsv(List<Result> results, string csvPath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(csvPath)!);

        using var writer = new StreamWriter(csvPath);
        writer.WriteLine("Architecture,MeanMs,StdDevMs,Iterations");
        foreach (var r in results)
        {
            writer.WriteLine(string.Format(CultureInfo.InvariantCulture,
                "{0},{1},{2},{3}",
                r.Architecture, r.MeanMs, r.StdDevMs, r.Iterations));
        }
    }
}
