using Shared.Models;

namespace Shared.Tests;

public class MetricTests
{
    [Fact]
    public void Metric_StoresAllProperties()
    {
        var tags = new Dictionary<string, string> { ["env"] = "test" };
        var ts = DateTimeOffset.UtcNow;

        var metric = new Metric("cpu.usage", 42.5, ts, tags);

        Assert.Equal("cpu.usage", metric.Name);
        Assert.Equal(42.5, metric.Value);
        Assert.Equal(ts, metric.Timestamp);
        Assert.NotNull(metric.Tags);
        Assert.Equal("test", metric.Tags!["env"]);
    }

    [Fact]
    public void Metric_TagsDefaultToNull()
    {
        var metric = new Metric("mem.free", 1024, DateTimeOffset.UtcNow);

        Assert.Null(metric.Tags);
    }

    [Fact]
    public void Metric_EqualityByValue()
    {
        var ts = DateTimeOffset.UtcNow;
        var a = new Metric("x", 1, ts);
        var b = new Metric("x", 1, ts);

        Assert.Equal(a, b);
    }

    [Fact]
    public void Metric_WithEqualButDistinctTagInstances_AreNotEqual()
    {
        var ts = DateTimeOffset.UtcNow;
        var a = new Metric("x", 1, ts, new Dictionary<string, string> { ["env"] = "test" });
        var b = new Metric("x", 1, ts, new Dictionary<string, string> { ["env"] = "test" });

        Assert.NotEqual(a, b);
    }
}
