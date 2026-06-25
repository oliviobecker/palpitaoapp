using Microsoft.EntityFrameworkCore;
using Palpitao.Api.Common;
using Palpitao.Api.Data;
using Palpitao.Api.DTOs.Scoring;
using Palpitao.Api.Entities;
using Palpitao.Api.Enums;
using Palpitao.Api.Services.Audit;
using Palpitao.Api.Services.Groups;

namespace Palpitao.Api.Services.Scoring;

public class SeasonScoringConfigService : ISeasonScoringConfigService
{
    private const int MaxRuleScore = 20;

    // Categories that can be assigned to an exact score (ExtraUncommon is the implicit
    // catch-all; only None is not a real points category).
    private static readonly HashSet<ScoreCategory> AssignableCategories = new()
    {
        ScoreCategory.ColumnOnly, ScoreCategory.Traditional, ScoreCategory.Medium,
        ScoreCategory.Uncommon, ScoreCategory.ExtraUncommon,
    };

    private readonly AppDbContext _db;
    private readonly IAuditService _audit;
    private readonly ICurrentGroupService _current;

    public SeasonScoringConfigService(AppDbContext db, IAuditService audit, ICurrentGroupService current)
    {
        _db = db;
        _audit = audit;
        _current = current;
    }

    public async Task<ScoringRuleSet> GetRuleSetAsync(Guid seasonId, CancellationToken ct)
    {
        var season = await _db.Seasons.AsNoTracking().FirstOrDefaultAsync(s => s.Id == seasonId, ct)
            ?? throw new NotFoundException("notFound.season");

        var config = await LoadConfigAsync(seasonId, tracking: false, ct);
        if (config is null)
        {
            var classicIds = await DefaultClassicTeamIdsAsync(season.TournamentType, ct);
            return ScoringDefaults.ForTournamentType(season.TournamentType, classicIds);
        }

        return BuildRuleSet(config);
    }

    public async Task<ScoringRuleSet> GetRuleSetForRoundAsync(Guid roundId, CancellationToken ct)
    {
        var seasonId = await _db.Rounds
            .Where(r => r.Id == roundId)
            .Select(r => r.SeasonId)
            .FirstOrDefaultAsync(ct);
        if (seasonId == Guid.Empty)
        {
            throw new NotFoundException("notFound.round");
        }

        return await GetRuleSetAsync(seasonId, ct);
    }

    public async Task<ScoringConfigDto> GetConfigAsync(Guid seasonId, CancellationToken ct)
    {
        var season = await LoadSeasonAsync(seasonId, ct);
        var config = await LoadConfigAsync(seasonId, tracking: false, ct);
        var hasScored = await HasScoredRoundsAsync(seasonId, ct);
        var candidates = await CandidateTeamsAsync(season.TournamentType, ct);

        // No persisted config yet: present the tournament-type defaults (read-only — a GET
        // never writes, so a participant viewing predictions can't create admin rows).
        if (config is null)
        {
            var classicIds = await DefaultClassicTeamIdsAsync(season.TournamentType, ct);
            return BuildDefaultDto(season, hasScored, candidates, classicIds);
        }

        return MapDto(season, config, hasScored, candidates);
    }

