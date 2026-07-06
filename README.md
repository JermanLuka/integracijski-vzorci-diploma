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

Measured by git diff when adding the 4th data source (OpenMeteo). Tracks files modified, new files, lines added, and whether core logic was changed.

### Code Complexity

Computed using Visual Studio Code Metrics (View > Other Windows > Code Metrics). Records maintainability index, cyclomatic complexity, class coupling, and lines of code per architecture project.

### Performance

`PerformanceRunner.cs` uses `Stopwatch` to measure each architecture over 50 iterations, computing mean and standard deviation in milliseconds.

### Fault Tolerance

`FaultToleranceRunner.cs` simulates one source failing at a time and measures the percentage of metrics successfully delivered. The monolith is tested in two modes: graceful (exceptions caught) and fail-fast (exceptions propagate).

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
