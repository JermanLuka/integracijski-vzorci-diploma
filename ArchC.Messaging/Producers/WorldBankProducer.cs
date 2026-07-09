using System.Threading.Channels;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Shared.Models;
using Shared.Sources;

namespace ArchC.Messaging.Producers;

public sealed class WorldBankProducer
{
    private readonly IConfiguration _config;
    private readonly ILoggerFactory _loggerFactory;
    private readonly HttpMessageHandler? _handler;

    public WorldBankProducer(IConfiguration config, ILoggerFactory loggerFactory, HttpMessageHandler? handler = null)
    {
        _config = config;
        _loggerFactory = loggerFactory;
        _handler = handler;
    }

    public async Task ProduceAsync(ChannelWriter<Metric> writer, CancellationToken ct = default)
    {
        var logger = _loggerFactory.CreateLogger<WorldBankProducer>();
        var countryCode = _config["WorldBank:CountryCode"] ?? "SVN";

        try
        {
            var http = _handler is not null ? new HttpClient(_handler, disposeHandler: false) : new HttpClient();
            var source = new PublicDataSource(http, countryCode, _loggerFactory.CreateLogger<PublicDataSource>());
            var metrics = await FetchRetryPolicy.FetchAsync(source.FetchMetricsAsync, nameof(WorldBankProducer), logger, ct);
            if (metrics is null)
                return;

            foreach (var metric in metrics)
                await writer.WriteAsync(metric, ct);

            logger.LogInformation("WorldBankProducer wrote {Count} metrics to channel", metrics.Count);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "WorldBankProducer failed, skipping");
        }
    }
}
