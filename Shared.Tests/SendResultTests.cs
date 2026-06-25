using Shared.Models;

namespace Shared.Tests;

public class SendResultTests
{
    [Fact]
    public void SendResult_Success()
    {
        var result = new SendResult(true, 5);

        Assert.True(result.Success);
        Assert.Equal(5, result.MetricsSent);
        Assert.Null(result.ErrorMessage);
    }

    [Fact]
    public void SendResult_Failure()
    {
        var result = new SendResult(false, 0, "connection refused");

        Assert.False(result.Success);
        Assert.Equal(0, result.MetricsSent);
        Assert.Equal("connection refused", result.ErrorMessage);
    }
}
