using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Palpitao.Api.Auth;
using Palpitao.Application.Auth;
using Palpitao.Domain.Entities;
using Palpitao.Domain.Enums;
using Palpitao.Infrastructure.BackgroundJobs;
using Palpitao.Infrastructure.Identity;
using Palpitao.Infrastructure.Persistence;
using Palpitao.Infrastructure.Persistence.Seed;

namespace Palpitao.IntegrationTests;

/// <summary>
/// The real API in a "Testing" environment: an in-memory SQLite database built from the model
/// (no migrations at startup), no background jobs, and an HTTP client factory whose every client
/// refuses the network. Rate limits are generous unless a subclass lowers them.
/// </summary>
public class ApiFactory : WebApplicationFactory<Program>
{
    public const string AdminEmail = "admin@palpitao.local";
    public const string AdminPassword = "Admin@123";

    /// <summary>The API's JSON: web defaults, enums as strings.</summary>
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    private readonly SqliteConnection _connection = new("DataSource=:memory:");

    /// <summary>Every Error-level log line the app wrote — what Sentry would turn into an event.</summary>
    public ErrorLogCapture ErrorLogs { get; } = new();

    /// <summary>Requests per minute on the anonymous auth endpoints.</summary>
    protected virtual int AuthPermitLimit => 10_000;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        _connection.Open();

        builder.UseEnvironment("Testing");
        // UseSetting, not ConfigureAppConfiguration: Program.cs reads these before Build().
        builder.UseSetting("ConnectionStrings:DefaultConnection", "Host=replaced-by-sqlite-in-tests");
        builder.UseSetting("Jwt:Key", Convert.ToBase64String(RandomNumberGenerator.GetBytes(48)));
        builder.UseSetting("Database:ApplyMigrationsOnStartup", "false");
        builder.UseSetting("ResultsRefresh:Enabled", "false");
        builder.UseSetting("OpenApi:Enabled", "true");
        builder.UseSetting("RateLimiting:Auth:PermitLimit", AuthPermitLimit.ToString());
        builder.UseSetting("RateLimiting:Public:PermitLimit", "10000");

        builder.ConfigureLogging(logging => logging.AddProvider(ErrorLogs));

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<AppDbContext>>();
            services.AddDbContext<AppDbContext>(options => options.UseSqlite(_connection));

            foreach (var job in services.Where(d => d.ImplementationType?.IsAssignableTo(typeof(SingleRunnerJob)) == true).ToList())
            {
                services.Remove(job);
            }

            services.ConfigureHttpClientDefaults(http => http.ConfigurePrimaryHttpMessageHandler(() => new NoNetworkHandler()));
        });
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        var host = base.CreateHost(builder);
        using var scope = host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Database.EnsureCreated();
        DevelopmentAdmin.SeedIfNoUsers(db);
        return host;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            _connection.Dispose();
        }
    }

    /// <summary>Runs <paramref name="work"/> against the test database, outside any request.</summary>
    public async Task<T> WithDbAsync<T>(Func<AppDbContext, Task<T>> work)
    {
        using var scope = Services.CreateScope();
        return await work(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    public Task WithDbAsync(Func<AppDbContext, Task> work) =>
        WithDbAsync(async db => { await work(db); return true; });

    /// <summary>A group with no members, for isolation tests.</summary>
    public Task<Guid> AddGroupAsync(string name) => WithDbAsync(async db =>
    {
        var group = new Group
        {
            Id = Guid.NewGuid(),
            Name = name,
            Slug = $"group-{Guid.NewGuid():N}",
            CreatedByUserId = SeedIds.AdminUser,
            OwnerUserId = SeedIds.AdminUser,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        db.Groups.Add(group);
        await db.SaveChangesAsync();
        return group.Id;
    });

    /// <summary>An approved participant of <paramref name="groupId"/> with the given password.</summary>
    public Task<Guid> AddParticipantAsync(string email, string password, Guid groupId, bool activeInGroup = true) =>
        WithDbAsync(async db =>
        {
            var user = new User
            {
                Id = Guid.NewGuid(),
                Name = "Participant",
                Email = email,
                PasswordHash = new BCryptPasswordHasher().Hash(password),
                Role = UserRole.Participant,
                Status = UserStatus.Approved,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
            };
            db.Users.Add(user);
            db.GroupUsers.Add(new GroupUser
            {
                Id = Guid.NewGuid(),
                GroupId = groupId,
                UserId = user.Id,
                Role = GroupRole.Participant,
                Status = GroupUserStatus.Approved,
                IsActive = activeInGroup,
                ApprovedAt = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            });
            await db.SaveChangesAsync();
            return user.Id;
        });

    /// <summary>Signs in and returns a client that sends the access token (and, optionally, a group).</summary>
    public async Task<HttpClient> SignedInClientAsync(string email, string password, Guid? groupId = null, string language = "en-US")
    {
        var client = CreateClient(language);
        var login = await client.PostAsJsonAsync("/auth/login", new LoginRequest { Email = email, Password = password });
        login.EnsureSuccessStatusCode();
        var tokens = await login.Content.ReadFromJsonAsync<LoginResponse>(Json);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens!.Token);
        if (groupId is { } id)
        {
            client.DefaultRequestHeaders.Add(HttpCurrentUser.GroupHeader, id.ToString());
        }

        return client;
    }

    /// <summary>A client that asks for answers in <paramref name="language"/>.</summary>
    public HttpClient CreateClient(string language)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.AcceptLanguage.Add(new StringWithQualityHeaderValue(language));
        return client;
    }

    /// <summary>Fails every outgoing request: a test that reached for the network is a bug.</summary>
    private sealed class NoNetworkHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            throw new InvalidOperationException($"Integration tests do not reach the network ({request.RequestUri}).");
    }

    /// <summary>Collects the Error-and-above log lines of every category.</summary>
    public sealed class ErrorLogCapture : ILoggerProvider
    {
        private readonly List<string> _lines = [];

        public IReadOnlyList<string> Lines
        {
            get
            {
                lock (_lines)
                {
                    return [.. _lines];
                }
            }
        }

        public ILogger CreateLogger(string categoryName) => new Logger(this, categoryName);

        public void Dispose()
        {
        }

        private sealed class Logger(ErrorLogCapture capture, string category) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Error;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
                Func<TState, Exception?, string> formatter)
            {
                if (IsEnabled(logLevel))
                {
                    lock (capture._lines)
                    {
                        capture._lines.Add($"{category}: {formatter(state, exception)}");
                    }
                }
            }
        }
    }
}
