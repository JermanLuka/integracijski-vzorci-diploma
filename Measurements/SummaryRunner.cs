using System.Globalization;

namespace Measurements;

public static class SummaryRunner
{
    public record ArchitectureSummary(
        string Architecture,
        int ExtFilesModified,
        int ExtNewFiles,
        int ExtLinesInNewFiles,
        int ExtLinesInExistingFiles,
        string ExtCoreChanged,
        int CxMaintainability,
        int CxCyclomaticComplexity,
        int CxClassCoupling,
        int CxLinesOfCode,
        string PerfMeanMs,
        string PerfStdDevMs,
        string PerfMedianMs,
        double FtPermanentAvgPct,
        double FtTransientAvgPct);

    public static void Run(string resultsDir)
    {
        var extensibility = ReadExtensibility(Path.Combine(resultsDir, "extensibility.csv"));
        var complexity = ReadComplexity(Path.Combine(resultsDir, "complexity.csv"));
        var performance = ReadPerformance(Path.Combine(resultsDir, "performance.csv"));
        var ftPermanent = ReadFaultTolerance(Path.Combine(resultsDir, "fault-tolerance.csv"), hasScenarioColumn: false);
        var ftTransient = ReadFaultTolerance(Path.Combine(resultsDir, "fault-tolerance-transient.csv"), hasScenarioColumn: true);

        var archNames = new[] { "Monolith", "Hexagonal", "Messaging" };
        var summaries = new List<ArchitectureSummary>();

        foreach (var arch in archNames)
        {
            var ext = extensibility.FirstOrDefault(e => MatchesArchitecture(e.Arch, arch));
            var cx = complexity.FirstOrDefault(c => MatchesArchitecture(c.Arch, arch));
            var perf = performance.FirstOrDefault(p => MatchesArchitecture(p.Arch, arch));
            var ftPermRows = ftPermanent.Where(f => f.Arch == arch).ToList();
            var ftTransRows = ftTransient.Where(f => f.Arch == arch).ToList();

            summaries.Add(new ArchitectureSummary(
                arch,
                ext?.FilesModified ?? 0, ext?.NewFiles ?? 0,
                ext?.LinesInNewFiles ?? 0, ext?.LinesInExistingFiles ?? 0, ext?.CoreChanged ?? "N/A",
                cx?.Maintainability ?? 0, cx?.CyclomaticComplexity ?? 0, cx?.ClassCoupling ?? 0, cx?.LinesOfCode ?? 0,
                perf?.MeanMs ?? "N/A", perf?.StdDevMs ?? "N/A", perf?.MedianMs ?? "N/A",
                ftPermRows.Count > 0 ? ftPermRows.Average(f => f.Percentage) : 0,
                ftTransRows.Count > 0 ? ftTransRows.Average(f => f.Percentage) : 0));
        }

        var csvPath = Path.Combine(resultsDir, "summary.csv");
        using var writer = new StreamWriter(csvPath);
        writer.WriteLine(
            "Architecture,Ext_FilesModified,Ext_NewFiles,Ext_LinesInNewFiles,Ext_LinesInExistingFiles,Ext_CoreChanged," +
            "Cx_Maintainability,Cx_CyclomaticComplexity,Cx_ClassCoupling,Cx_LinesOfCode," +
            "Perf_MeanMs,Perf_StdDevMs,Perf_MedianMs,FT_PermanentAvgPct,FT_TransientAvgPct");
        foreach (var s in summaries)
        {
            writer.WriteLine(string.Format(CultureInfo.InvariantCulture,
                "{0},{1},{2},{3},{4},{5},{6},{7},{8},{9},{10},{11},{12},{13:F1},{14:F1}",
                s.Architecture, s.ExtFilesModified, s.ExtNewFiles, s.ExtLinesInNewFiles, s.ExtLinesInExistingFiles,
                s.ExtCoreChanged, s.CxMaintainability, s.CxCyclomaticComplexity, s.CxClassCoupling, s.CxLinesOfCode,
                s.PerfMeanMs, s.PerfStdDevMs, s.PerfMedianMs, s.FtPermanentAvgPct, s.FtTransientAvgPct));
        }
    }

    private static bool MatchesArchitecture(string label, string arch) =>
        label.Equals(arch, StringComparison.OrdinalIgnoreCase) ||
        label.StartsWith(arch + " (", StringComparison.OrdinalIgnoreCase);

    private record ExtRow(string Arch, int FilesModified, int NewFiles, int LinesInNewFiles, int LinesInExistingFiles, string CoreChanged);

    private static List<ExtRow> ReadExtensibility(string path)
    {
        if (!File.Exists(path)) return [];
        return File.ReadAllLines(path)
            .Skip(1)
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .Select(l =>
            {
                var parts = l.Split(',');
                return new ExtRow(
                    parts[0].Trim(),
                    ParseInt(parts, 1),
                    ParseInt(parts, 2),
                    ParseInt(parts, 3),
                    ParseInt(parts, 4),
                    parts.Length > 5 ? parts[5].Trim() : "N/A");
            })
            .ToList();
    }

    private record CxRow(string Arch, int Maintainability, int CyclomaticComplexity, int ClassCoupling, int LinesOfCode);

    private static List<CxRow> ReadComplexity(string path)
    {
        if (!File.Exists(path)) return [];
        return File.ReadAllLines(path)
            .Skip(1)
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .Select(l =>
            {
                var parts = l.Split(',');
                return new CxRow(
                    parts[0].Trim(),
                    ParseInt(parts, 1),
                    ParseInt(parts, 2),
                    ParseInt(parts, 3),
                    ParseInt(parts, 4));
            })
            .ToList();
    }

    private record PerfRow(string Arch, string MeanMs, string StdDevMs, string MedianMs);

    private static List<PerfRow> ReadPerformance(string path)
    {
        if (!File.Exists(path)) return [];
        return File.ReadAllLines(path)
            .Skip(1)
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .Select(l =>
            {
                var parts = l.Split(',');
                return new PerfRow(
                    parts[0].Trim(),
                    parts[1].Trim(),
                    parts[2].Trim(),
                    parts.Length > 3 ? parts[3].Trim() : "N/A");
            })
            .ToList();
    }

    private record FtRow(string Arch, double Percentage);

    private static List<FtRow> ReadFaultTolerance(string path, bool hasScenarioColumn)
    {
        if (!File.Exists(path)) return [];
        var offset = hasScenarioColumn ? 1 : 0;
        return File.ReadAllLines(path)
            .Skip(1)
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .Select(l =>
            {
                var parts = l.Split(',');
                return new FtRow(
                    parts[offset].Trim(),
                    double.TryParse(parts[offset + 4].Trim(), CultureInfo.InvariantCulture, out var pct) ? pct : 0);
            })
            .ToList();
    }

    private static int ParseInt(string[] parts, int index) =>
        parts.Length > index && int.TryParse(parts[index].Trim(), out var value) ? value : 0;
}
