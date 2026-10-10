using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Palpitao.Domain.Entities;
using Palpitao.Infrastructure.Persistence;

namespace Palpitao.ArchitectureTests;

/// <summary>
/// The multi-tenant defence in depth, read off the EF model: every tenant root is filtered to the
/// request's group, and nothing else that carries a group escapes the rule by accident.
/// </summary>
public class TenancyTests
{
    /// <summary>
    /// Entities that carry a group without being tenant roots. A membership is read across groups
    /// by design (the group switcher, the access check itself), and an audit entry may belong to no
    /// group at all.
    /// </summary>
    private static readonly HashSet<Type> GroupedButUnfiltered = [typeof(GroupUser), typeof(AuditLog)];

    private static readonly IModel Model = BuildModel();

    private static IModel BuildModel()
    {
        // Building the model needs a provider, not a database: nothing here opens a connection.
        var options = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql("Host=localhost").Options;
        using var db = new AppDbContext(options);
        return db.Model;
    }

    [Fact]
    public void Every_tenant_root_is_filtered_to_the_request_group()
    {
        var roots = Model.GetEntityTypes().Where(e => typeof(IGroupOwned).IsAssignableFrom(e.ClrType)).ToList();

        Assert.NotEmpty(roots);
        Assert.All(roots, root => Assert.Single(root.GetDeclaredQueryFilters()));
    }

    [Fact]
    public void An_entity_with_a_group_is_a_tenant_root_unless_listed()
    {
        var unowned = Model.GetEntityTypes()
            .Where(e => e.FindProperty(nameof(IGroupOwned.GroupId)) is not null)
            .Select(e => e.ClrType)
            .Where(t => !typeof(IGroupOwned).IsAssignableFrom(t) && !GroupedButUnfiltered.Contains(t))
            .Select(t => t.Name);

        Assert.Empty(unowned);
    }

    [Fact]
    public void Only_tenant_roots_carry_a_query_filter()
    {
        var filtered = Model.GetEntityTypes()
            .Where(e => e.GetDeclaredQueryFilters().Count > 0)
            .Select(e => e.ClrType)
            .Where(t => !typeof(IGroupOwned).IsAssignableFrom(t))
            .Select(t => t.Name);

        Assert.Empty(filtered);
    }
}
