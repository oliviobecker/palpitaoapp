using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Palpitao.Api.Auth;
using Palpitao.Api.Common;
using Palpitao.Api.Validation;
using Palpitao.Api.Data;
using Palpitao.Api.Middlewares;
using Palpitao.Api.Monitoring;
using Palpitao.Api.Services.Absences;
using Palpitao.Api.Services.Auth;
using Palpitao.Api.Services.Registrations;
using Palpitao.Api.Services.AdminPredictions;
using Palpitao.Api.Services.Audit;
using Palpitao.Api.Services.Fixtures;
using Palpitao.Api.Services.Flavio;
using Palpitao.Api.Services.Groups;
using Palpitao.Api.Services.Localization;
using Palpitao.Api.Services.Ocr;
using Palpitao.Api.Services.Predictions;
using Palpitao.Api.Services.Results;
using Palpitao.Api.Services.Rounds;
using Palpitao.Api.Services.Scoring;
using Palpitao.Api.Services.Scouts;
using Palpitao.Api.Services.Seasons;
using Palpitao.Api.Services.Standings;
using Palpitao.Api.Services.Users;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UsePalpitaoSentry();

// --- Services ---------------------------------------------------------------
builder.Services.AddControllers(options =>
    {
        // FluentValidation runs the request validators and throws a localized 400.
        options.Filters.Add<ValidationActionFilter>();
    })
    .AddJsonOptions(options =>
    {
        // Serialize enums as readable strings (e.g. "PremierLeague").
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

// Validation: FluentValidation validators + suppress the DataAnnotations ModelState
// 400 so the ValidationActionFilter owns validation (localized via DomainMessages).
builder.Services.AddValidatorsFromAssemblyContaining<LoginRequestValidator>();
builder.Services.Configure<ApiBehaviorOptions>(options => options.SuppressModelStateInvalidFilter = true);

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

// PostgreSQL + EF Core (code-first). Connection string comes from configuration
// ("ConnectionStrings:DefaultConnection") and can be overridden by the
// environment variable ConnectionStrings__DefaultConnection (see .env.example).
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString));

// --- Auth (JWT) -------------------------------------------------------------
builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection("Jwt"));
var jwtSettings = builder.Configuration.GetSection("Jwt").Get<JwtSettings>() ?? new JwtSettings();

// Fail fast on security-critical misconfiguration: an empty/weak signing key, the
// dev placeholder key outside Development, or a missing connection string. Better to
// crash loudly at startup than to boot accepting forgeable tokens.
StartupValidation.Validate(jwtSettings.Key, connectionString, builder.Environment.IsDevelopment());

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtSettings.Issuer,
            ValidAudience = jwtSettings.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.Key)),
        };
    });
builder.Services.AddAuthorization();

// --- Rate limiting (abuse protection on auth endpoints) ---------------------
// Throttles the unauthenticated auth endpoints (login/register/create-group/
// refresh) per client IP so BCrypt's per-guess cost can't be brute-forced by
// volume. Tunable via "RateLimiting:Auth". NOTE: behind a reverse proxy, configure
// forwarded headers at the proxy so RemoteIpAddress is the real client, not the proxy.
const string AuthRateLimitPolicy = "auth";
var authRateLimitPermit = builder.Configuration.GetValue<int?>("RateLimiting:Auth:PermitLimit") ?? 20;
var authRateLimitWindowSeconds = builder.Configuration.GetValue<int?>("RateLimiting:Auth:WindowSeconds") ?? 60;
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy(AuthRateLimitPolicy, httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = authRateLimitPermit,
                Window = TimeSpan.FromSeconds(authRateLimitWindowSeconds),
                QueueLimit = 0,
            }));

    // Consistent JSON body with the rest of the API ({ status, message }), localized.
    options.OnRejected = async (context, ct) =>
    {
        var response = context.HttpContext.Response;
        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
        {
            response.Headers.RetryAfter = ((int)retryAfter.TotalSeconds).ToString();
        }

        if (!response.HasStarted)
        {
            response.StatusCode = StatusCodes.Status429TooManyRequests;
            response.ContentType = "application/json; charset=utf-8";
            var localizer = context.HttpContext.RequestServices.GetRequiredService<ILocalizationService>();
            var payload = JsonSerializer.Serialize(new
            {
                status = 429,
                message = localizer.Get("error.tooManyRequests"),
                traceId = context.HttpContext.TraceIdentifier,
            });
            await response.WriteAsync(payload, ct);
        }
    };
});

