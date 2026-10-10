using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Palpitao.Api.Tests.TestSupport;
using Palpitao.Application.Audit;
using Palpitao.Application.Common.Exceptions;
using Palpitao.Application.Ocr;
using Palpitao.Domain.Common;
using Palpitao.Domain.Entities;
using Palpitao.Domain.Enums;
using Palpitao.Infrastructure.Persistence;
using Palpitao.Infrastructure.Persistence.Seed;

namespace Palpitao.Api.Tests.Ocr;

/// <summary>
/// The admin side of the learned aliases: what the import writes automatically, an admin can
/// inspect, re-point, remove, or teach up front. The learning path itself is covered by
/// <see cref="PredictionImportServiceTests"/> through ConfirmAsync.
/// </summary>
public class OcrAliasServiceTests
{
    private static readonly Guid Admin = SeedIds.AdminUser;
    private static readonly Guid OtherGroup = Guid.Parse("88888888-8888-8888-8888-888888888802");
    private static readonly CancellationToken Ct = CancellationToken.None;

    [Fact]
    public async Task Lists_the_groups_aliases_with_the_participant_name()
    {
        using var db = CreateContext();
        var tb = SeedParticipant(db, "TB");
        SeedAlias(db, SeedIds.DefaultGroup, tb, "Careca");

        var list = await CreateService(db).ListAsync(Ct);

        var alias = Assert.Single(list);
        Assert.Equal("Careca", alias.AliasRaw);
        Assert.Equal("careca", alias.Alias);
        Assert.Equal(tb, alias.UserId);
        Assert.Equal("TB", alias.UserName);
    }

    [Fact]
    public async Task Creates_an_alias_under_its_normalized_key()
    {
        using var db = CreateContext();
        var tb = SeedParticipant(db, "TB");

        var created = await CreateService(db).CreateAsync(
            new CreateOcrParticipantAliasRequest { AliasRaw = "  Caréca ", UserId = tb }, Admin, Ct);

        // The raw form is what the admin recognises; the key is what a screenshot has to hit.
        Assert.Equal("Caréca", created.AliasRaw);
        Assert.Equal("careca", created.Alias);
        Assert.Equal(tb, created.UserId);
        Assert.Equal("TB", created.UserName);
        Assert.Contains(await db.AuditLogs.ToListAsync(Ct), a => a.Action == "OcrParticipantAliasCreated");
    }

    [Fact]
    public async Task A_manually_created_alias_is_used_by_the_next_import()
    {
        // The point of the screen: teach it once, before any screenshot has needed it.
        using var db = CreateContext();
        var tb = SeedParticipant(db, "TB");
        var service = CreateService(db);
        await service.CreateAsync(
            new CreateOcrParticipantAliasRequest { AliasRaw = "Careca", UserId = tb }, Admin, Ct);

        var aliases = await service.GetForGroupAsync(SeedIds.DefaultGroup, Ct);

        Assert.Equal(tb, aliases[OcrTeamMatcher.NormalizeAlias("CARECA")]);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Refuses_an_empty_alias(string raw)
    {
        using var db = CreateContext();
        var tb = SeedParticipant(db, "TB");

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => CreateService(db).CreateAsync(
            new CreateOcrParticipantAliasRequest { AliasRaw = raw, UserId = tb }, Admin, Ct));

        Assert.Equal("ocr.aliasEmpty", ex.Key);
    }

    [Fact]
    public async Task Refuses_a_duplicate_rather_than_silently_repointing_it()
    {
        // The existing row is already on the admin's screen; overwriting it here would hide that
        // somebody else's mapping just changed.
        using var db = CreateContext();
        var tb = SeedParticipant(db, "TB");
        var flavio = SeedParticipant(db, "Flavio");
        SeedAlias(db, SeedIds.DefaultGroup, tb, "Careca");

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => CreateService(db).CreateAsync(
            new CreateOcrParticipantAliasRequest { AliasRaw = "CARECA", UserId = flavio }, Admin, Ct));

