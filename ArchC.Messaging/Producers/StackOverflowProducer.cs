using System.Threading.Channels;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Shared.Models;
using Shared.Sources;

namespace ArchC.Messaging.Producers;

public sealed class StackOverflowProducer
{
    private readonly IConfiguration _config;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<StackOverflowProducer> _logger;
    private readonly HttpMessageHandler? _handler;

    public StackOverflowProducer(IConfiguration config, ILoggerFactory loggerFactory, HttpMessageHandler? handler = null)
    {
        _config = config;
        _loggerFactory = loggerFactory;
        _logger = loggerFactory.CreateLogger<StackOverflowProducer>();
        _handler = handler;
    }

    public async Task ProduceAsync(ChannelWriter<Metric> writer, CancellationToken ct = default)
    {
        var accessToken = _config["StackOverflow:AccessToken"];
        var apiKey = _config["StackOverflow:ApiKey"];
        if (string.IsNullOrWhiteSpace(accessToken) || string.IsNullOrWhiteSpace(apiKey))
        {
            _logger.LogWarning("StackOverflow credentials not configured, skipping");
            return;
        }

        try
        {
            var http = _handler is not null ? new HttpClient(_handler, disposeHandler: false) : new HttpClient();
            var source = new StackOverflowSource(http, accessToken, apiKey, _loggerFactory.CreateLogger<StackOverflowSource>());
            var metrics = await FetchRetryPolicy.FetchAsync(source.FetchMetricsAsync, nameof(StackOverflowProducer), _logger, ct);
            if (metrics is null)
                return;

            foreach (var metric in metrics)
                await writer.WriteAsync(metric, ct);

            _logger.LogInformation("StackOverflowProducer wrote {Count} metrics to channel", metrics.Count);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "StackOverflowProducer failed, skipping");
        }
    }
}
