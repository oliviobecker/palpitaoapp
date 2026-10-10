using Palpitao.Api.Auth;
using Palpitao.Api.Extensions;
using Palpitao.Api.Middlewares;
using Palpitao.Api.Monitoring;
using Palpitao.Api.OpenApi;
using Palpitao.Application;
using Palpitao.Application.Auth;
using Palpitao.Infrastructure;
using Palpitao.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UsePalpitaoSentry();

// Fail fast on security-critical misconfiguration: an empty/weak signing key, the dev placeholder
// key outside Development, or a missing connection string. Better to crash loudly at startup than
// to boot accepting forgeable tokens.
var jwtSettings = builder.Configuration.GetSection(JwtSettings.SectionName).Get<JwtSettings>() ?? new JwtSettings();
StartupValidation.Validate(
    jwtSettings.Key,
    builder.Configuration.GetConnectionString(ConnectionStrings.DefaultName),
    builder.Environment.IsDevelopment());

builder.Services
    .AddApplication()
    .AddInfrastructure(builder.Configuration)
    .AddApiServices()
    .AddJwtAuthentication(jwtSettings)
    .AddRateLimitPolicies(builder.Configuration)
    .AddCorsPolicies(builder.Configuration);

var app = builder.Build();

app.ApplyDatabaseMigrations();

// --- HTTP pipeline ----------------------------------------------------------
app.UseMiddleware<ExceptionHandlingMiddleware>();

app.MapApiDocumentation();

if (app.Environment.IsDevelopment())
{
    app.UseCors(CorsPolicies.Development);
    app.UseHttpsRedirection();
}
else if (CorsPolicies.AllowedOrigins(app.Configuration).Length > 0)
{
    // CORS must run before auth so preflight (OPTIONS) responses carry the headers.
    app.UseCors(CorsPolicies.ConfiguredOrigins);
}

app.UseAuthentication();
app.UseMiddleware<SentryUserContextMiddleware>();
app.UseAuthorization();
app.UseRateLimiter();

app.MapControllers();

app.Run();
