using System.Threading.Channels;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Shared.Models;
using Shared.Sources;

namespace ArchC.Messaging.Producers;

public sealed class OpenMeteoProducer
{
    private readonly IConfiguration _config;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<OpenMeteoProducer> _logger;
    private readonly HttpMessageHandler? _handler;

    public OpenMeteoProducer(IConfiguration config, ILoggerFactory loggerFactory, HttpMessageHandler? handler = null)
    {
        _config = config;
        _loggerFactory = loggerFactory;
        _logger = loggerFactory.CreateLogger<OpenMeteoProducer>();
        _handler = handler;
    }

    public async Task ProduceAsync(ChannelWriter<Metric> writer, CancellationToken ct = default)
    {
        var latitude = _config["OpenMeteo:Latitude"] ?? "46.05";
        var longitude = _config["OpenMeteo:Longitude"] ?? "14.51";

        try
        {
            var http = _handler is not null ? new HttpClient(_handler, disposeHandler: false) : new HttpClient();
            var source = new OpenMeteoSource(http, latitude, longitude, _loggerFactory.CreateLogger<OpenMeteoSource>());
            var metrics = await FetchRetryPolicy.FetchAsync(source.FetchMetricsAsync, nameof(OpenMeteoProducer), _logger, ct);
            if (metrics is null)
                return;

            foreach (var metric in metrics)
                await writer.WriteAsync(metric, ct);

            _logger.LogInformation("OpenMeteoProducer wrote {Count} metrics to channel", metrics.Count);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "OpenMeteoProducer failed, skipping");
        }
    }
}
