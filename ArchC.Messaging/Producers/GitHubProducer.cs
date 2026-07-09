using System.Threading.Channels;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Shared.Models;
using Shared.Sources;

namespace ArchC.Messaging.Producers;

public sealed class GitHubProducer
{
    private readonly IConfiguration _config;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<GitHubProducer> _logger;
    private readonly HttpMessageHandler? _handler;

    public GitHubProducer(IConfiguration config, ILoggerFactory loggerFactory, HttpMessageHandler? handler = null)
    {
        _config = config;
        _loggerFactory = loggerFactory;
        _logger = loggerFactory.CreateLogger<GitHubProducer>();
        _handler = handler;
    }

    public async Task ProduceAsync(ChannelWriter<Metric> writer, CancellationToken ct = default)
    {
        var token = _config["GitHub:Token"];
        if (string.IsNullOrWhiteSpace(token))
        {
            _logger.LogWarning("GitHub token not configured, skipping");
            return;
        }

        try
        {
            var http = _handler is not null ? new HttpClient(_handler, disposeHandler: false) : new HttpClient();
            var source = new GitHubSource(http, token, _loggerFactory.CreateLogger<GitHubSource>());
            var metrics = await FetchRetryPolicy.FetchAsync(source.FetchMetricsAsync, nameof(GitHubProducer), _logger, ct);
            if (metrics is null)
                return;

            foreach (var metric in metrics)
                await writer.WriteAsync(metric, ct);

            _logger.LogInformation("GitHubProducer wrote {Count} metrics to channel", metrics.Count);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "GitHubProducer failed, skipping");
        }
    }
}