    public async Task<ScoringConfigDto> UpdateAsync(Guid seasonId, ScoringConfigRequest request, Guid actingUserId, CancellationToken ct)
    {
        var season = await LoadSeasonAsync(seasonId, ct);
        await Validate(request, ct);

        var now = DateTime.UtcNow;
        var config = await LoadConfigAsync(seasonId, tracking: true, ct);
        if (config is null)
        {
            config = new SeasonScoringConfig
            {
                Id = Guid.NewGuid(),
                GroupId = season.GroupId,
                SeasonId = season.Id,
                CreatedAt = now,
            };
            _db.SeasonScoringConfigs.Add(config);
        }

        config.ColumnOnlyPoints = request.BasePoints.ColumnOnly;
        config.TraditionalPoints = request.BasePoints.Traditional;
        config.MediumPoints = request.BasePoints.Medium;
        config.UncommonPoints = request.BasePoints.Uncommon;
        config.ExtraUncommonPoints = request.BasePoints.ExtraUncommon;
        config.UpdatedAt = now;

        // Replace children wholesale (delete-then-insert keeps the edit simple and atomic).
        _db.ScoringScoreEntries.RemoveRange(_db.ScoringScoreEntries.Where(x => x.ConfigId == config.Id));
        _db.ScoringMultiplierRules.RemoveRange(_db.ScoringMultiplierRules.Where(x => x.ConfigId == config.Id));
        _db.ScoringClassicTeams.RemoveRange(_db.ScoringClassicTeams.Where(x => x.ConfigId == config.Id));
        await _db.SaveChangesAsync(ct);

        foreach (var entry in request.ScoreEntries)
        {
            _db.ScoringScoreEntries.Add(new ScoringScoreEntry
            {
                Id = Guid.NewGuid(),
                ConfigId = config.Id,
                Low = Math.Min(entry.Low, entry.High),
                High = Math.Max(entry.Low, entry.High),
                Category = entry.Category,
            });
        }

        foreach (var rule in request.MultiplierRules)
        {
            _db.ScoringMultiplierRules.Add(new ScoringMultiplierRule
            {
                Id = Guid.NewGuid(),
                ConfigId = config.Id,
                Competition = rule.Competition,
                Phase = rule.Phase,
                Multiplier = rule.Multiplier,
                ClassicMultiplier = rule.ClassicMultiplier,
            });
        }

        foreach (var teamId in request.ClassicTeamIds.Distinct())
        {
            _db.ScoringClassicTeams.Add(new ScoringClassicTeam
            {
                Id = Guid.NewGuid(),
                ConfigId = config.Id,
                TeamId = teamId,
            });
        }

        _audit.Add(actingUserId, "ScoringConfigUpdated", nameof(SeasonScoringConfig), config.Id.ToString(), new
        {
            seasonId,
            scoreEntries = request.ScoreEntries.Count,
            multiplierRules = request.MultiplierRules.Count,
            classicTeams = request.ClassicTeamIds.Count,
        });
        await _db.SaveChangesAsync(ct);

        return await GetConfigAsync(seasonId, ct);
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------
    private async Task<Season> LoadSeasonAsync(Guid seasonId, CancellationToken ct)
    {
        var groupId = await _current.GetGroupIdAsync(ct);
        return await _db.Seasons.FirstOrDefaultAsync(s => s.Id == seasonId && s.GroupId == groupId, ct)
            ?? throw new NotFoundException("notFound.season");
    }

    private async Task<SeasonScoringConfig?> LoadConfigAsync(Guid seasonId, bool tracking, CancellationToken ct)
    {
        var query = _db.SeasonScoringConfigs
            .Include(c => c.ScoreEntries)
            .Include(c => c.MultiplierRules)
            .Include(c => c.ClassicTeams)
            .AsQueryable();
        if (!tracking)
        {
            query = query.AsNoTracking();
        }

        return await query.FirstOrDefaultAsync(c => c.SeasonId == seasonId, ct);
    }

    private Task<bool> HasScoredRoundsAsync(Guid seasonId, CancellationToken ct)
        => _db.Rounds.AnyAsync(r => r.SeasonId == seasonId && r.Status == RoundStatus.Scored, ct);

    /// <summary>Default classic-eligible teams from the global catalogue (Big Seven / world champions).</summary>
    private async Task<IReadOnlySet<Guid>> DefaultClassicTeamIdsAsync(TournamentType type, CancellationToken ct)
    {
        var query = type == TournamentType.FifaWorldCup
            ? _db.Teams.Where(t => t.WorldCupTitles > 0)
            : _db.Teams.Where(t => t.IsBigSevenClub);
        var ids = await query.Select(t => t.Id).ToListAsync(ct);
        return ids.ToHashSet();
    }

    /// <summary>Candidate teams an admin may mark as classics, by tournament type.</summary>
    private async Task<List<ScoringConfigTeamDto>> CandidateTeamsAsync(TournamentType type, CancellationToken ct)
    {
        var query = type == TournamentType.FifaWorldCup
            ? _db.Teams.Where(t => t.TeamType == TeamType.NationalTeam)
            : _db.Teams.Where(t => t.TeamType == TeamType.Club);
        return await query
            .OrderBy(t => t.Name)
            .Select(t => new ScoringConfigTeamDto { TeamId = t.Id, Name = t.Name, ShortName = t.ShortName })
            .ToListAsync(ct);
    }

    private static ScoringRuleSet BuildRuleSet(SeasonScoringConfig config)
    {
        var basePoints = new Dictionary<ScoreCategory, int>
        {
            [ScoreCategory.ColumnOnly] = config.ColumnOnlyPoints,
            [ScoreCategory.Traditional] = config.TraditionalPoints,
            [ScoreCategory.Medium] = config.MediumPoints,
            [ScoreCategory.Uncommon] = config.UncommonPoints,
            [ScoreCategory.ExtraUncommon] = config.ExtraUncommonPoints,
        };

        var scoreCategories = new Dictionary<(int, int), ScoreCategory>();
        foreach (var entry in config.ScoreEntries)
        {
            scoreCategories[(Math.Min(entry.Low, entry.High), Math.Max(entry.Low, entry.High))] = entry.Category;
        }

        var multipliers = new Dictionary<(Competition, MatchPhase), (int, int)>();
        foreach (var rule in config.MultiplierRules)
        {
            multipliers[(rule.Competition, rule.Phase)] = (rule.Multiplier, rule.ClassicMultiplier);
        }

        var classic = config.ClassicTeams.Select(t => t.TeamId).ToHashSet();
        return new ScoringRuleSet(basePoints, scoreCategories, multipliers, classic);
    }

    private static ScoringConfigDto MapDto(
        Season season, SeasonScoringConfig config, bool hasScored, List<ScoringConfigTeamDto> candidates)
    {
        var selected = config.ClassicTeams.Select(t => t.TeamId).ToHashSet();
        return new ScoringConfigDto
        {
            SeasonId = season.Id,
            SeasonName = season.Name,
            TournamentType = season.TournamentType,
            HasScoredRounds = hasScored,
            BasePoints = new ScoringBasePointsDto
            {
                ColumnOnly = config.ColumnOnlyPoints,
                Traditional = config.TraditionalPoints,
                Medium = config.MediumPoints,
                Uncommon = config.UncommonPoints,
                ExtraUncommon = config.ExtraUncommonPoints,
            },
            ScoreEntries = config.ScoreEntries
                .OrderBy(e => e.Low).ThenBy(e => e.High)
                .Select(e => new ScoringScoreEntryDto { Low = e.Low, High = e.High, Category = e.Category })
                .ToList(),
            MultiplierRules = config.MultiplierRules
                .OrderBy(r => r.Competition).ThenBy(r => r.Phase)
                .Select(r => new ScoringMultiplierRuleDto
                {
                    Competition = r.Competition,
                    Phase = r.Phase,
                    Multiplier = r.Multiplier,
                    ClassicMultiplier = r.ClassicMultiplier,
                })
                .ToList(),
            Teams = WithSelection(candidates, selected),
        };
    }

    private static ScoringConfigDto BuildDefaultDto(
        Season season, bool hasScored, List<ScoringConfigTeamDto> candidates, IReadOnlySet<Guid> classicIds)
    {
        var bp = ScoringDefaults.BasePoints();
        return new ScoringConfigDto
        {
            SeasonId = season.Id,
            SeasonName = season.Name,
            TournamentType = season.TournamentType,
            HasScoredRounds = hasScored,
            BasePoints = new ScoringBasePointsDto
            {
                ColumnOnly = bp[ScoreCategory.ColumnOnly],
                Traditional = bp[ScoreCategory.Traditional],
                Medium = bp[ScoreCategory.Medium],
                Uncommon = bp[ScoreCategory.Uncommon],
                ExtraUncommon = bp[ScoreCategory.ExtraUncommon],
            },
            ScoreEntries = ScoringDefaults.ScoreCategories()
                .Select(e => new ScoringScoreEntryDto { Low = e.Low, High = e.High, Category = e.Category })
                .ToList(),
            MultiplierRules = ScoringDefaults.MultiplierRules(season.TournamentType)
                .Select(r => new ScoringMultiplierRuleDto
                {
                    Competition = r.Competition,
                    Phase = r.Phase,
                    Multiplier = r.Normal,
                    ClassicMultiplier = r.Classic,
                })
                .ToList(),
            Teams = WithSelection(candidates, classicIds),
        };
    }

    private static List<ScoringConfigTeamDto> WithSelection(
        List<ScoringConfigTeamDto> candidates, IReadOnlySet<Guid> selected)
        => candidates
            .Select(t => new ScoringConfigTeamDto
            {
                TeamId = t.TeamId,
                Name = t.Name,
                ShortName = t.ShortName,
                IsClassic = selected.Contains(t.TeamId),
            })
            .ToList();

    private async Task Validate(ScoringConfigRequest request, CancellationToken ct)
    {
        var bp = request.BasePoints;
        if (bp.ColumnOnly < 0 || bp.Traditional < 0 || bp.Medium < 0 || bp.Uncommon < 0 || bp.ExtraUncommon < 0)
        {
            throw new BusinessRuleException("scoring.basePointsNegative");
        }

        var seenScores = new HashSet<(int, int)>();
        foreach (var entry in request.ScoreEntries)
        {
            if (entry.Low is < 0 or > MaxRuleScore || entry.High is < 0 or > MaxRuleScore)
            {
                throw new BusinessRuleException("scoring.scoreOutOfRange");
            }
            if (!AssignableCategories.Contains(entry.Category))
            {
                throw new BusinessRuleException("scoring.invalidCategory");
            }
            var key = (Math.Min(entry.Low, entry.High), Math.Max(entry.Low, entry.High));
            if (!seenScores.Add(key))
            {
                throw new BusinessRuleException("scoring.duplicateScore");
            }
        }

        foreach (var rule in request.MultiplierRules)
        {
            if (rule.Multiplier < 1 || rule.ClassicMultiplier < 1)
            {
                throw new BusinessRuleException("scoring.multiplierMin");
            }
        }

        var teamIds = request.ClassicTeamIds.Distinct().ToList();
        if (teamIds.Count > 0)
        {
            var existing = await _db.Teams.CountAsync(t => teamIds.Contains(t.Id), ct);
            if (existing != teamIds.Count)
            {
                throw new BusinessRuleException("scoring.unknownTeam");
            }
        }
    }
}