// --- Application / domain services ------------------------------------------
builder.Services.AddHttpContextAccessor();
// DB-free per-request group accessor consumed by AppDbContext for the multi-tenant
// query filter + insert-stamping (defence-in-depth, separate from access validation).
builder.Services.AddScoped<IRequestGroupContext, RequestGroupContext>();
builder.Services.AddScoped<ILocalizationService, LocalizationService>();
builder.Services.AddSingleton<IScoringService, ScoringService>();
builder.Services.AddScoped<IRoundScoringService, RoundScoringService>();
builder.Services.AddScoped<IStandingsService, StandingsService>();
builder.Services.AddScoped<IJwtTokenService, JwtTokenService>();
builder.Services.AddScoped<IRefreshTokenService, RefreshTokenService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<ICurrentGroupService, CurrentGroupService>();
builder.Services.AddScoped<IGroupService, GroupService>();
builder.Services.AddScoped<IRegistrationRequestService, RegistrationRequestService>();
builder.Services.AddScoped<IAuditService, AuditService>();
builder.Services.AddScoped<IRoundService, RoundService>();
builder.Services.AddScoped<IPredictionsService, PredictionsService>();
builder.Services.AddScoped<IAbsenceService, AbsenceService>();
builder.Services.AddScoped<IFlavioRuleService, FlavioRuleService>();
builder.Services.AddScoped<ISeasonService, SeasonService>();
builder.Services.AddScoped<IUserAdminService, UserAdminService>();
builder.Services.AddScoped<IAdminPredictionService, AdminPredictionService>();
builder.Services.AddScoped<IPredictionImportService, PredictionImportService>();
builder.Services.AddScoped<IOcrService, OcrService>();
builder.Services.AddSingleton<IOcrEngine, TesseractOcrEngine>();

// --- External fixtures (round-by-period import) -----------------------------
builder.Services.Configure<FixtureOptions>(builder.Configuration.GetSection(FixtureOptions.SectionName));
// Transient-fault retry handler shared by the external fixture/results HTTP clients.
builder.Services.AddTransient<TransientHttpRetryHandler>();
// Isolated provider with its own HttpClient (timeout + key/user-agent set in ctor),
// wrapped in transient-fault retry. Selected by Fixtures:Provider:
//   fixturedownload — free, no key, Premier League + Championship only
//   apifootball     — all four competitions, but the free tier lacks the current season
//   thesportsdb     — free, but the public test key returns only sample data
//   (default)       — OneFootball web-experience API: free, covers all four competitions
var fixtureOptions = builder.Configuration.GetSection(FixtureOptions.SectionName).Get<FixtureOptions>()
    ?? new FixtureOptions();
IHttpClientBuilder fixtureClient = fixtureOptions.Provider?.ToLowerInvariant() switch
{
    "fixturedownload" => builder.Services.AddHttpClient<IFixtureProvider, FixtureDownloadFixtureProvider>(),
    "apifootball" => builder.Services.AddHttpClient<IFixtureProvider, ApiFootballFixtureProvider>(),
    "thesportsdb" => builder.Services.AddHttpClient<IFixtureProvider, TheSportsDbFixtureProvider>(),
    _ => builder.Services.AddHttpClient<IFixtureProvider, OneFootballFixtureProvider>(),
};
fixtureClient.AddHttpMessageHandler<TransientHttpRetryHandler>();
builder.Services.AddScoped<IFixtureImportService, FixtureImportService>();

// --- Match results (refresh + temporary standings) --------------------------
builder.Services.Configure<ResultsProviderOptions>(
    builder.Configuration.GetSection(ResultsProviderOptions.SectionName));
var resultsOptions = builder.Configuration.GetSection(ResultsProviderOptions.SectionName)
    .Get<ResultsProviderOptions>() ?? new ResultsProviderOptions();
