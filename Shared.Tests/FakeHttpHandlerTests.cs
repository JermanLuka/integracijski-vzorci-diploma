using System.Net;

namespace Shared.Tests;

public class FakeHttpHandlerTests
{
    private static HttpClient MakeClient(FakeHttpHandler handler) =>
        new(handler, disposeHandler: false) { BaseAddress = new Uri("https://example.test") };

    [Fact]
    public async Task TransientKey_FailsFirstThenServesNormalResponse()
    {
        var handler = new FakeHttpHandler(
            new Dictionary<string, string> { ["/flaky"] = """{"ok":true}""" },
            new Dictionary<string, int> { ["/flaky"] = 1 });
        var client = MakeClient(handler);

        var first = await client.GetAsync("/flaky");
        var second = await client.GetAsync("/flaky");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.Equal("""{"ok":true}""", await second.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task KeyWithoutTransientFailure_AlwaysServesNormalResponse()
    {
        var handler = new FakeHttpHandler(
            new Dictionary<string, string>
            {
                ["/stable"] = """{"ok":true}""",
                ["/flaky"] = """{"ok":true}""",
            },
            new Dictionary<string, int> { ["/flaky"] = 1 });
        var client = MakeClient(handler);

        var first = await client.GetAsync("/stable");
        var second = await client.GetAsync("/stable");

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
    }

    [Fact]
    public async Task NoTransientFailuresConfigured_BehavesAsBefore()
    {
        var handler = new FakeHttpHandler(new Dictionary<string, string> { ["/a"] = "body" });
        var client = MakeClient(handler);

        var known = await client.GetAsync("/a");
        var unknown = await client.GetAsync("/missing");

        Assert.Equal(HttpStatusCode.OK, known.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
    }
}
