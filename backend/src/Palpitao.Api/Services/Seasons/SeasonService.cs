using Microsoft.EntityFrameworkCore;
using Palpitao.Api.Common;
using Palpitao.Api.Data;
using Palpitao.Api.DTOs.Seasons;
using Palpitao.Api.Entities;
using Palpitao.Api.Enums;
using Palpitao.Api.Services.Audit;
using Palpitao.Api.Services.Groups;

namespace Palpitao.Api.Services.Seasons;

public class SeasonService : ISeasonService
{
    private readonly AppDbContext _db;
    private readonly IAuditService _audit;
    private readonly ICurrentGroupService _current;

    public SeasonService(AppDbContext db, IAuditService audit, ICurrentGroupService current)
    {
        _db = db;
        _audit = audit;
        _current = current;
    }

    public async Task<IReadOnlyList<SeasonDto>> ListAsync(CancellationToken ct)
    {
        var groupId = await _current.GetGroupIdAsync(ct);
        return await _db.Seasons
            .Where(s => s.GroupId == groupId)
            .OrderByDescending(s => s.StartDate)
            .Select(ProjectExpr)
            .ToListAsync(ct);
    }

    public async Task<SeasonDto?> GetActiveAsync(CancellationToken ct)
    {
        var groupId = await _current.GetGroupIdAsync(ct);
        return await _db.Seasons
            .Where(s => s.IsActive && s.GroupId == groupId)
            .Select(ProjectExpr)
            .FirstOrDefaultAsync(ct);
    }

    /// <summary>Server-side projection including the "has participant predictions" subquery.</summary>
    private System.Linq.Expressions.Expression<Func<Season, SeasonDto>> ProjectExpr => s => new SeasonDto
    {
        Id = s.Id,
        Name = s.Name,
        StartDate = s.StartDate,
        EndDate = s.EndDate,
        IsActive = s.IsActive,
        AllowParticipantsToViewOthersPredictions = s.AllowParticipantsToViewOthersPredictions,
        AllowParticipantsToSubmitPredictions = s.AllowParticipantsToSubmitPredictions,
        HasParticipantPredictions = _db.Predictions.Any(
            p => p.Source == PredictionSource.Participant
                && _db.Rounds.Any(r => r.Id == p.RoundId && r.SeasonId == s.Id)),
    };

    public async Task<SeasonDto> CreateAsync(SeasonRequest request, Guid actingUserId, CancellationToken ct)
    {
        ValidateDates(request);

        var groupId = await _current.GetGroupIdAsync(ct);
        var season = new Season
        {
            Id = Guid.NewGuid(),
            GroupId = groupId,
            Name = request.Name,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            IsActive = request.IsActive,
            AllowParticipantsToViewOthersPredictions = request.AllowParticipantsToViewOthersPredictions,
            AllowParticipantsToSubmitPredictions = request.AllowParticipantsToSubmitPredictions,
            CreatedAt = DateTime.UtcNow,
        };

        if (request.IsActive)
        {
            await DeactivateOthersAsync(season.Id, ct);
        }

        _db.Seasons.Add(season);
        _audit.Add(actingUserId, "SeasonCreated", nameof(Season), season.Id.ToString(), new
        {
            season.Name,
            season.AllowParticipantsToViewOthersPredictions,
            season.AllowParticipantsToSubmitPredictions,
        });
        await _db.SaveChangesAsync(ct);
        return Map(season, hasParticipantPredictions: false);
    }

    public async Task<SeasonDto> UpdateAsync(Guid id, SeasonRequest request, Guid actingUserId, CancellationToken ct)
    {
        ValidateDates(request);

        var groupId = await _current.GetGroupIdAsync(ct);
        var season = await _db.Seasons.FirstOrDefaultAsync(s => s.Id == id && s.GroupId == groupId, ct)
            ?? throw new NotFoundException("notFound.season");

        season.Name = request.Name;
        season.StartDate = request.StartDate;
        season.EndDate = request.EndDate;
        season.AllowParticipantsToViewOthersPredictions = request.AllowParticipantsToViewOthersPredictions;
        season.AllowParticipantsToSubmitPredictions = request.AllowParticipantsToSubmitPredictions;

        if (request.IsActive && !season.IsActive)
        {
            await DeactivateOthersAsync(season.Id, ct);
        }
        season.IsActive = request.IsActive;

        _audit.Add(actingUserId, "SeasonUpdated", nameof(Season), season.Id.ToString(), new
        {
            season.Name,
            season.AllowParticipantsToViewOthersPredictions,
            season.AllowParticipantsToSubmitPredictions,
        });
        await _db.SaveChangesAsync(ct);
        return Map(season, await HasParticipantPredictionsAsync(season.Id, ct));
    }

    public async Task<SeasonDto> SetActiveAsync(Guid id, Guid actingUserId, CancellationToken ct)
    {
        var groupId = await _current.GetGroupIdAsync(ct);
        var season = await _db.Seasons.FirstOrDefaultAsync(s => s.Id == id && s.GroupId == groupId, ct)
            ?? throw new NotFoundException("notFound.season");

        await DeactivateOthersAsync(season.Id, ct);
        season.IsActive = true;

        _audit.Add(actingUserId, "SeasonActivated", nameof(Season), season.Id.ToString(), null);
        await _db.SaveChangesAsync(ct);
        return Map(season, await HasParticipantPredictionsAsync(season.Id, ct));
    }

    private async Task DeactivateOthersAsync(Guid keepId, CancellationToken ct)
    {
        var groupId = await _current.GetGroupIdAsync(ct);
        var others = await _db.Seasons.Where(s => s.IsActive && s.Id != keepId && s.GroupId == groupId).ToListAsync(ct);
        foreach (var s in others)
        {
            s.IsActive = false;
        }
    }

    private Task<bool> HasParticipantPredictionsAsync(Guid seasonId, CancellationToken ct)
        => _db.Predictions.AnyAsync(
            p => p.Source == PredictionSource.Participant
                && _db.Rounds.Any(r => r.Id == p.RoundId && r.SeasonId == seasonId),
            ct);

    private static void ValidateDates(SeasonRequest request)
    {
        if (request.EndDate < request.StartDate)
        {
            throw new BusinessRuleException("season.endBeforeStart");
        }
    }

    private static SeasonDto Map(Season s, bool hasParticipantPredictions) => new()
    {
        Id = s.Id,
        Name = s.Name,
        StartDate = s.StartDate,
        EndDate = s.EndDate,
        IsActive = s.IsActive,
        AllowParticipantsToViewOthersPredictions = s.AllowParticipantsToViewOthersPredictions,
        AllowParticipantsToSubmitPredictions = s.AllowParticipantsToSubmitPredictions,
        HasParticipantPredictions = hasParticipantPredictions,
    };
}
