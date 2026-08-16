using Microsoft.EntityFrameworkCore;
using Palpitao.Api.Common;
using Palpitao.Api.Data;
using Palpitao.Api.DTOs.Admin;
using Palpitao.Api.Entities;
using Palpitao.Api.Services.Audit;
using Palpitao.Api.Services.Groups;

namespace Palpitao.Api.Services.Ocr;

/// <inheritdoc cref="IOcrAliasService"/>
public class OcrAliasService : IOcrAliasService
{
    private readonly AppDbContext _db;
    private readonly IAuditService _audit;
    private readonly ICurrentGroupService _current;

    public OcrAliasService(AppDbContext db, IAuditService audit, ICurrentGroupService current)
    {
        _db = db;
        _audit = audit;
        _current = current;
    }

    public async Task<IReadOnlyDictionary<string, Guid>> GetForGroupAsync(
        Guid groupId, CancellationToken ct) =>
        await _db.OcrParticipantAliases
            .AsNoTracking()
            .Where(a => a.GroupId == groupId)
            .ToDictionaryAsync(a => a.Alias, a => a.UserId, ct);

    /// <summary>
    /// The admin has just told us who "Paraguaio" is by filing the rows against them; remembering
    /// it is the difference between fixing the same screenshot format once and fixing it every
    /// round.
    ///
    /// Only names the matcher could not already resolve on its own are stored, and only when every
    /// row bearing that name agrees on the participant — one image can carry two people, and a name
    /// that pointed at both would be a coin flip on the next import.
    /// </summary>
    public async Task<int> LearnAsync(
        ICollection<OcrPredictionCandidate> candidates,
        Guid groupId,
        Guid adminId,
        DateTime now,
        CancellationToken ct)
    {
        var participants = await GroupQueries.ActiveParticipants(_db, groupId).ToListAsync(ct);

        var unanimous = candidates
            .Where(c => !string.IsNullOrWhiteSpace(c.ParticipantNameRaw) && c.UserId is not null)
            .GroupBy(c => OcrTeamMatcher.NormalizeAlias(c.ParticipantNameRaw!), StringComparer.Ordinal)
            .Where(g => g.Select(c => c.UserId!.Value).Distinct().Count() == 1)
            .Select(g => (Key: g.Key, UserId: g.First().UserId!.Value, Raw: g.First().ParticipantNameRaw!))
            .Where(x => x.Key.Length > 0
                && OcrTeamMatcher.ResolveParticipant(x.Raw, participants) != x.UserId)
            .ToList();

        if (unanimous.Count == 0)
        {
            return 0;
        }

        var keys = unanimous.Select(x => x.Key).ToList();
        var stored = await _db.OcrParticipantAliases
            .Where(a => a.GroupId == groupId && keys.Contains(a.Alias))
            .ToDictionaryAsync(a => a.Alias, ct);

        foreach (var (key, userId, raw) in unanimous)
        {
            if (stored.TryGetValue(key, out var existing))
            {
                // The latest correction wins: an alias learned from junk that turns out to
                // belong to somebody else is re-pointed rather than left wrong forever.
                existing.UserId = userId;
                existing.AliasRaw = raw;
                existing.UpdatedAt = now;
                continue;
            }

            _db.OcrParticipantAliases.Add(NewAlias(groupId, userId, key, raw, now));
        }

        _audit.Add(adminId, "OcrParticipantAliasesLearned", nameof(OcrParticipantAlias), null,
            new { aliases = unanimous.Select(x => new { x.Raw, x.UserId }).ToList() });

        return unanimous.Count;
    }

    public async Task<List<OcrParticipantAliasDto>> ListAsync(CancellationToken ct)
    {
        var groupId = await _current.GetGroupIdAsync(ct);

        return await _db.OcrParticipantAliases
            .AsNoTracking()
            .Where(a => a.GroupId == groupId)
            .OrderBy(a => a.AliasRaw)
            .Select(a => new OcrParticipantAliasDto
            {
                Id = a.Id,
                Alias = a.Alias,
                AliasRaw = a.AliasRaw,
                UserId = a.UserId,
                // A real navigation, unlike OcrImportBatch.UploadedByUserId — no second query.
                UserName = a.User!.Name,
                CreatedAt = a.CreatedAt,
                UpdatedAt = a.UpdatedAt,
            })
            .ToListAsync(ct);
    }

