using Microsoft.Extensions.Logging;
using Shared.Models;

namespace ArchA.Hexagonal.Core;

/// <summary>
/// Core application logic -orchestrates metric collection and sending.
/// Knows only ports (IMetricSource, IMetricSink) and the domain model (Metric).
/// </summary>
public sealed class MetricOrchestrator
{
    private readonly IEnumerable<IMetricSource> _sources;
    private readonly IMetricSink _sink;
    private readonly ILogger<MetricOrchestrator> _logger;

    public MetricOrchestrator(IEnumerable<IMetricSource> sources, IMetricSink sink, ILogger<MetricOrchestrator> logger)
    {
        _sources = sources;
        _sink = sink;
        _logger = logger;
    }

    public async Task<bool> RunAsync(CancellationToken ct = default)
    {
        _logger.LogInformation("Orchestrator starting");

        var all = new List<Metric>();

        foreach (var source in _sources)
        {
            try
            {
                var metrics = await source.FetchAsync(ct);
                _logger.LogInformation("{Source}: {Count} metrics", source.Name, metrics.Count);
                all.AddRange(metrics);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "{Source} failed, skipping", source.Name);
            }
        }

        if (all.Count == 0)
        {
            _logger.LogWarning("No metrics collected from any source");
            return false;
        }

        var result = await _sink.SendAsync(all, ct);

        if (result.Success)
        {
            _logger.LogInformation("Orchestrator finished. Sent {Count} metrics", result.MetricsSent);
            return true;
        }

        _logger.LogError("Orchestrator failed to send metrics: {Error}", result.ErrorMessage);
        return false;
    }
}
