using Microsoft.Extensions.Logging;
using Shared.Models;
using Shared.Target;

namespace ArchB.Monolith;

/// <summary>
/// Application layer - fetch metrics from each source, send to target, log result.
/// </summary>
public sealed class MetricOrchestrator
{
    private readonly DataAccess _dataAccess;
    private readonly ITargetClient _target;
    private readonly ILogger<MetricOrchestrator> _logger;

    public MetricOrchestrator(DataAccess dataAccess, ITargetClient target, ILogger<MetricOrchestrator> logger)
    {
        _dataAccess = dataAccess;
        _target = target;
        _logger = logger;
    }

    public async Task<bool> RunAsync(CancellationToken ct = default)
    {
        _logger.LogInformation("Orchestrator starting");

        var all = new List<Metric>();

        var github = await _dataAccess.FetchGitHubAsync(ct);
        _logger.LogInformation("GitHub: {Count} metrics", github.Count);
        all.AddRange(github);

        var stackoverflow = await _dataAccess.FetchStackOverflowAsync(ct);
        _logger.LogInformation("StackOverflow: {Count} metrics", stackoverflow.Count);
        all.AddRange(stackoverflow);

        var worldbank = await _dataAccess.FetchWorldBankAsync(ct);
        _logger.LogInformation("WorldBank: {Count} metrics", worldbank.Count);
        all.AddRange(worldbank);

        var openmeteo = await _dataAccess.FetchOpenMeteoAsync(ct);
        _logger.LogInformation("OpenMeteo: {Count} metrics", openmeteo.Count);
        all.AddRange(openmeteo);

        if (all.Count == 0)
        {
            _logger.LogWarning("No metrics collected from any source");
            return false;
        }

        var result = await _target.SendAsync(all, ct);

        if (result.Success)
        {
            _logger.LogInformation("Orchestrator finished. Sent {Count} metrics", result.MetricsSent);
            return true;
        }

        _logger.LogError("Orchestrator failed to send metrics: {Error}", result.ErrorMessage);
        return false;
    }
}
