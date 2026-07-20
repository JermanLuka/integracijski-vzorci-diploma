using System.Diagnostics;
using System.Globalization;
using Microsoft.Extensions.Configuration;
using Shared.Tests;

namespace Measurements;

public sealed class PerformanceRunner
{
    private readonly FakeHttpHandler _handler = new(TestData.AllResponses());
    private readonly IConfigurationRoot _config = TestData.Config();

    public record Result(
        string Architecture,
        double MeanMs,
        double StdDevMs,
        double MedianMs,
        double P95Ms,
        int Iterations);

    public async Task<List<Result>> RunAllAsync(int iterations = 500)
    {
        var pipelines = new (string Name, Func<Func<Task>> Prepare)[]
        {
            ("Monolith (Layered)", PrepareMonolith),
            ("Hexagonal (Ports & Adapters)", PrepareHexagonal),
            ("Messaging (Channel-based)", PrepareMessaging),
        };

        const int warmupIterations = 10;
        for (int i = 0; i < warmupIterations; i++)
            foreach (var (_, prepare) in pipelines)
                await prepare()();

        var results = new List<Result>();
        foreach (var (name, prepare) in pipelines)
            results.Add(await MeasureAsync(name, prepare, iterations));

        return results;
    }

    private static async Task<Result> MeasureAsync(string name, Func<Func<Task>> prepare, int iterations)
    {
        for (int i = 0; i < 10; i++)
            await prepare()();
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        var times = new double[iterations];
        var sw = new Stopwatch();

        for (int i = 0; i < iterations; i++)
        {
            var run = prepare();
            sw.Restart();
            await run();
            sw.Stop();
            times[i] = sw.Elapsed.TotalMilliseconds;
        }

        var mean = times.Average();
        var stddev = Math.Sqrt(times.Select(t => (t - mean) * (t - mean)).Average());

        var sorted = (double[])times.Clone();
        Array.Sort(sorted);
        var median = Percentile(sorted, 50);
        var p95 = Percentile(sorted, 95);

        return new Result(name, Math.Round(mean, 3), Math.Round(stddev, 3),
            Math.Round(median, 3), Math.Round(p95, 3), iterations);
    }

    private static double Percentile(double[] sorted, double percentile)
    {
        var rank = percentile / 100.0 * (sorted.Length - 1);
        var lower = (int)Math.Floor(rank);
        var upper = (int)Math.Ceiling(rank);
        return sorted[lower] + (sorted[upper] - sorted[lower]) * (rank - lower);
    }

    private Func<Task> PrepareMonolith()
    {
        var (orchestrator, _) = ArchitectureFactory.BuildMonolith(_config, _handler);
        return () => orchestrator.RunAsync();
    }

    private Func<Task> PrepareHexagonal()
    {
        var (orchestrator, _) = ArchitectureFactory.BuildHexagonal(_config, _handler);
        return () => orchestrator.RunAsync();
    }

    private Func<Task> PrepareMessaging()
    {
        var (orchestrator, _) = ArchitectureFactory.BuildMessaging(_config, _handler);
        return () => orchestrator.RunAsync();
    }

    public static void WriteCsv(List<Result> results, string csvPath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(csvPath)!);

        using var writer = new StreamWriter(csvPath);
        writer.WriteLine("Architecture,MeanMs,StdDevMs,MedianMs,P95Ms,Iterations");
        foreach (var r in results)
        {
            writer.WriteLine(string.Format(CultureInfo.InvariantCulture,
                "{0},{1},{2},{3},{4},{5}",
                r.Architecture, r.MeanMs, r.StdDevMs, r.MedianMs, r.P95Ms, r.Iterations));
        }
    }
}
