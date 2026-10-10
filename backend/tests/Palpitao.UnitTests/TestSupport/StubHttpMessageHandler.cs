using System.Net;
using System.Text;

namespace Palpitao.UnitTests.TestSupport;

/// <summary>
/// Stands in for the network behind a provider's <see cref="HttpClient"/>: answers each request
/// from <see cref="Respond"/> (keyed by URL) with a JSON body, and records the URLs requested.
/// </summary>
public sealed class StubHttpMessageHandler(string defaultBody = "{}") : HttpMessageHandler
{
    public Func<string, (HttpStatusCode Status, string Body)> Respond { get; set; } =
        _ => (HttpStatusCode.OK, defaultBody);

    public List<string> Requests { get; } = [];

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var url = request.RequestUri!.ToString();
        Requests.Add(url);
        var (status, body) = Respond(url);
        return Task.FromResult(new HttpResponseMessage(status)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        });
    }
}
