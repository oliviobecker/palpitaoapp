using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Palpitao.Api.Common;
using Palpitao.Api.Data;
using Palpitao.Api.DTOs.Teams;
using Palpitao.Api.Entities;
using Palpitao.Api.Enums;
using Palpitao.Api.Services.Audit;
using Palpitao.Api.Services.Fixtures;
using Palpitao.Api.Services.Groups;

namespace Palpitao.Api.Services.Teams;

/// <summary>
/// Admin operations over the global team catalogue: moving a club between league
/// divisions by hand, and reconciling the catalogue with an external squad list.
/// </summary>
/// <remarks>
/// The catalogue is global (no <c>GroupId</c>), so these writes are visible to every
/// group. They are audited with the acting group's id so they show up in that group's
/// audit screen, which is where the admin who ran them will look.
/// </remarks>
public class TeamCatalogService : ITeamCatalogService
{
    /// <summary>
    /// Divisions a club can belong to. Cups draw from every division and the World
    /// Cup is played by national teams, so neither is a division.
    /// </summary>
    private static readonly Competition[] Divisions =
    {
        Competition.PremierLeague,
        Competition.Championship,
        Competition.LeagueOne,
    };

    private readonly AppDbContext _db;
    private readonly ITeamCatalogProvider _provider;
    private readonly IAuditService _audit;
    private readonly ICurrentGroupService _current;
    private readonly FixtureOptions _options;

    public TeamCatalogService(
        AppDbContext db,
        ITeamCatalogProvider provider,
        IAuditService audit,
        ICurrentGroupService current,
        IOptions<FixtureOptions> options)
    {
        _db = db;
        _provider = provider;
        _audit = audit;
        _current = current;
        _options = options.Value;
    }

    public async Task<AdminTeamDto> UpdateDivisionAsync(
        Guid teamId, UpdateTeamDivisionRequest request, Guid actingUserId, CancellationToken ct)
    {
        var groupId = await _current.GetGroupIdAsync(ct);

        var team = await _db.Teams.FirstOrDefaultAsync(t => t.Id == teamId, ct)
            ?? throw new NotFoundException("notFound.team");

        if (team.TeamType == TeamType.NationalTeam)
        {
            throw new BusinessRuleException("teams.nationalTeamNoDivision");
        }

        if (request.Division is not null && !Divisions.Contains(request.Division.Value))
        {
            throw new BusinessRuleException("teams.invalidDivision");
        }

        if (team.Division != request.Division)
        {
            var from = team.Division;
            team.Division = request.Division;
            // Enum values are serialized by the audit service with the default options,
            // which would write them as numbers — store the names instead.
            _audit.Add(actingUserId, "TeamDivisionChanged", nameof(Team), team.Id.ToString(),
                new { team.Name, from = from?.ToString(), to = request.Division?.ToString() }, groupId);
            await _db.SaveChangesAsync(ct);
        }

        return ToDto(team);
    }

    public async Task<TeamSyncResponse> SyncAsync(bool apply, Guid actingUserId, CancellationToken ct)
    {
        if (!_options.EnableExternalFixtureImport)
        {
            throw new BusinessRuleException("teams.syncDisabled");
        }

        var groupId = await _current.GetGroupIdAsync(ct);

        var teams = await _db.Teams.ToListAsync(ct);
        // Canonical (not Normalize): the source spells clubs its own way, so "Wolves"
        // has to find the stored "Wolverhampton Wanderers" instead of creating a twin.
        var byName = new Dictionary<string, Team>();
        foreach (var team in teams)
        {
            byName.TryAdd(FootballReference.Canonical(team.Name), team);
        }

        var response = new TeamSyncResponse { Source = _provider.SourceName, Applied = apply };
        // Teams the source placed in some division this run. Used to decide what is
        // genuinely missing, after every competition has had its say.
        var claimed = new HashSet<Guid>();

        foreach (var competition in Divisions)
        {
            var names = await _provider.GetTeamNamesAsync(competition, ct);
            var dto = new TeamSyncCompetitionDto { Competition = competition, FoundCount = names.Count };
            response.Competitions.Add(dto);
            if (names.Count == 0)
            {
                continue;
            }

            foreach (var name in names)
            {
                if (!byName.TryGetValue(FootballReference.Canonical(name), out var team))
                {
                    dto.ToCreate.Add(new TeamSyncCreateDto
                    {
                        Name = name.Trim(),
                        ShortName = TeamFactory.BuildShortName(name.Trim()),
                        IsBigSevenClub = FootballReference.IsBigSeven(name),
                    });
                    continue;
                }

                // A national team sharing a club's name must never be moved into a
                // division (nor created again — Name is unique).
                if (team.TeamType == TeamType.NationalTeam || !claimed.Add(team.Id))
                {
                    continue;
                }

                if (team.Division == competition)
                {
                    dto.UnchangedCount++;
                }
                else
                {
                    dto.ToMove.Add(new TeamSyncMoveDto
                    {
                        TeamId = team.Id,
                        Name = team.Name,
                        FromDivision = team.Division,
                        ToDivision = competition,
                    });
                }
            }
        }

        // Second pass: a club promoted out of League One is claimed by the Championship,
        // so it must not also be reported as missing from League One.
        foreach (var dto in response.Competitions.Where(c => c.FoundCount > 0))
        {
            dto.NotFound = teams
                .Where(t => t.Division == dto.Competition
                    && t.TeamType == TeamType.Club
                    && !claimed.Contains(t.Id))
                .OrderBy(t => t.Name)
                .Select(t => new TeamSyncNotFoundDto { TeamId = t.Id, Name = t.Name })
                .ToList();
        }

        if (apply)
        {
            await ApplyAsync(response, actingUserId, groupId, ct);
        }

        return response;
    }

    private async Task ApplyAsync(TeamSyncResponse response, Guid actingUserId, Guid groupId, CancellationToken ct)
    {
        var created = new List<string>();
        var moved = new List<string>();

        foreach (var dto in response.Competitions)
        {
            foreach (var candidate in dto.ToCreate)
            {
                _db.Teams.Add(TeamFactory.Create(candidate.Name, dto.Competition));
                created.Add(candidate.Name);
            }

            foreach (var move in dto.ToMove)
            {
                var team = await _db.Teams.FirstAsync(t => t.Id == move.TeamId, ct);
                team.Division = move.ToDivision;
                moved.Add($"{move.Name}: {move.FromDivision?.ToString() ?? "-"} -> {move.ToDivision}");
            }
        }

        if (created.Count == 0 && moved.Count == 0)
        {
            return;
        }

        // One entry for the whole run: the summary is the review artifact, and a row
        // per club would bury the rest of the group's audit trail.
        _audit.Add(actingUserId, "TeamCatalogSynced", nameof(Team), null, new
        {
            source = response.Source,
            found = response.Competitions.ToDictionary(c => c.Competition.ToString(), c => c.FoundCount),
            created,
            moved,
            notFound = response.Competitions.SelectMany(c => c.NotFound).Select(t => t.Name).ToList(),
        }, groupId);
        await _db.SaveChangesAsync(ct);
    }

    private static AdminTeamDto ToDto(Team team) => new()
    {
        Id = team.Id,
        Name = team.Name,
        ShortName = team.ShortName,
        IsBigSevenClub = team.IsBigSevenClub,
        CrestUrl = team.CrestUrl,
        Division = team.Division,
        TeamType = team.TeamType,
    };
}
