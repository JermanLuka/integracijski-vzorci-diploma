using System.Diagnostics;
using System.Globalization;
using Microsoft.Extensions.Configuration;
using Shared.Tests;

namespace Measurements;

public sealed class PerformanceRunner
{
    private readonly FakeHttpHandler _handler = new(TestData.AllResponses());
    private readonly IConfigurationRoot _config = TestData.Config();

    public record Result(string Architecture, double MeanMs, double StdDevMs, int Iterations);

    public async Task<List<Result>> RunAllAsync(int iterations = 500)
    {
        // Warmup: run each architecture several times first, so the runtime has
        // finished compiling and optimizing the code before any timing starts
        const int warmupIterations = 10;
        for (int i = 0; i < warmupIterations; i++)
        {
            await RunMonolith();
            await RunHexagonal();
            await RunMessaging();
        }

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
        // Re-warm this specific action and level the GC state so the first
        // measured block does not absorb leftover work from the previous one
        for (int i = 0; i < 10; i++)
            await action();
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

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
        var (orchestrator, _) = ArchitectureFactory.BuildMonolith(_config, _handler);
        await orchestrator.RunAsync();
    }

    private async Task RunHexagonal()
    {
        var (orchestrator, _) = ArchitectureFactory.BuildHexagonal(_config, _handler);
        await orchestrator.RunAsync();
    }

    private async Task RunMessaging()
    {
        var (orchestrator, _) = ArchitectureFactory.BuildMessaging(_config, _handler);
        await orchestrator.RunAsync();
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
