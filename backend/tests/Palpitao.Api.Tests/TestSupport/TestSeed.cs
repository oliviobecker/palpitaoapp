using Palpitao.Api.Data;
using Palpitao.Api.Entities;
using Palpitao.Api.Enums;

namespace Palpitao.Api.Tests.TestSupport;

/// <summary>Helpers for seeding group memberships in service unit tests.</summary>
public static class TestSeed
{
    /// <summary>
    /// Test-only clubs that belong to no classic group, so a match between them takes the plain
    /// (competition, phase) multiplier. Tests that measure base points use these — a Big Seven
    /// or Championship-rival pair would double wherever the ruleset gives classics a value.
    /// Names are unique in the catalogue, so the results provider can match on them.
    /// </summary>
    private static readonly (Guid Id, string Name)[] NeutralTeams =
    {
        (Guid.Parse("55555555-5555-5555-5555-555555555501"), "Neutral Rovers"),
        (Guid.Parse("55555555-5555-5555-5555-555555555502"), "Neutral Athletic"),
        (Guid.Parse("55555555-5555-5555-5555-555555555503"), "Neutral Wanderers"),
        (Guid.Parse("55555555-5555-5555-5555-555555555504"), "Neutral County"),
        (Guid.Parse("55555555-5555-5555-5555-555555555505"), "Neutral Albion"),
        (Guid.Parse("55555555-5555-5555-5555-555555555506"), "Neutral Town"),
    };

    /// <summary>The <see cref="NeutralTeams"/> as home/away pairs, in declaration order.</summary>
    public static readonly (Guid Home, Guid Away)[] NeutralPairs = NeutralTeams
        .Chunk(2)
        .Select(pair => (pair[0].Id, pair[1].Id))
        .ToArray();

    /// <summary>The same pairs by name, for tests that match teams the way a provider does.</summary>
    public static readonly (string Home, string Away)[] NeutralPairNames = NeutralTeams
        .Chunk(2)
        .Select(pair => (pair[0].Name, pair[1].Name))
        .ToArray();

    /// <summary>Adds the neutral clubs to the catalogue (does not SaveChanges).</summary>
    public static void AddNeutralTeams(AppDbContext db)
    {
        foreach (var (id, name) in NeutralTeams)
        {
            db.Teams.Add(new Team
            {
                Id = id,
                Name = name,
                ShortName = name[8..11].ToUpperInvariant(),
                IsBigSevenClub = false,
                Division = Competition.Championship,
                TeamType = TeamType.Club,
                CreatedAt = DateTime.UtcNow,
            });
        }
    }

    /// <summary>Adds an approved participant membership in the default group (does not SaveChanges).
    /// Per-group active/eliminated flags now live on the membership.</summary>
    public static void AddDefaultGroupMembership(AppDbContext db, Guid userId,
        GroupRole role = GroupRole.Participant, GroupUserStatus status = GroupUserStatus.Approved,
        bool isActive = true, bool isEliminated = false)
    {
        db.GroupUsers.Add(new GroupUser
        {
            Id = Guid.NewGuid(),
            GroupId = SeedIds.DefaultGroup,
            UserId = userId,
            Role = role,
            Status = status,
            IsActive = isActive,
            IsEliminated = isEliminated,
            ApprovedAt = status == GroupUserStatus.Approved ? DateTime.UtcNow : null,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        });
    }

    /// <summary>Reads a user's per-group eliminated flag in the default group.</summary>
    public static bool IsEliminatedInDefaultGroup(AppDbContext db, Guid userId)
        => db.GroupUsers.Single(gu => gu.GroupId == SeedIds.DefaultGroup && gu.UserId == userId).IsEliminated;

    /// <summary>Reads a user's per-group active flag in the default group.</summary>
    public static bool IsActiveInDefaultGroup(AppDbContext db, Guid userId)
        => db.GroupUsers.Single(gu => gu.GroupId == SeedIds.DefaultGroup && gu.UserId == userId).IsActive;
}