    public async Task<OcrParticipantAliasDto> CreateAsync(
        CreateOcrParticipantAliasRequest request, Guid adminId, CancellationToken ct)
    {
        var groupId = await _current.GetGroupIdAsync(ct);
        var raw = (request.AliasRaw ?? string.Empty).Trim();
        var key = OcrTeamMatcher.NormalizeAlias(raw);
        if (key.Length == 0)
        {
            throw new BusinessRuleException("ocr.aliasEmpty");
        }

        if (raw.Length > OcrParticipantAlias.MaxAliasLength)
        {
            throw new BusinessRuleException("ocr.aliasTooLong");
        }

        await EnsureParticipantAsync(request.UserId, groupId, ct);

        var clash = await _db.OcrParticipantAliases
            .AnyAsync(a => a.GroupId == groupId && a.Alias == key, ct);
        if (clash)
        {
            // Deliberately not an upsert: the row is already on the admin's screen, and silently
            // re-pointing it would hide that somebody else's mapping just changed.
            throw new BusinessRuleException("ocr.aliasAlreadyExists");
        }

        var now = DateTime.UtcNow;
        var alias = NewAlias(groupId, request.UserId, key, raw, now);
        _db.OcrParticipantAliases.Add(alias);

        _audit.Add(adminId, "OcrParticipantAliasCreated", nameof(OcrParticipantAlias),
            alias.Id.ToString(), new { alias.AliasRaw, alias.UserId });
        await _db.SaveChangesAsync(ct);

        return await GetDtoAsync(alias.Id, ct);
    }

    public async Task<OcrParticipantAliasDto> UpdateAsync(
        Guid id, UpdateOcrParticipantAliasRequest request, Guid adminId, CancellationToken ct)
    {
        var groupId = await _current.GetGroupIdAsync(ct);
        var alias = await FindInGroupAsync(id, groupId, ct);
        await EnsureParticipantAsync(request.UserId, groupId, ct);

        var from = alias.UserId;
        alias.UserId = request.UserId;
        alias.UpdatedAt = DateTime.UtcNow;

        _audit.Add(adminId, "OcrParticipantAliasUpdated", nameof(OcrParticipantAlias), id.ToString(),
            new { alias.AliasRaw, from, to = request.UserId });
        await _db.SaveChangesAsync(ct);

        return await GetDtoAsync(id, ct);
    }

    public async Task DeleteAsync(Guid id, Guid adminId, CancellationToken ct)
    {
        var groupId = await _current.GetGroupIdAsync(ct);
        var alias = await FindInGroupAsync(id, groupId, ct);

        _db.OcrParticipantAliases.Remove(alias);
        _audit.Add(adminId, "OcrParticipantAliasDeleted", nameof(OcrParticipantAlias), id.ToString(),
            new { alias.AliasRaw, alias.UserId });
        await _db.SaveChangesAsync(ct);
    }

    private static OcrParticipantAlias NewAlias(
        Guid groupId, Guid userId, string key, string raw, DateTime now) => new()
    {
        Id = Guid.NewGuid(),
        GroupId = groupId,
        UserId = userId,
        Alias = key,
        AliasRaw = raw,
        CreatedAt = now,
        UpdatedAt = now,
    };

    /// <summary>Loads an alias of the current group, or 404 — never another tenant's row.</summary>
    private async Task<OcrParticipantAlias> FindInGroupAsync(Guid id, Guid groupId, CancellationToken ct) =>
        await _db.OcrParticipantAliases.FirstOrDefaultAsync(a => a.Id == id && a.GroupId == groupId, ct)
        ?? throw new NotFoundException("notFound.ocrAlias");

    /// <summary>
    /// The target must be on this group's roster. Eliminated members count: an alias may well be
    /// older than their elimination, and the import already ignores an alias whose participant is
    /// not in the roster it was handed.
    /// </summary>
    private async Task EnsureParticipantAsync(Guid userId, Guid groupId, CancellationToken ct)
    {
        var isParticipant = await GroupQueries.AllParticipants(_db, groupId)
            .AnyAsync(u => u.Id == userId, ct);
        if (!isParticipant)
        {
            throw new NotFoundException("notFound.participant");
        }
    }

    private async Task<OcrParticipantAliasDto> GetDtoAsync(Guid id, CancellationToken ct) =>
        await _db.OcrParticipantAliases
            .AsNoTracking()
            .Where(a => a.Id == id)
            .Select(a => new OcrParticipantAliasDto
            {
                Id = a.Id,
                Alias = a.Alias,
                AliasRaw = a.AliasRaw,
                UserId = a.UserId,
                UserName = a.User!.Name,
                CreatedAt = a.CreatedAt,
                UpdatedAt = a.UpdatedAt,
            })
            .FirstAsync(ct);
}
