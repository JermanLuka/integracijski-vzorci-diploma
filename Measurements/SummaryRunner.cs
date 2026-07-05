using System.Globalization;

namespace Measurements;

public static class SummaryRunner
{
    public record ArchitectureSummary(
        string Architecture,
        int ExtFilesModified,
        int ExtNewFiles,
        int ExtLinesAdded,
        string ExtCoreChanged,
        int CxMaintainability,
        int CxCyclomaticComplexity,
        int CxClassCoupling,
        int CxLinesOfCode,
        string PerfMeanMs,
        string PerfStdDevMs,
        double FtAvgDeliveryPct);

    public static void Run(string resultsDir)
    {
        var extensibility = ReadExtensibility(Path.Combine(resultsDir, "extensibility.csv"));
        var complexity = ReadComplexity(Path.Combine(resultsDir, "complexity.csv"));
        var performance = ReadPerformance(Path.Combine(resultsDir, "performance.csv"));
        var faultTolerance = ReadFaultTolerance(Path.Combine(resultsDir, "fault-tolerance.csv"));

        var archNames = new[] { "Monolith", "Hexagonal", "Messaging" };
        var summaries = new List<ArchitectureSummary>();

        foreach (var arch in archNames)
        {
            var ext = extensibility.FirstOrDefault(e => e.Arch.StartsWith(arch, StringComparison.OrdinalIgnoreCase));
            var cx = complexity.FirstOrDefault(c => c.Arch.StartsWith(arch, StringComparison.OrdinalIgnoreCase));
            var perf = performance.FirstOrDefault(p => p.Arch.StartsWith(arch, StringComparison.OrdinalIgnoreCase));
            var ftRows = faultTolerance.Where(f => f.Arch == arch).ToList();
            var ftAvg = ftRows.Count > 0 ? ftRows.Average(f => f.Percentage) : 0;

            summaries.Add(new ArchitectureSummary(
                arch,
                ext?.FilesModified ?? 0, ext?.NewFiles ?? 0, ext?.LinesAdded ?? 0, ext?.CoreChanged ?? "N/A",
                cx?.Maintainability ?? 0, cx?.CyclomaticComplexity ?? 0, cx?.ClassCoupling ?? 0, cx?.LinesOfCode ?? 0,
                perf?.MeanMs ?? "N/A", perf?.StdDevMs ?? "N/A",
                ftAvg));
        }

        var csvPath = Path.Combine(resultsDir, "summary.csv");
        using (var writer = new StreamWriter(csvPath))
        {
            writer.WriteLine("Architecture,Ext_FilesModified,Ext_NewFiles,Ext_LinesAdded,Ext_CoreChanged,Cx_Maintainability,Cx_CyclomaticComplexity,Cx_ClassCoupling,Cx_LinesOfCode,Perf_MeanMs,Perf_StdDevMs,FT_AvgDeliveryPct");
            foreach (var s in summaries)
            {
                writer.WriteLine(string.Format(CultureInfo.InvariantCulture,
                    "{0},{1},{2},{3},{4},{5},{6},{7},{8},{9},{10},{11:F1}",
                    s.Architecture, s.ExtFilesModified, s.ExtNewFiles, s.ExtLinesAdded, s.ExtCoreChanged,
                    s.CxMaintainability, s.CxCyclomaticComplexity, s.CxClassCoupling, s.CxLinesOfCode,
                    s.PerfMeanMs, s.PerfStdDevMs, s.FtAvgDeliveryPct));
            }
        }

    }

    private record ExtRow(string Arch, int FilesModified, int NewFiles, int LinesAdded, string CoreChanged);

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
                    int.TryParse(parts[1].Trim(), out var fm) ? fm : 0,
                    int.TryParse(parts[2].Trim(), out var nf) ? nf : 0,
                    int.TryParse(parts[3].Trim(), out var la) ? la : 0,
                    parts.Length > 4 ? parts[4].Trim() : "N/A");
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
                    int.TryParse(parts[1].Trim(), out var mi) ? mi : 0,
                    int.TryParse(parts[2].Trim(), out var cc) ? cc : 0,
                    int.TryParse(parts[3].Trim(), out var cp) ? cp : 0,
                    int.TryParse(parts[4].Trim(), out var loc) ? loc : 0);
            })
            .ToList();
    }

    private record PerfRow(string Arch, string MeanMs, string StdDevMs);

    private static List<PerfRow> ReadPerformance(string path)
    {
        if (!File.Exists(path)) return [];
        return File.ReadAllLines(path)
            .Skip(1)
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .Select(l =>
            {
                var parts = l.Split(',');
                return new PerfRow(parts[0].Trim(), parts[1].Trim(), parts[2].Trim());
            })
            .ToList();
    }

    private record FtRow(string Arch, double Percentage);

    private static List<FtRow> ReadFaultTolerance(string path)
    {
        if (!File.Exists(path)) return [];
        return File.ReadAllLines(path)
            .Skip(1)
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .Select(l =>
            {
                var parts = l.Split(',');
                return new FtRow(
                    parts[0].Trim(),
                    double.TryParse(parts[4].Trim(), CultureInfo.InvariantCulture, out var pct) ? pct : 0);
            })
            .ToList();
    }
}
