using Microsoft.EntityFrameworkCore;
using Palpitao.Api.Common;
using Palpitao.Api.Data;
using Palpitao.Api.DTOs.Admin;
using Palpitao.Api.DTOs.Predictions;
using Palpitao.Api.Entities;
using Palpitao.Api.Enums;
using Palpitao.Api.Services.Audit;
using Palpitao.Api.Services.Groups;

namespace Palpitao.Api.Services.AdminPredictions;

public class AdminPredictionService : IAdminPredictionService
{
    private readonly AppDbContext _db;
    private readonly IAuditService _audit;
    private readonly ICurrentGroupService _current;

    public AdminPredictionService(AppDbContext db, IAuditService audit, ICurrentGroupService current)
    {
        _db = db;
        _audit = audit;
        _current = current;
    }

    public async Task SaveManualAsync(Guid roundId, ManualPredictionRequest request, Guid adminId, CancellationToken ct)
    {
        var groupId = await _current.GetGroupIdAsync(ct);
        var round = await _db.Rounds.Include(r => r.Matches).FirstOrDefaultAsync(r => r.Id == roundId && r.GroupId == groupId, ct)
            ?? throw new NotFoundException("notFound.round");

        var membership = await GroupQueries.ApprovedMemberships(_db, groupId)
            .FirstOrDefaultAsync(gu => gu.UserId == request.UserId, ct)
            ?? throw new NotFoundException("notFound.participant");

        var hasJustification = !string.IsNullOrWhiteSpace(request.Justification);

        if (!membership.IsActive)
        {
            throw new BusinessRuleException("participant.inactive");
        }

        if (membership.IsEliminated && !(request.AllowAfterDeadline && hasJustification))
        {
            throw new BusinessRuleException("adminPrediction.eliminatedNeedsOverride");
        }

        EnsureRoundOpen(round, request.AllowAfterDeadline, hasJustification);
        ValidateBatch(round, request.Predictions);

        var existing = await _db.Predictions
            .Where(p => p.RoundId == roundId && p.UserId == request.UserId)
            .ToDictionaryAsync(p => p.RoundMatchId, ct);

        if (existing.Count > 0 && !request.OverwriteExisting)
        {
            throw new BusinessRuleException("adminPrediction.alreadyHasPredictions");
        }

        var now = DateTime.UtcNow;
        foreach (var item in request.Predictions)
        {
            if (existing.TryGetValue(item.RoundMatchId, out var prediction))
            {
                prediction.PredictedHomeScore = item.PredictedHomeScore;
                prediction.PredictedAwayScore = item.PredictedAwayScore;
                prediction.UpdatedAt = now;
                prediction.Source = PredictionSource.AdminManual;
                prediction.UpdatedByUserId = adminId;
            }
            else
            {
                _db.Predictions.Add(new Prediction
                {
                    Id = Guid.NewGuid(),
                    RoundId = roundId,
                    RoundMatchId = item.RoundMatchId,
                    UserId = request.UserId,
                    PredictedHomeScore = item.PredictedHomeScore,
                    PredictedAwayScore = item.PredictedAwayScore,
                    SubmittedAt = now,
                    Source = PredictionSource.AdminManual,
                    CreatedByUserId = adminId,
                });
            }
        }

        _audit.Add(adminId, existing.Count > 0 ? "AdminPredictionOverwritten" : "AdminPredictionCreated",
            nameof(Prediction), roundId.ToString(),
            new { request.UserId, count = request.Predictions.Count, request.OverwriteExisting, request.AllowAfterDeadline, request.Justification });

        await _db.SaveChangesAsync(ct);
    }

