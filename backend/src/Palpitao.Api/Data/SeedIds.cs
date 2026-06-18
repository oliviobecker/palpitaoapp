namespace Palpitao.Api.Data;

/// <summary>
/// Stable identifiers used by the seed data (and referenced by tests).
/// </summary>
public static class SeedIds
{
    // Big Seven clubs
    public static readonly Guid Arsenal = Guid.Parse("11111111-1111-1111-1111-111111111101");
    public static readonly Guid Chelsea = Guid.Parse("11111111-1111-1111-1111-111111111102");
    public static readonly Guid Liverpool = Guid.Parse("11111111-1111-1111-1111-111111111103");
    public static readonly Guid ManchesterCity = Guid.Parse("11111111-1111-1111-1111-111111111104");
    public static readonly Guid ManchesterUnited = Guid.Parse("11111111-1111-1111-1111-111111111105");
    public static readonly Guid Newcastle = Guid.Parse("11111111-1111-1111-1111-111111111106");
    public static readonly Guid Tottenham = Guid.Parse("11111111-1111-1111-1111-111111111107");

    // Development admin user
    public static readonly Guid AdminUser = Guid.Parse("22222222-2222-2222-2222-222222222201");

    // Default group (tenant) that owns all pre-existing data, plus the admin's
    // GroupAdmin membership in it.
    public static readonly Guid DefaultGroup = Guid.Parse("33333333-3333-3333-3333-333333333301");
    public static readonly Guid DefaultGroupAdminMembership = Guid.Parse("33333333-3333-3333-3333-333333333302");
}
