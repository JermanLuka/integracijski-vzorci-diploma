# Integration Patterns - Architecture Comparison

A diploma project that compares three software architectures for an integration service. The service collects metrics from multiple public APIs and forwards them to a target monitoring system. The same task is implemented three times using different architectural approaches, then measured across four quality metrics.

## Architectures

- **Monolith (Layered)** - Simple sequential pipeline with no abstractions. Serves as the baseline.
- **Hexagonal (Ports & Adapters)** - Core logic separated from external dependencies through interfaces. Emphasis on extensibility.
- **Messaging (Channel-based)** - Fetching and sending decoupled via in-process message channels (`System.Threading.Channels`). Emphasis on fault tolerance.

All three architectures share the same `Shared` module (domain model, data sources, target client) and differ only in how they compose these building blocks.

## Data Sources

| Source | Auth |
|---|---|
| GitHub | API token |
| StackOverflow | API key |
| World Bank | None |
| OpenMeteo | None (added as 4th source for extensibility measurement) |

## Hypotheses

- **H1**: Hexagonal architecture requires fewer changes to existing code when adding a new source than the monolith.
- **H2**: Messaging architecture delivers a higher percentage of metrics when a source fails than the synchronous monolith.
- **H3**: Greater architectural abstraction increases code complexity and processing time compared to the monolith.

## Project Structure

```
Shared/                     Shared domain model, data sources, target client
Shared.Tests/               Unit tests for shared components

ArchA.Hexagonal/            Hexagonal architecture (Ports & Adapters)
ArchA.Hexagonal.Tests/      Tests for hexagonal architecture

ArchB.Monolith/             Layered monolith architecture
ArchB.Monolith.Tests/       Tests for monolith architecture

ArchC.Messaging/            Message-based architecture (Channels)
ArchC.Messaging.Tests/      Tests for messaging architecture

Measurements/               Performance and fault tolerance runners
results/                    CSV output from measurements
```

## Measurements

All measurements run against `FakeHttpHandler` and `MockTargetClient` for deterministic, network-free results.

### Extensibility

Computed by `Measurements/measure-extensibility.ps1` from the git diff of the commit that added the 4th data source (OpenMeteo). For each project it reports files modified, new files, lines added in new files vs. lines changed in existing files, and whether core logic was changed. Files whose only change is a comment are excluded; core files are an explicit list per architecture (Monolith: `DataAccess` + orchestrator, Hexagonal: `Core/`, Messaging: `MetricConsumer` + `FetchRetryPolicy`).

### Code Complexity

Computed using Visual Studio Code Metrics (View > Other Windows > Code Metrics). Records maintainability index, cyclomatic complexity, class coupling, and lines of code per architecture project.

### Performance

`PerformanceRunner.cs` uses `Stopwatch` to measure each architecture over 500 iterations. Pipeline construction happens outside the timed region, so only the execution of `RunAsync` is measured. Reported statistics are mean, standard deviation, median, and 95th percentile in milliseconds. Because sources are faked in memory, the numbers reflect pure architectural overhead rather than real network behavior. The monolith fetches sequentially while messaging producers run concurrently, so relative results would differ under real network latency.

### Fault Tolerance

`FaultToleranceRunner.cs` measures the percentage of metrics successfully delivered under two scenarios.

**Scenario A (permanent outage):** one source is down for the entire run. Every implementation that catches errors per source delivers all remaining metrics, so this scenario does not differentiate the architectures.

**Scenario B (transient outage):** one source rejects its first request and then recovers. Only the messaging implementation retries fetches (`FetchRetryPolicy`, up to 3 attempts with exponential backoff) and delivers 100%. The monolith and hexagonal implementations fetch once and lose the metrics of that source for the cycle. This asymmetry is deliberate: the retry mechanism is the measured difference (see H2).

The monolith is additionally tested in a mode where exceptions propagate and the run crashes, labeled "Monolith (fail-fast)" in the results.

Results from both scenarios and the other metrics are aggregated into `results/summary.csv`.

## Running

```bash
# Build
dotnet build

# Run tests
dotnet test

# Run measurements (outputs CSVs to results/)
dotnet run --project Measurements
```

## Tech Stack

- C# / .NET 10
- xUnit (testing)
- System.Threading.Channels (messaging architecture)
