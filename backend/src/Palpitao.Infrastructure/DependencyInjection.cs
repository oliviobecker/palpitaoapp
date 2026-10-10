using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Palpitao.Application.Abstractions;
using Palpitao.Application.Auth;
using Palpitao.Application.Fixtures;
using Palpitao.Application.Ocr;
using Palpitao.Application.Results;
using Palpitao.Application.Teams;
using Palpitao.Infrastructure.BackgroundJobs;
using Palpitao.Infrastructure.ExternalData.Fixtures;
using Palpitao.Infrastructure.ExternalData.Http;
using Palpitao.Infrastructure.ExternalData.Results;
using Palpitao.Infrastructure.ExternalData.Teams;
using Palpitao.Infrastructure.Identity;
using Palpitao.Infrastructure.Ocr;
using Palpitao.Infrastructure.Persistence;

namespace Palpitao.Infrastructure;

public static class DependencyInjection
{
    /// <summary>
    /// Registers the adapters behind the Application's ports and binds their configuration
    /// sections. The per-request ports (<see cref="ICurrentUser"/>,
    /// <see cref="IRequestGroupContext"/>, <see cref="ILocalizationService"/>) come from the web host.
    /// </summary>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        AddPersistence(services, configuration);
        AddIdentity(services, configuration);
        AddOcr(services, configuration);
        AddExternalData(services, configuration);
        return services;
    }

    private static void AddPersistence(IServiceCollection services, IConfiguration configuration)
    {
        // PostgreSQL + EF Core (code-first). The connection string comes from
        // "ConnectionStrings:DefaultConnection", overridable by ConnectionStrings__DefaultConnection.
        services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString(ConnectionStrings.DefaultName)));
        // The use cases depend on IAppDbContext; it must be the request's AppDbContext instance, which
        // the transaction runner and the tenant filter also work through.
        services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<AppDbContext>());
        services.AddScoped<ITransactionRunner, EfTransactionRunner>();
        services.AddScoped<IDatabaseHealthProbe, EfDatabaseHealthProbe>();
    }

    private static void AddIdentity(IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<JwtSettings>(configuration.GetSection(JwtSettings.SectionName));
        services.AddScoped<IJwtTokenService, JwtTokenService>();
        services.AddSingleton<IPasswordHasher, BCryptPasswordHasher>();
    }

    private static void AddOcr(IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<IOcrEngine, TesseractOcrEngine>();
        // Uploaded images live in Postgres, so their footprint is capped per round and swept by age.
        services.Configure<OcrStorageOptions>(configuration.GetSection(OcrStorageOptions.SectionName));
        services.AddHostedService<OcrImageRetentionBackgroundService>();
    }

    private static void AddExternalData(IServiceCollection services, IConfiguration configuration)
    {
        // Transient-fault retry handler shared by the external fixture/results HTTP clients.
        services.AddTransient<TransientHttpRetryHandler>();

        // Fixtures (round-by-period import). Isolated provider with its own HttpClient (timeout +
        // key/user-agent set in ctor), wrapped in transient-fault retry. Selected by Fixtures:Provider:
        //   fixturedownload — free, no key, Premier League + Championship only
        //   apifootball     — all four competitions, but the free tier lacks the current season
        //   thesportsdb     — free, but the public test key returns only sample data
        //   (default)       — OneFootball web-experience API: free, covers all four competitions
        var fixturesSection = configuration.GetSection(FixtureOptions.SectionName);
        services.Configure<FixtureOptions>(fixturesSection);
        var fixtureProvider = (fixturesSection.Get<FixtureOptions>() ?? new FixtureOptions()).Provider;
        var fixtureClient = fixtureProvider?.ToLowerInvariant() switch
        {
            "fixturedownload" => services.AddHttpClient<IFixtureProvider, FixtureDownloadFixtureProvider>(),
            "apifootball" => services.AddHttpClient<IFixtureProvider, ApiFootballFixtureProvider>(),
            "thesportsdb" => services.AddHttpClient<IFixtureProvider, TheSportsDbFixtureProvider>(),
            _ => services.AddHttpClient<IFixtureProvider, OneFootballFixtureProvider>(),
        };
        fixtureClient.AddHttpMessageHandler<TransientHttpRetryHandler>();

        // Team catalogue (admin review + squad-list sync). Registered unconditionally: the catalogue
        // sync reads OneFootball by design, independently of which provider Fixtures:Provider picks.
        services.AddHttpClient<ITeamCatalogProvider, OneFootballTeamCatalogProvider>()
            .AddHttpMessageHandler<TransientHttpRetryHandler>();

        // Match results (refresh + temporary standings).
        var resultsSection = configuration.GetSection(ResultsProviderOptions.SectionName);
        services.Configure<ResultsProviderOptions>(resultsSection);
        var resultsProvider = (resultsSection.Get<ResultsProviderOptions>() ?? new ResultsProviderOptions()).Provider;
        if (string.Equals(resultsProvider, "OneFootball", StringComparison.OrdinalIgnoreCase))
        {
            // Real source: same OneFootball web-experience API used for fixture import.
            services.AddHttpClient<IResultsProvider, OneFootballResultsProvider>()
                .AddHttpMessageHandler<TransientHttpRetryHandler>();
        }
        else if (string.Equals(resultsProvider, "ConfiguredWebsite", StringComparison.OrdinalIgnoreCase))
        {
            services.AddHttpClient<IResultsProvider, ConfiguredWebsiteResultsProvider>()
                .AddHttpMessageHandler<TransientHttpRetryHandler>();
        }
        else
        {
            // Default: manual results (no external fetch; temporary standings still work).
            services.AddScoped<IResultsProvider, ManualResultsProvider>();
        }

        // Periodic background results refresh (a safe no-op when ResultsRefresh:Enabled is off).
        services.Configure<ResultsRefreshOptions>(configuration.GetSection(ResultsRefreshOptions.SectionName));
        services.AddHostedService<ResultsRefreshBackgroundService>();
    }
}