        Assert.Equal("ocr.aliasAlreadyExists", ex.Key);
        Assert.Equal(tb, (await db.OcrParticipantAliases.SingleAsync(Ct)).UserId);
    }

    [Fact]
    public async Task Refuses_a_target_that_is_not_a_participant_of_the_group()
    {
        using var db = CreateContext();
        var stranger = SeedUser(db, "Estranho"); // no membership

        var ex = await Assert.ThrowsAsync<NotFoundException>(() => CreateService(db).CreateAsync(
            new CreateOcrParticipantAliasRequest { AliasRaw = "Careca", UserId = stranger }, Admin, Ct));

        Assert.Equal("notFound.participant", ex.Key);
        Assert.Empty(await db.OcrParticipantAliases.ToListAsync(Ct));
    }

    [Fact]
    public async Task Repoints_an_alias_at_another_participant()
    {
        using var db = CreateContext();
        var tb = SeedParticipant(db, "TB");
        var flavio = SeedParticipant(db, "Flavio");
        var aliasId = SeedAlias(db, SeedIds.DefaultGroup, tb, "nAc");

        var updated = await CreateService(db).UpdateAsync(
            aliasId, new UpdateOcrParticipantAliasRequest { UserId = flavio }, Admin, Ct);

        Assert.Equal(flavio, updated.UserId);
        Assert.Equal("Flavio", updated.UserName);
        // The key is untouched — re-pointing must not re-file what the alias matches.
        Assert.Equal("nac", updated.Alias);
        Assert.Contains(await db.AuditLogs.ToListAsync(Ct), a => a.Action == "OcrParticipantAliasUpdated");
    }

    [Fact]
    public async Task Deletes_an_alias()
    {
        using var db = CreateContext();
        var tb = SeedParticipant(db, "TB");
        var aliasId = SeedAlias(db, SeedIds.DefaultGroup, tb, "nAc");

        await CreateService(db).DeleteAsync(aliasId, Admin, Ct);

        Assert.Empty(await db.OcrParticipantAliases.ToListAsync(Ct));
        Assert.Contains(await db.AuditLogs.ToListAsync(Ct), a => a.Action == "OcrParticipantAliasDeleted");
    }

    [Fact]
    public async Task Another_groups_alias_is_invisible_and_untouchable()
    {
        // A nickname means different people in different pools, so this is not a cosmetic leak.
        using var db = CreateContext();
        SeedOtherGroup(db);
        var outsider = SeedParticipant(db, "TB");
        var foreignAlias = SeedAlias(db, OtherGroup, outsider, "Careca");
        var service = CreateService(db);

        Assert.Empty(await service.ListAsync(Ct));
        Assert.Empty(await service.GetForGroupAsync(SeedIds.DefaultGroup, Ct));

        var update = await Assert.ThrowsAsync<NotFoundException>(() => service.UpdateAsync(
            foreignAlias, new UpdateOcrParticipantAliasRequest { UserId = outsider }, Admin, Ct));
        Assert.Equal("notFound.ocrAlias", update.Key);

        var delete = await Assert.ThrowsAsync<NotFoundException>(
            () => service.DeleteAsync(foreignAlias, Admin, Ct));
        Assert.Equal("notFound.ocrAlias", delete.Key);

        Assert.Equal(1, await db.OcrParticipantAliases.CountAsync(Ct));
    }

    [Fact]
    public async Task The_same_alias_may_mean_different_people_in_different_groups()
    {
        using var db = CreateContext();
        SeedOtherGroup(db);
        var here = SeedParticipant(db, "TB");
        var there = SeedParticipant(db, "Outro");
        SeedAlias(db, OtherGroup, there, "Careca");

        var created = await CreateService(db).CreateAsync(
            new CreateOcrParticipantAliasRequest { AliasRaw = "Careca", UserId = here }, Admin, Ct);

        // The unique index is (GroupId, Alias) — the other group's row must not block this one.
        Assert.Equal(here, created.UserId);
        Assert.Equal(2, await db.OcrParticipantAliases.CountAsync(Ct));
    }

    // --- helpers ------------------------------------------------------------

    private static OcrAliasService CreateService(AppDbContext db)
    {
        var current = new FakeCurrentGroupService();
        return new OcrAliasService(db, new AuditService(db), current);
    }

    private static AppDbContext CreateContext()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options;
        var db = new AppDbContext(options);
        db.Database.EnsureCreated();
        return db;
    }

    private static void SeedOtherGroup(AppDbContext db)
    {
        db.Groups.Add(new Group
        {
            Id = OtherGroup,
            Name = "Outro bolão",
            Slug = "outro-bolao",
            CreatedByUserId = SeedIds.AdminUser,
            OwnerUserId = SeedIds.AdminUser,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        });
        db.SaveChanges();
    }

    private static Guid SeedUser(AppDbContext db, string name)
    {
        var id = Guid.NewGuid();
        db.Users.Add(new User
        {
            Id = id,
            Name = name,
            Email = $"{id}@x.com",
            PasswordHash = "x",
            Role = UserRole.Participant,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        });
        db.SaveChanges();
        return id;
    }

    private static Guid SeedParticipant(AppDbContext db, string name)
    {
        var id = SeedUser(db, name);
        TestSeed.AddDefaultGroupMembership(db, id);
        db.SaveChanges();
        return id;
    }

    private static Guid SeedAlias(AppDbContext db, Guid groupId, Guid userId, string raw)
    {
        var alias = new OcrParticipantAlias
        {
            Id = Guid.NewGuid(),
            GroupId = groupId,
            UserId = userId,
            Alias = OcrTeamMatcher.NormalizeAlias(raw),
            AliasRaw = raw,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        db.OcrParticipantAliases.Add(alias);
        db.SaveChanges();
        return alias.Id;
    }
}
