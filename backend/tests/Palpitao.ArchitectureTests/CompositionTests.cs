using System.Security.Cryptography;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Palpitao.Api.Extensions;
using Palpitao.Application;
using Palpitao.Application.Abstractions;
using Palpitao.Application.Auth;
using Palpitao.Infrastructure;
using Palpitao.Infrastructure.Persistence;

namespace Palpitao.ArchitectureTests;

/// <summary>
/// The web host's registrations (the same chain as <c>Program.cs</c>) under the container's own
/// checks: every service resolvable, and no singleton holding a scoped service. Nothing starts —
/// no server, no hosted service, no database.
/// </summary>
public sealed class CompositionTests : IDisposable
{
    private readonly WebApplication _app;

    public CompositionTests()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Testing" });
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = "Host=localhost;Database=never-opened",
            ["Jwt:Key"] = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48)),
        });
        builder.Host.UseDefaultServiceProvider(options =>
        {
            options.ValidateOnBuild = true;
            options.ValidateScopes = true;
        });

        var jwt = builder.Configuration.GetSection(JwtSettings.SectionName).Get<JwtSettings>()!;
        builder.Services
            .AddApplication()
            .AddInfrastructure(builder.Configuration)
            .AddApiServices()
            .AddJwtAuthentication(jwt)
            .AddRateLimitPolicies(builder.Configuration)
            .AddCorsPolicies(builder.Configuration);

        _app = builder.Build(); // throws if any registration cannot be resolved
    }

    public void Dispose() => ((IDisposable)_app).Dispose();

    [Fact]
    public void The_container_builds_with_every_service_resolvable()
    {
        Assert.NotNull(_app.Services);
    }

    [Fact]
    public void The_use_cases_share_the_request_s_db_context()
    {
        // The transaction runner, the tenant filter and the use cases must all see one unit of work.
        using var scope = _app.Services.CreateScope();

        Assert.Same(
            scope.ServiceProvider.GetRequiredService<AppDbContext>(),
            scope.ServiceProvider.GetRequiredService<IAppDbContext>());
    }
}
