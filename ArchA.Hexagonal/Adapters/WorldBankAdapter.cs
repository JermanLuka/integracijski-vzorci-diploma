using ArchA.Hexagonal.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Shared.Models;
using Shared.Sources;

namespace ArchA.Hexagonal.Adapters;

/// <summary>
/// Adapter that wraps PublicDataSource behind the IMetricSource port.
/// </summary>
public sealed class WorldBankAdapter : IMetricSource
{
    private readonly IConfiguration _config;
    private readonly ILoggerFactory _loggerFactory;
    private readonly HttpMessageHandler? _handler;

    public WorldBankAdapter(IConfiguration config, ILoggerFactory loggerFactory, HttpMessageHandler? handler = null)
    {
        _config = config;
        _loggerFactory = loggerFactory;
        _handler = handler;
    }

    public string Name => "WorldBank";

    public async Task<IReadOnlyList<Metric>> FetchAsync(CancellationToken ct = default)
    {
        var countryCode = _config["WorldBank:CountryCode"] ?? "SVN";
        using var http = _handler is not null ? new HttpClient(_handler, disposeHandler: false) : new HttpClient();
        var source = new PublicDataSource(http, countryCode, _loggerFactory.CreateLogger<PublicDataSource>());
        return await source.FetchMetricsAsync(ct);
    }
}
