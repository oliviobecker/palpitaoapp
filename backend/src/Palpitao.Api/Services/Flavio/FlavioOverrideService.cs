using System.Data;
using Microsoft.EntityFrameworkCore;
using Palpitao.Api.Common;
using Palpitao.Api.Data;
using Palpitao.Api.DTOs.Admin;
using Palpitao.Api.Entities;
using Palpitao.Api.Enums;
using Palpitao.Api.Services.Audit;
using Palpitao.Api.Services.Groups;
using Palpitao.Api.Services.Scoring;

namespace Palpitao.Api.Services.Flavio;

public class FlavioOverrideService(
    AppDbContext db, ICurrentGroupService current, IAuditService audit,
    IFlavioRuleService flavio, ISeasonScoringConfigService config, IRoundScoringService scoring)
{
    private async Task<Round> GetRoundAsync(Guid roundId, CancellationToken ct)
    {
        var groupId = await current.GetGroupIdAsync(ct);
        return await db.Rounds.Include(r => r.Matches).Include(r => r.Season)
            .FirstOrDefaultAsync(r => r.Id == roundId && r.GroupId == groupId, ct)
            ?? throw new NotFoundException("notFound.round");
    }

    public async Task<RoundFlavioOverridesDto> GetAsync(Guid roundId, CancellationToken ct)
    {
        var round = await GetRoundAsync(roundId, ct);
        var rules = await config.GetRuleParamsAsync(round.SeasonId, ct);
        var applies = flavio.ShouldApplyFlavioRule(round, round.Season!.TournamentType, rules.FlavioFromRound);
        var targets = round.Season.TournamentType == TournamentType.FifaWorldCup
            ? (round.FlavioRuleTargetUserId is Guid target ? new[] { target } : Array.Empty<Guid>())
            : await flavio.GetLeadersBeforeRoundAsync(roundId, ct);
        var participants = await GroupQueries.AllParticipants(db, round.GroupId)
            .OrderBy(p => p.Name).Select(p => new { p.Id, p.Name }).ToListAsync(ct);
        var submitted = await db.Predictions.Where(p => p.RoundId == roundId)
            .GroupBy(p => p.UserId).Select(g => new { UserId = g.Key, At = g.Max(p => p.SubmittedAt) })
            .ToDictionaryAsync(p => p.UserId, p => p.At, ct);
        var results = await db.RoundParticipantResults.Where(r => r.RoundId == roundId).ToDictionaryAsync(r => r.UserId, ct);
        var overrides = await db.FlavioOverrides.Where(o => o.RoundId == roundId).ToDictionaryAsync(o => o.UserId, ct);
        return new(roundId, applies, FlavioRuleService.TryComputeEffectiveDeadline(round),
            participants.Select(p =>
            {
                results.TryGetValue(p.Id, out var result);
                overrides.TryGetValue(p.Id, out var adjustment);
                return new FlavioParticipantDto(p.Id, p.Name, applies && targets.Contains(p.Id),
                    submitted.TryGetValue(p.Id, out var at) ? at : null,
                    result?.GrossPoints, result?.FinalPoints, result?.FlavioRuleApplied ?? false,
                    adjustment?.IsExempt ?? false, adjustment?.Justification,
                    adjustment?.UpdatedByUserId, adjustment?.UpdatedAt);
            }).ToList());
    }

    public async Task SaveAsync(Guid roundId, FlavioOverrideRequest request, Guid adminId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Justification) || request.Justification.Length > 500)
            throw new BusinessRuleException("flavio.justificationRequired");

        await using var tx = db.Database.IsRelational()
            ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct) : null;
        var round = await GetRoundAsync(roundId, ct);
        if (round.Status is not (RoundStatus.Published or RoundStatus.Locked or RoundStatus.Scored))
            throw new BusinessRuleException("flavio.overrideRoundStatus");
        if (!await GroupQueries.AllParticipants(db, round.GroupId).AnyAsync(p => p.Id == request.UserId, ct))
            throw new NotFoundException("notFound.participant");

        var adjustment = await db.FlavioOverrides
            .FirstOrDefaultAsync(o => o.RoundId == roundId && o.UserId == request.UserId, ct);
        var previous = adjustment is null ? null : new { adjustment.IsExempt, adjustment.Justification };
        var now = DateTime.UtcNow;
        if (adjustment is null)
        {
            adjustment = new FlavioOverride
            {
                Id = Guid.NewGuid(), RoundId = roundId, UserId = request.UserId,
                CreatedAt = now, CreatedByUserId = adminId,
            };
            db.FlavioOverrides.Add(adjustment);
        }
        adjustment.IsExempt = request.IsExempt;
        adjustment.Justification = request.Justification.Trim();
        adjustment.UpdatedByUserId = adminId;
        adjustment.UpdatedAt = now;
        audit.Add(adminId, "FlavioOverrideChanged", nameof(Round), roundId.ToString(),
            new { request.UserId, previous, adjustment.IsExempt, adjustment.Justification }, round.GroupId);
        await db.SaveChangesAsync(ct);
        if (round.Status == RoundStatus.Scored)
            await scoring.RecalculateSeasonAsync(round.SeasonId, adminId, ct);
        if (tx is not null) await tx.CommitAsync(ct);
    }
}
