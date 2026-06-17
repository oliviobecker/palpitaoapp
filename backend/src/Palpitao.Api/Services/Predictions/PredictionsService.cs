using Microsoft.EntityFrameworkCore;
using Palpitao.Api.Common;
using Palpitao.Api.Data;
using Palpitao.Api.DTOs.Predictions;
using Palpitao.Api.Entities;
using Palpitao.Api.Enums;
using Palpitao.Api.Services.Audit;
using Palpitao.Api.Services.Groups;
using Sentry;

namespace Palpitao.Api.Services.Predictions;

public class PredictionsService : IPredictionsService
{
    private readonly AppDbContext _db;
    private readonly IAuditService _audit;
    private readonly ICurrentGroupService _current;

    public PredictionsService(AppDbContext db, IAuditService audit, ICurrentGroupService current)
    {
        _db = db;
        _audit = audit;
        _current = current;
    }

    public async Task<MyPredictionsDto> GetMyPredictionsAsync(Guid roundId, Guid userId, CancellationToken ct)
    {
        var groupId = await _current.GetGroupIdAsync(ct);
        var round = await _db.Rounds.FirstOrDefaultAsync(r => r.Id == roundId && r.GroupId == groupId, ct)
            ?? throw new NotFoundException("notFound.round");

        var predictions = await _db.Predictions
            .Where(p => p.RoundId == roundId && p.UserId == userId)
            .OrderBy(p => p.SubmittedAt)
            .Select(p => new PredictionDto
            {
                RoundMatchId = p.RoundMatchId,
                PredictedHomeScore = p.PredictedHomeScore,
                PredictedAwayScore = p.PredictedAwayScore,
                SubmittedAt = p.SubmittedAt,
                UpdatedAt = p.UpdatedAt,
            })
            .ToListAsync(ct);

        return new MyPredictionsDto
        {
            RoundId = round.Id,
            Status = round.Status,
            FirstMatchStartsAt = round.FirstMatchStartsAt,
            Predictions = predictions,
        };
    }

    public async Task<MyPredictionsDto> SavePredictionsAsync(
        Guid roundId, Guid userId, SavePredictionsRequest request, bool isEdit, CancellationToken ct)
    {
        var groupId = await _current.GetGroupIdAsync(ct);
        var round = await _db.Rounds
            .Include(r => r.Matches)
            .FirstOrDefaultAsync(r => r.Id == roundId && r.GroupId == groupId, ct)
            ?? throw new NotFoundException("notFound.round");

        var membership = await GroupQueries.ApprovedMemberships(_db, groupId)
            .FirstOrDefaultAsync(gu => gu.UserId == userId, ct)
            ?? throw new NotFoundException("notFound.user");

        EnsureCanParticipate(membership);
        EnsureRoundOpenForPredictions(round);
        ValidateBatch(round, request);

        var now = DateTime.UtcNow;
        var existing = await _db.Predictions
            .Where(p => p.RoundId == roundId && p.UserId == userId)
            .ToDictionaryAsync(p => p.RoundMatchId, ct);

        foreach (var item in request.Predictions)
        {
            if (existing.TryGetValue(item.RoundMatchId, out var prediction))
            {
                prediction.PredictedHomeScore = item.PredictedHomeScore;
                prediction.PredictedAwayScore = item.PredictedAwayScore;
                prediction.UpdatedAt = now;
                prediction.Source = PredictionSource.Participant;
                prediction.UpdatedByUserId = userId;
            }
            else
            {
                _db.Predictions.Add(new Prediction
                {
                    Id = Guid.NewGuid(),
                    RoundId = roundId,
                    RoundMatchId = item.RoundMatchId,
                    UserId = userId,
                    PredictedHomeScore = item.PredictedHomeScore,
                    PredictedAwayScore = item.PredictedAwayScore,
                    SubmittedAt = now,
                    Source = PredictionSource.Participant,
                    CreatedByUserId = userId,
                });
            }
        }

        _audit.Add(userId, isEdit ? "PredictionsUpdated" : "PredictionsCreated", nameof(Prediction),
            roundId.ToString(), new { count = request.Predictions.Count });
        await _db.SaveChangesAsync(ct);
        SentrySdk.AddBreadcrumb("Predictions saved.", "predictions", data: new Dictionary<string, string>
        {
            ["roundId"] = roundId.ToString(),
            ["count"] = request.Predictions.Count.ToString(),
            ["isEdit"] = isEdit.ToString(),
        });

        return await GetMyPredictionsAsync(roundId, userId, ct);
    }

