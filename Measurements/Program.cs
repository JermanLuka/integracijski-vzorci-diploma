using System.Globalization;
using Measurements;

CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.InvariantCulture;

var resultsDir = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "results");
Directory.CreateDirectory(resultsDir);

var perfRunner = new PerformanceRunner();
var perfResults = await perfRunner.RunAllAsync(iterations: 50);
PerformanceRunner.WriteCsv(perfResults, Path.Combine(resultsDir, "performance.csv"));

var ftRunner = new FaultToleranceRunner();
var ftResults = await ftRunner.RunAllAsync();
FaultToleranceRunner.WriteCsv(ftResults, Path.Combine(resultsDir, "fault-tolerance.csv"));

var ftTransientResults = await ftRunner.RunAllTransientAsync();
FaultToleranceRunner.WriteTransientCsv(ftTransientResults, Path.Combine(resultsDir, "fault-tolerance-transient.csv"));

SummaryRunner.Run(resultsDir);
