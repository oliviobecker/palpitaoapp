using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Palpitao.Api.Controllers;
using Palpitao.Application;
using Palpitao.Application.Abstractions;
using Palpitao.Domain.Entities;
using Palpitao.Infrastructure.Persistence;

namespace Palpitao.ArchitectureTests;

/// <summary>
/// The dependency rule, Api → Infrastructure → Application → Domain, checked on the compiled
/// assemblies: an assembly only lists a reference it actually uses, so a stray <c>using</c> of a
/// forbidden layer or package fails here, not in review.
/// </summary>
public class LayerTests
{
    private static readonly Assembly Domain = typeof(Season).Assembly;
    private static readonly Assembly Application = typeof(DependencyInjection).Assembly;
    private static readonly Assembly Infrastructure = typeof(AppDbContext).Assembly;
    private static readonly Assembly Api = typeof(TeamsController).Assembly;

    private static IEnumerable<string> References(Assembly assembly) =>
        assembly.GetReferencedAssemblies().Select(a => a.Name!);

    [Fact]
    public void The_domain_references_nothing_but_the_base_library()
    {
        var offending = References(Domain).Where(name => !name.StartsWith("System", StringComparison.Ordinal));

        Assert.Empty(offending);
    }

    [Fact]
    public void The_application_references_only_the_domain_among_the_layers()
    {
        var layers = References(Application).Where(name => name.StartsWith("Palpitao.", StringComparison.Ordinal));

        Assert.Equal(new[] { "Palpitao.Domain" }, layers);
    }

    [Theory]
    [InlineData("Microsoft.AspNetCore")]
    [InlineData("Microsoft.EntityFrameworkCore.Relational")]
    [InlineData("Npgsql")]
    [InlineData("BCrypt")]
    [InlineData("System.IdentityModel")]
    [InlineData("Microsoft.IdentityModel")]
    [InlineData("Tesseract")]
    [InlineData("Sentry")]
    public void The_application_does_not_reference_infrastructure_packages(string package)
    {
        Assert.DoesNotContain(References(Application), name => name.StartsWith(package, StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("Palpitao.Api")]
    [InlineData("Microsoft.AspNetCore")]
    [InlineData("Sentry")]
    public void The_infrastructure_does_not_reference_the_web_host(string assembly)
    {
        Assert.DoesNotContain(References(Infrastructure), name => name.StartsWith(assembly, StringComparison.Ordinal));
    }

    [Fact]
    public void Controllers_reach_data_only_through_use_cases()
    {
        var persistence = new[] { typeof(DbContext), typeof(IAppDbContext) };
        var offending = Api.GetTypes()
            .Where(t => typeof(ControllerBase).IsAssignableFrom(t) && !t.IsAbstract)
            .SelectMany(t => t.GetConstructors().SelectMany(c => c.GetParameters()), (t, p) => (Controller: t.Name, p.ParameterType))
            .Where(x => persistence.Any(p => p.IsAssignableFrom(x.ParameterType)))
            .Select(x => $"{x.Controller} takes {x.ParameterType.Name}");

        Assert.Empty(offending);
    }
}
