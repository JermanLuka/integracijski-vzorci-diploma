using Shared.Models;

namespace ArchA.Hexagonal.Core;

/// <summary>
/// Input port -represents a source of metrics.
/// </summary>
public interface IMetricSource
{
    string Name { get; }
    Task<IReadOnlyList<Metric>> FetchAsync(CancellationToken ct = default);
}
