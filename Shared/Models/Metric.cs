namespace Shared.Models;

/// <summary>
/// Immutable representation of a single metric data point.
/// </summary>
public sealed record Metric(
    string Name,
    double Value,
    DateTimeOffset Timestamp,
    IReadOnlyDictionary<string, string>? Tags = null);
