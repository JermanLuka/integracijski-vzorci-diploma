using Shared.Models;

namespace ArchA.Hexagonal.Core;

/// <summary>
/// Output port -represents a target for sending metrics.
/// </summary>
public interface IMetricSink
{
    Task<SendResult> SendAsync(IEnumerable<Metric> metrics, CancellationToken ct = default);
}
