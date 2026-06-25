using System.Net;

namespace Shared.Tests;

/// <summary>
/// A test HTTP handler that returns canned responses based on request path + query.
/// </summary>
public sealed class FakeHttpHandler : HttpMessageHandler
{
    private readonly Dictionary<string, string> _responses;

    public FakeHttpHandler(Dictionary<string, string> responses)
    {
        _responses = responses;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var key = request.RequestUri!.PathAndQuery;

        if (!_responses.TryGetValue(key, out var body))
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)
            {
                Content = new StringContent($"No canned response for: {key}"),
            });
        }

        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json"),
        });
    }
}