    public async Task<AdminParticipantPredictionsDto> GetParticipantPredictionsAsync(
        Guid roundId, Guid userId, CancellationToken ct)
    {
        var groupId = await _current.GetGroupIdAsync(ct);
        if (!await _db.Rounds.AnyAsync(r => r.Id == roundId && r.GroupId == groupId, ct))
        {
            throw new NotFoundException("notFound.round");
        }

        if (!await GroupQueries.AllParticipants(_db, groupId).AnyAsync(u => u.Id == userId, ct))
        {
            throw new NotFoundException("notFound.participant");
        }

        var predictions = await _db.Predictions
            .Where(p => p.RoundId == roundId && p.UserId == userId)
            .OrderBy(p => p.RoundMatchId)
            .Select(p => new AdminPredictionItemDto
            {
                RoundMatchId = p.RoundMatchId,
                PredictedHomeScore = p.PredictedHomeScore,
                PredictedAwayScore = p.PredictedAwayScore,
                Source = p.Source,
                UpdatedAt = p.UpdatedAt,
            })
            .ToListAsync(ct);

        return new AdminParticipantPredictionsDto
        {
            RoundId = roundId,
            UserId = userId,
            HasPredictions = predictions.Count > 0,
            Predictions = predictions,
        };
    }

    public async Task<PredictionCoverageDto> GetCoverageAsync(Guid roundId, CancellationToken ct)
    {
        var groupId = await _current.GetGroupIdAsync(ct);
        var matchCount = await _db.RoundMatches
            .Where(m => m.RoundId == roundId && m.Round!.GroupId == groupId)
            .CountAsync(ct);
        if (matchCount == 0 && !await _db.Rounds.AnyAsync(r => r.Id == roundId && r.GroupId == groupId, ct))
        {
            throw new NotFoundException("notFound.round");
        }

        var participants = await GroupQueries.ActiveParticipants(_db, groupId)
            .OrderBy(u => u.Name)
            .Select(u => new { u.Id, u.Name })
            .ToListAsync(ct);

        var predictedCounts = await _db.Predictions
            .Where(p => p.RoundId == roundId)
            .GroupBy(p => p.UserId)
            .Select(g => new { UserId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.UserId, x => x.Count, ct);

        var missing = participants
            .Select(u => new PredictionCoverageParticipantDto
            {
                UserId = u.Id,
                Name = u.Name,
                PredictedCount = predictedCounts.GetValueOrDefault(u.Id),
            })
            .Where(p => matchCount == 0 || p.PredictedCount < matchCount)
            .ToList();

        return new PredictionCoverageDto
        {
            RoundId = roundId,
            MatchCount = matchCount,
            TotalParticipants = participants.Count,
            CompleteParticipants = participants.Count - missing.Count,
            Missing = missing,
        };
    }

    private static void EnsureRoundOpen(Round round, bool allowAfterDeadline, bool hasJustification)
    {
        var openOnTime = round.Status == RoundStatus.Published
            && round.FirstMatchStartsAt is not null
            && DateTime.UtcNow < round.FirstMatchStartsAt.Value;

        if (openOnTime)
        {
            return;
        }

        // Round closed (Locked/Scored/Cancelled) or past deadline -> needs override.
        if (!(allowAfterDeadline && hasJustification))
        {
            throw new BusinessRuleException("adminPrediction.roundNotOpenOverride");
        }
    }

    private static void ValidateBatch(Round round, List<PredictionItemRequest> items)
    {
        if (items.Count == 0)
        {
            throw new BusinessRuleException("prediction.allMatchesRequired");
        }

        if (items.Select(i => i.RoundMatchId).Distinct().Count() != items.Count)
        {
            throw new BusinessRuleException("prediction.noDuplicates");
        }

        var roundMatchIds = round.Matches.Select(m => m.Id).ToHashSet();
        if (items.Any(i => !roundMatchIds.Contains(i.RoundMatchId)))
        {
            throw new BusinessRuleException("prediction.matchNotInRound");
        }

        if (items.Count != roundMatchIds.Count)
        {
            throw new BusinessRuleException("prediction.allMatchesRequired");
        }

        if (items.Any(i => i.PredictedHomeScore < 0 || i.PredictedAwayScore < 0))
        {
            throw new BusinessRuleException("prediction.negativeScore");
        }
    }
}