    public async Task<MirrorDto> GetMirrorAsync(Guid roundId, Guid requestingUserId, CancellationToken ct)
    {
        var groupId = await _current.GetGroupIdAsync(ct);
        var round = await _db.Rounds
            .Include(r => r.Matches).ThenInclude(m => m.HomeTeam)
            .Include(r => r.Matches).ThenInclude(m => m.AwayTeam)
            .FirstOrDefaultAsync(r => r.Id == roundId && r.GroupId == groupId, ct)
            ?? throw new NotFoundException("notFound.round");

        // Before the lock, predictions stay private — the mirror is only released
        // after the round is locked (or already scored).
        if (round.Status is not (RoundStatus.Locked or RoundStatus.Scored))
        {
            throw new BusinessRuleException("mirror.afterLockOnly");
        }

        var matches = round.Matches
            .OrderBy(m => m.Order)
            .ThenBy(m => m.StartsAt)
            .ToList();

        // Per-group elimination flag travels on the membership.
        var participants = await GroupQueries.ApprovedMemberships(_db, round.GroupId)
            .Join(_db.Users, gu => gu.UserId, u => u.Id, (gu, u) => new { u.Id, u.Name, gu.IsEliminated })
            .OrderBy(p => p.Name)
            .ToListAsync(ct);

        var predictions = await _db.Predictions
            .Where(p => p.RoundId == roundId)
            .ToListAsync(ct);

        var mirror = new MirrorDto
        {
            RoundId = round.Id,
            Status = round.Status,
            Matches = matches.Select(m => new MirrorMatchDto
            {
                RoundMatchId = m.Id,
                Competition = m.Competition,
                Phase = m.Phase,
                HomeTeamName = m.HomeTeam?.Name ?? string.Empty,
                AwayTeamName = m.AwayTeam?.Name ?? string.Empty,
                StartsAt = m.StartsAt,
            }).ToList(),
        };

        foreach (var participant in participants)
        {
            var userPredictions = predictions.Where(p => p.UserId == participant.Id).ToList();

            mirror.Participants.Add(new MirrorParticipantDto
            {
                UserId = participant.Id,
                Name = participant.Name,
                // Absent when the participant did not predict every match of the round.
                IsAbsent = userPredictions.Count < matches.Count,
                IsEliminated = participant.IsEliminated,
                // Filled by the Flávio rule module (later phase).
                FlavioRuleApplied = false,
                Predictions = userPredictions.Select(p => new MirrorPredictionDto
                {
                    RoundMatchId = p.RoundMatchId,
                    PredictedHomeScore = p.PredictedHomeScore,
                    PredictedAwayScore = p.PredictedAwayScore,
                    SubmittedAt = p.SubmittedAt,
                }).ToList(),
            });
        }

        return mirror;
    }

    // -----------------------------------------------------------------------
    // Validation
    // -----------------------------------------------------------------------
    private static void EnsureCanParticipate(GroupUser membership)
    {
        if (!membership.IsActive)
        {
            throw new BusinessRuleException("prediction.inactiveCannotPredict");
        }

        if (membership.IsEliminated)
        {
            throw new BusinessRuleException("prediction.eliminatedCannotPredict");
        }
    }

    private static void EnsureRoundOpenForPredictions(Round round)
    {
        switch (round.Status)
        {
            case RoundStatus.Draft:
                throw new BusinessRuleException("prediction.roundNotOpenYet");
            case RoundStatus.Locked:
                throw new BusinessRuleException("prediction.roundLocked");
            case RoundStatus.Scored:
                throw new BusinessRuleException("prediction.roundScored");
            case RoundStatus.Cancelled:
                throw new BusinessRuleException("prediction.roundCancelled");
            case RoundStatus.Published:
                break;
        }

        if (round.FirstMatchStartsAt is null || DateTime.UtcNow >= round.FirstMatchStartsAt.Value)
        {
            throw new BusinessRuleException("prediction.deadlinePassed");
        }
    }

    private static void ValidateBatch(Round round, SavePredictionsRequest request)
    {
        var items = request.Predictions ?? new List<PredictionItemRequest>();

        if (items.Count == 0)
        {
            throw new BusinessRuleException("prediction.allMatchesRequired");
        }

        // No duplicated matches in the payload.
        var distinctIds = items.Select(i => i.RoundMatchId).Distinct().Count();
        if (distinctIds != items.Count)
        {
            throw new BusinessRuleException("prediction.noDuplicates");
        }

        var roundMatchIds = round.Matches.Select(m => m.Id).ToHashSet();

        // Every submitted match must belong to the round.
        if (items.Any(i => !roundMatchIds.Contains(i.RoundMatchId)))
        {
            throw new BusinessRuleException("prediction.matchNotInRound");
        }

        // Every match of the round must be present (complete submission).
        if (items.Count != roundMatchIds.Count)
        {
            throw new BusinessRuleException("prediction.allMatchesRequired");
        }

        // Scores must be non-negative integers (the type already enforces integer).
        if (items.Any(i => i.PredictedHomeScore < 0 || i.PredictedAwayScore < 0))
        {
            throw new BusinessRuleException("prediction.negativeScore");
        }
    }
}
