using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Palpitao.Api.Controllers;
using Palpitao.Api.Middlewares;
using Palpitao.Api.Monitoring;
using Palpitao.Api.Services.Localization;
using Sentry;

namespace Palpitao.Api.Tests.Monitoring;

public class SentryIntegrationTests
{
    [Fact]
    public void Sentry_settings_are_disabled_by_default()
    {
        var settings = new SentrySettings();

        Assert.Equal(string.Empty, settings.Dsn);
        Assert.False(settings.Debug);
        Assert.False(settings.SendDefaultPii);
        Assert.Equal(0.0, settings.TracesSampleRate);
    }

    [Fact]
    public void Sanitizer_filters_sensitive_request_values()
    {
        var sentryEvent = new SentryEvent
        {
            ServerName = "server01",
            Request = new SentryRequest
            {
                Cookies = "auth=secret",
                Data = new { Password = "secret" },
            },
        };
        sentryEvent.Request.Headers["Authorization"] = "Bearer jwt";
        sentryEvent.Request.Headers["X-Correlation-Id"] = "abc";
        sentryEvent.Request.Env["ConnectionStrings__DefaultConnection"] = "Host=localhost;Password=secret";

        var sanitized = SentrySensitiveDataSanitizer.Sanitize(sentryEvent);

        Assert.Null(sanitized.ServerName);
        Assert.Null(sanitized.Request!.Cookies);
        Assert.Null(sanitized.Request.Data);
        Assert.Equal("[Filtered]", sanitized.Request.Headers!["Authorization"]);
        Assert.Equal("abc", sanitized.Request.Headers["X-Correlation-Id"]);
        Assert.Equal("[Filtered]", sanitized.Request.Env!["ConnectionStrings__DefaultConnection"]);
    }

    [Fact]
    public async Task Exception_middleware_returns_friendly_500_without_stack_trace()
    {
        var services = new ServiceCollection()
            .AddSingleton<ILocalizationService, TestLocalizationService>()
            .BuildServiceProvider();

        var context = new DefaultHttpContext
        {
            RequestServices = services,
            TraceIdentifier = "trace-123",
        };
        context.Response.Body = new MemoryStream();

        var middleware = new ExceptionHandlingMiddleware(
            _ => throw new InvalidOperationException("database password leaked"),
            NullLogger<ExceptionHandlingMiddleware>.Instance);

        await middleware.InvokeAsync(context);

        context.Response.Body.Position = 0;
        var body = await new StreamReader(context.Response.Body).ReadToEndAsync();
        var json = JsonDocument.Parse(body).RootElement;

        Assert.Equal((int)HttpStatusCode.InternalServerError, context.Response.StatusCode);
        Assert.Equal("Ocorreu um erro inesperado. Tente novamente.", json.GetProperty("message").GetString());
        Assert.Equal("trace-123", json.GetProperty("traceId").GetString());
        Assert.DoesNotContain("database password leaked", body);
        Assert.DoesNotContain("InvalidOperationException", body);
    }

    [Fact]
    public void Sentry_test_endpoint_is_admin_only_and_disabled_outside_development()
    {
        var authorize = typeof(AdminSentryController)
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .OfType<AuthorizeAttribute>()
            .Single();

        var controller = new AdminSentryController(new TestWebHostEnvironment
        {
            EnvironmentName = Environments.Production,
        });

        Assert.Equal("Admin", authorize.Roles);
        Assert.IsType<NotFoundResult>(controller.TestError());
    }

    private sealed class TestLocalizationService : ILocalizationService
    {
        public string Language => "pt";

        public string Get(string key) => key == "error.unexpected"
            ? "Ocorreu um erro inesperado. Tente novamente."
            : key;

        public string Get(string key, string? acceptLanguage) => Get(key);

        public string ResolveLanguage(string? acceptLanguage) => "pt";
    }

    private sealed class TestWebHostEnvironment : IWebHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Production;
        public string ApplicationName { get; set; } = "Palpitao.Api.Tests";
        public string WebRootPath { get; set; } = string.Empty;
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string ContentRootPath { get; set; } = string.Empty;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
