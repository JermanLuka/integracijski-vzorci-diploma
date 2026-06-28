using ArchA.Hexagonal.Core;
using Shared.Models;
using Shared.Target;

namespace ArchA.Hexagonal.Adapters;

/// <summary>
/// Adapter that wraps ITargetClient behind the IMetricSink port.
/// </summary>
public sealed class TargetSinkAdapter : IMetricSink
{
    private readonly ITargetClient _client;

    public TargetSinkAdapter(ITargetClient client)
    {
        _client = client;
    }

    public Task<SendResult> SendAsync(IEnumerable<Metric> metrics, CancellationToken ct = default)
        => _client.SendAsync(metrics, ct);
}
