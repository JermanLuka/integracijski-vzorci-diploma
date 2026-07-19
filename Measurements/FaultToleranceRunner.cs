using System.Globalization;
using Shared.Tests;

namespace Measurements;

public sealed class FaultToleranceRunner
{
    // Scenario A: the failing source is permanently down (its endpoints have no responses at all)
    private static FakeHttpHandler PermanentOutageHandler(string failingSource) =>
        new(TestData.ResponsesExcept(failingSource));

    // Scenario B: the failing source rejects its first incoming request, then is recovered.
    // Sources fetch their endpoints sequentially, so a transient failure on the first
    // endpoint means the source is back up by the time the request is retried.
    private static FakeHttpHandler TransientOutageHandler(string failingSource) =>
        new(TestData.AllResponses(), new Dictionary<string, int> { [TestData.FirstEndpoint(failingSource)] = 1 });

    public record ExperimentResult(
        string Scenario,
        string Architecture,
        string FailingSource,
        int ExpectedMetrics,
        int ActualMetrics,
        double Percentage,
        bool Continued);

    public async Task<List<ExperimentResult>> RunAllAsync()
    {
        var results = new List<ExperimentResult>();

        foreach (var failingSource in TestData.SourceNames)
        {
            var expected = TestData.TotalMetrics - TestData.MetricCountBySource[failingSource];
            results.Add(await RunMonolithAsync("A", failingSource, PermanentOutageHandler(failingSource), expected, failFast: false));
            results.Add(await RunMonolithAsync("A", failingSource, PermanentOutageHandler(failingSource), expected, failFast: true));
            results.Add(await RunHexagonalAsync("A", failingSource, PermanentOutageHandler(failingSource), expected));
            results.Add(await RunMessagingAsync("A", failingSource, PermanentOutageHandler(failingSource), expected));
        }

        return results;
    }

    public async Task<List<ExperimentResult>> RunAllTransientAsync()
    {
        var results = new List<ExperimentResult>();

        foreach (var failingSource in TestData.SourceNames)
        {
            // The source recovers, so a resilient implementation should deliver all metrics
            results.Add(await RunMonolithAsync("B", failingSource, TransientOutageHandler(failingSource), TestData.TotalMetrics, failFast: false));
            results.Add(await RunMonolithAsync("B", failingSource, TransientOutageHandler(failingSource), TestData.TotalMetrics, failFast: true));
            results.Add(await RunHexagonalAsync("B", failingSource, TransientOutageHandler(failingSource), TestData.TotalMetrics));
            results.Add(await RunMessagingAsync("B", failingSource, TransientOutageHandler(failingSource), TestData.TotalMetrics));
        }

        return results;
    }

    private static ExperimentResult MakeResult(
        string scenario, string architecture, string failingSource, int expected, int actual)
    {
        var pct = expected > 0 ? (double)actual / expected * 100 : 0;
        return new ExperimentResult(scenario, architecture, failingSource, expected, actual, pct, actual > 0);
    }

    private async Task<ExperimentResult> RunMonolithAsync(
        string scenario, string failingSource, FakeHttpHandler handler, int expected, bool failFast)
    {
        var (orchestrator, target) = ArchitectureFactory.BuildMonolith(TestData.Config(), handler, failFast);
        var archLabel = failFast ? "Monolith (fail-fast)" : "Monolith";

        try
        {
            await orchestrator.RunAsync();
        }
        catch
        {
            // fail-fast mode: exception propagated, pipeline crashed
            return new ExperimentResult(scenario, archLabel, failingSource, expected, 0, 0, false);
        }

        return MakeResult(scenario, archLabel, failingSource, expected, target.SentMetrics.Count);
    }

    private async Task<ExperimentResult> RunHexagonalAsync(
        string scenario, string failingSource, FakeHttpHandler handler, int expected)
    {
        var (orchestrator, target) = ArchitectureFactory.BuildHexagonal(TestData.Config(), handler);

        await orchestrator.RunAsync();

        return MakeResult(scenario, "Hexagonal", failingSource, expected, target.SentMetrics.Count);
    }

    private async Task<ExperimentResult> RunMessagingAsync(
        string scenario, string failingSource, FakeHttpHandler handler, int expected)
    {
        var (orchestrator, target) = ArchitectureFactory.BuildMessaging(TestData.Config(), handler);

        await orchestrator.RunAsync();

        return MakeResult(scenario, "Messaging", failingSource, expected, target.SentMetrics.Count);
    }

    public static void WriteCsv(List<ExperimentResult> results, string csvPath) =>
        WriteCsv(results, csvPath, includeScenario: false);

    public static void WriteTransientCsv(List<ExperimentResult> results, string csvPath) =>
        WriteCsv(results, csvPath, includeScenario: true);

    private static void WriteCsv(List<ExperimentResult> results, string csvPath, bool includeScenario)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(csvPath)!);

        using var writer = new StreamWriter(csvPath);
        writer.WriteLine(includeScenario
            ? "Scenario,Architecture,FailingSource,ExpectedMetrics,ActualMetrics,Percentage,Continued"
            : "Architecture,FailingSource,ExpectedMetrics,ActualMetrics,Percentage,Continued");
        foreach (var r in results)
        {
            var prefix = includeScenario ? r.Scenario + "," : "";
            writer.WriteLine(prefix + string.Format(CultureInfo.InvariantCulture,
                "{0},{1},{2},{3},{4:F1},{5}",
                r.Architecture, r.FailingSource, r.ExpectedMetrics, r.ActualMetrics,
                r.Percentage, r.Continued));
        }
    }
}
