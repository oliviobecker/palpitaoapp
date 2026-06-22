using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using Palpitao.Api.Common;
using Xunit;

namespace Palpitao.Api.Tests.Http;

public class TransientHttpRetryHandlerTests
{
    private static readonly CancellationToken Ct = CancellationToken.None;
    // Two retries with no real delay so the tests stay fast.
    private static readonly IReadOnlyList<TimeSpan> NoDelay = new[] { TimeSpan.Zero, TimeSpan.Zero };

    /// <summary>Inner handler that returns a scripted sequence of responses and counts calls.</summary>
    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Queue<Func<HttpResponseMessage>> _steps;
        public int Calls { get; private set; }

        public StubHandler(params Func<HttpResponseMessage>[] steps) => _steps = new Queue<Func<HttpResponseMessage>>(steps);

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Calls++;
            return Task.FromResult(_steps.Dequeue().Invoke());
        }
    }

    private static Func<HttpResponseMessage> Respond(HttpStatusCode code) => () => new HttpResponseMessage(code);
    private static Func<HttpResponseMessage> Throws() => () => throw new HttpRequestException("transient");

    private static HttpMessageInvoker Invoker(StubHandler inner) => new(
        new TransientHttpRetryHandler(NullLogger<TransientHttpRetryHandler>.Instance, NoDelay) { InnerHandler = inner });

    private static HttpRequestMessage Get() => new(HttpMethod.Get, "https://example.test/x");

    [Fact]
    public async Task Retries_a_transient_status_then_succeeds()
    {
        var inner = new StubHandler(
            Respond(HttpStatusCode.ServiceUnavailable),
            Respond(HttpStatusCode.ServiceUnavailable),
            Respond(HttpStatusCode.OK));

        var response = await Invoker(inner).SendAsync(Get(), Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(3, inner.Calls);
    }

    [Fact]
    public async Task Gives_up_and_returns_the_last_response_after_exhausting_retries()
    {
        var inner = new StubHandler(
            Respond(HttpStatusCode.InternalServerError),
            Respond(HttpStatusCode.InternalServerError),
            Respond(HttpStatusCode.InternalServerError));

        var response = await Invoker(inner).SendAsync(Get(), Ct);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal(3, inner.Calls); // 1 initial + 2 retries
    }

    [Fact]
    public async Task Retries_a_transient_exception_then_succeeds()
    {
        var inner = new StubHandler(Throws(), Throws(), Respond(HttpStatusCode.OK));

        var response = await Invoker(inner).SendAsync(Get(), Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(3, inner.Calls);
    }

    [Fact]
    public async Task Does_not_retry_non_get_requests()
    {
        var inner = new StubHandler(Respond(HttpStatusCode.ServiceUnavailable));

        var response = await Invoker(inner).SendAsync(new HttpRequestMessage(HttpMethod.Post, "https://example.test/x"), Ct);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal(1, inner.Calls);
    }

    [Fact]
    public async Task Does_not_retry_non_transient_client_errors()
    {
        var inner = new StubHandler(Respond(HttpStatusCode.NotFound));

        var response = await Invoker(inner).SendAsync(Get(), Ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(1, inner.Calls);
    }
}