if (string.Equals(resultsOptions.Provider, "OneFootball", StringComparison.OrdinalIgnoreCase))
{
    // Real source: same OneFootball web-experience API used for fixture import.
    builder.Services.AddHttpClient<IResultsProvider, OneFootballResultsProvider>()
        .AddHttpMessageHandler<TransientHttpRetryHandler>();
}
else if (string.Equals(resultsOptions.Provider, "ConfiguredWebsite", StringComparison.OrdinalIgnoreCase))
{
    builder.Services.AddHttpClient<IResultsProvider, ConfiguredWebsiteResultsProvider>()
        .AddHttpMessageHandler<TransientHttpRetryHandler>();
}
else
{
    // Default: manual results (no external fetch; temporary standings still work).
    builder.Services.AddScoped<IResultsProvider, ManualResultsProvider>();
}
builder.Services.AddScoped<IResultsUpdateService, ResultsUpdateService>();
builder.Services.AddScoped<ITemporaryStandingsService, TemporaryStandingsService>();
builder.Services.AddScoped<IScoutService, ScoutService>();

// Periodic background results refresh (disabled by default; safe no-op when off).
builder.Services.Configure<ResultsRefreshOptions>(
    builder.Configuration.GetSection(ResultsRefreshOptions.SectionName));
builder.Services.AddHostedService<ResultsRefreshBackgroundService>();

// CORS. Development allows the Angular dev server; other environments allow the
// trusted origins configured under "Cors:AllowedOrigins" (e.g. the deployed
// frontend URL), so the API can serve a cross-origin SPA without being open.
const string DevCorsPolicy = "AllowFrontendDev";
const string ProdCorsPolicy = "AllowConfiguredOrigins";
var corsAllowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
    ?? Array.Empty<string>();
builder.Services.AddCors(options =>
{
    options.AddPolicy(DevCorsPolicy, policy =>
        policy.WithOrigins("http://localhost:4200")
              .AllowAnyHeader()
              .AllowAnyMethod());

    if (corsAllowedOrigins.Length > 0)
    {
        options.AddPolicy(ProdCorsPolicy, policy =>
            policy.WithOrigins(corsAllowedOrigins)
                  .AllowAnyHeader()
                  .AllowAnyMethod());
    }
});

var app = builder.Build();

// Apply pending migrations on startup (creates the schema + seed on first run).
// Resilient: if the database is unreachable, log and keep the API up so /health
// still responds and the real error is visible in the logs.
using (var scope = app.Services.CreateScope())
{
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    try
    {
        // Log the target database (password redacted) so a misconfigured
        // production connection string is obvious in the logs.
        logger.LogInformation("Conectando ao banco: {ConnectionString}", MaskConnectionString(connectionString));

        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Database.Migrate();
        logger.LogInformation("Migrations aplicadas com sucesso.");
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Falha ao aplicar migrations no startup.");

        // In Development we keep the API up (so /health is reachable while you fix the
        // DB locally). Outside Development a migration/schema failure must not be masked:
        // fail fast so the deploy is rolled back / the host restarts instead of serving a
        // drifted schema.
        if (!app.Environment.IsDevelopment())
        {
            throw;
        }
    }
}

// --- HTTP pipeline ----------------------------------------------------------
app.UseMiddleware<ExceptionHandlingMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseCors(DevCorsPolicy);
    app.UseHttpsRedirection();
}
else if (corsAllowedOrigins.Length > 0)
{
    // CORS must run before auth so preflight (OPTIONS) responses carry the headers.
    app.UseCors(ProdCorsPolicy);
}

app.UseAuthentication();
app.UseMiddleware<SentryUserContextMiddleware>();
app.UseAuthorization();
app.UseRateLimiter();

app.MapControllers();

app.Run();

// Redacts the password from a Npgsql connection string for safe logging.
static string MaskConnectionString(string? connectionString)
{
    if (string.IsNullOrWhiteSpace(connectionString))
    {
        return "(não configurada)";
    }

    try
    {
        var builder = new Npgsql.NpgsqlConnectionStringBuilder(connectionString);
        if (!string.IsNullOrEmpty(builder.Password))
        {
            builder.Password = "***";
        }

        return builder.ConnectionString;
    }
    catch
    {
        return "(string de conexão inválida)";
    }
}
