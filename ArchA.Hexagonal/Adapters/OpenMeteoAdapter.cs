using ArchA.Hexagonal.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Shared.Models;
using Shared.Sources;

namespace ArchA.Hexagonal.Adapters;

/// <summary>
/// Adapter that wraps OpenMeteoSource behind the IMetricSource port.
/// </summary>
public sealed class OpenMeteoAdapter : IMetricSource
{
    private readonly IConfiguration _config;
    private readonly ILoggerFactory _loggerFactory;
    private readonly HttpMessageHandler? _handler;

    public OpenMeteoAdapter(IConfiguration config, ILoggerFactory loggerFactory, HttpMessageHandler? handler = null)
    {
        _config = config;
        _loggerFactory = loggerFactory;
        _handler = handler;
    }

    public string Name => "OpenMeteo";

    public async Task<IReadOnlyList<Metric>> FetchAsync(CancellationToken ct = default)
    {
        var latitude = _config["OpenMeteo:Latitude"] ?? "46.05";
        var longitude = _config["OpenMeteo:Longitude"] ?? "14.51";
        var http = _handler is not null ? new HttpClient(_handler, disposeHandler: false) : new HttpClient();
        var source = new OpenMeteoSource(http, latitude, longitude, _loggerFactory.CreateLogger<OpenMeteoSource>());
        return await source.FetchMetricsAsync(ct);
    }
}
