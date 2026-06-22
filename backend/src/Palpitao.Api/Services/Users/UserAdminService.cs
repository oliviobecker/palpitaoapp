using Microsoft.EntityFrameworkCore;
using Palpitao.Api.Common;
using Palpitao.Api.Data;
using Palpitao.Api.DTOs.Admin;
using Palpitao.Api.Entities;
using Palpitao.Api.Enums;
using Palpitao.Api.Services.Audit;
using Palpitao.Api.Services.Groups;

namespace Palpitao.Api.Services.Users;

public class UserAdminService : IUserAdminService
{
    private readonly AppDbContext _db;
    private readonly IAuditService _audit;
    private readonly ICurrentGroupService _current;

    public UserAdminService(AppDbContext db, IAuditService audit, ICurrentGroupService current)
    {
        _db = db;
        _audit = audit;
        _current = current;
    }

    public async Task<IReadOnlyList<ParticipantDto>> ListParticipantsAsync(CancellationToken ct)
    {
        var groupId = await _current.GetGroupIdAsync(ct);

        // The roster is the current group's approved participant members; pending/
        // rejected sign-ups live in the registration-requests screen. Active/eliminated
        // are per-group flags carried on the membership.
        var participants = await _db.GroupUsers
            .Where(gu => gu.GroupId == groupId
                && gu.Role == GroupRole.Participant
                && gu.Status == GroupUserStatus.Approved)
            .Join(_db.Users, gu => gu.UserId, u => u.Id, (gu, u) => new
            {
                u.Id,
                u.Name,
                u.Email,
                gu.IsActive,
                gu.IsEliminated,
            })
            .OrderBy(x => x.Name)
            .ToListAsync(ct);

        var activeSeasonId = await _db.Seasons
            .Where(s => s.IsActive && s.GroupId == groupId)
            .Select(s => (Guid?)s.Id)
            .FirstOrDefaultAsync(ct);

        var standings = activeSeasonId is null
            ? new Dictionary<Guid, Standing>()
            : await _db.Standings
                .Where(s => s.SeasonId == activeSeasonId)
                .ToDictionaryAsync(s => s.UserId, ct);

        return participants.Select(x =>
        {
            standings.TryGetValue(x.Id, out var standing);
            return new ParticipantDto
            {
                Id = x.Id,
                Name = x.Name,
                Email = x.Email,
                IsActive = x.IsActive,
                IsEliminated = x.IsEliminated,
                TotalPoints = standing?.TotalPoints ?? 0,
                AbsenceCount = standing?.AbsenceCount ?? 0,
                PenaltyPoints = standing?.PenaltyPoints ?? 0,
            };
        }).ToList();
    }

    public async Task<ParticipantDto> CreateAsync(CreateParticipantRequest request, Guid actingUserId, CancellationToken ct)
    {
        var groupId = await _current.GetGroupIdAsync(ct);

        // Admin-created accounts must meet the same strength rule as self-registration.
        PasswordPolicy.Validate(request.Password);

        var emailTaken = await _db.Users.AnyAsync(u => u.Email == request.Email, ct);
        if (emailTaken)
        {
            throw new BusinessRuleException("user.emailExists");
        }

        var now = DateTime.UtcNow;
        var user = new User
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            Email = request.Email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            Role = UserRole.Participant,
            Status = UserStatus.Approved,
            IsActive = true,
            ApprovedAt = now,
            ApprovedByUserId = actingUserId,
            CreatedAt = now,
        };

        _db.Users.Add(user);
        // Add the user as an approved participant of the current group.
        _db.GroupUsers.Add(new GroupUser
        {
            Id = Guid.NewGuid(),
            GroupId = groupId,
            UserId = user.Id,
            Role = GroupRole.Participant,
            Status = GroupUserStatus.Approved,
            ApprovedAt = now,
            ApprovedByUserId = actingUserId,
            CreatedAt = now,
            UpdatedAt = now,
        });
        _audit.Add(actingUserId, "ParticipantCreated", nameof(User), user.Id.ToString(), new { user.Name, user.Email }, groupId);
        await _db.SaveChangesAsync(ct);

        return new ParticipantDto
        {
            Id = user.Id,
            Name = user.Name,
            Email = user.Email,
            IsActive = true,
            IsEliminated = false,
        };
    }

    public async Task<ParticipantDto> UpdateAsync(Guid id, UpdateParticipantRequest request, Guid actingUserId, CancellationToken ct)
    {
        var (user, membership) = await LoadParticipantAsync(id, ct);

        if (user.Email != request.Email &&
            await _db.Users.AnyAsync(u => u.Email == request.Email && u.Id != id, ct))
        {
            throw new BusinessRuleException("user.emailExists");
        }

        user.Name = request.Name;
        user.Email = request.Email;

        _audit.Add(actingUserId, "ParticipantUpdated", nameof(User), user.Id.ToString(), new { user.Name, user.Email });
        await _db.SaveChangesAsync(ct);

        return new ParticipantDto
        {
            Id = user.Id,
            Name = user.Name,
            Email = user.Email,
            IsActive = membership.IsActive,
            IsEliminated = membership.IsEliminated,
        };
    }

    public async Task SetActiveAsync(Guid id, bool active, Guid actingUserId, CancellationToken ct)
    {
        var (_, membership) = await LoadParticipantAsync(id, ct);
        // Per-group deactivation only; the global account is untouched.
        membership.IsActive = active;
        _audit.Add(actingUserId, active ? "ParticipantActivated" : "ParticipantDeactivated", nameof(GroupUser), id.ToString(), null);
        await _db.SaveChangesAsync(ct);
    }

    public async Task EliminateAsync(Guid id, string justification, Guid actingUserId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(justification))
        {
            throw new BusinessRuleException("common.justificationRequired");
        }

        var (_, membership) = await LoadParticipantAsync(id, ct);
        membership.IsEliminated = true;
        _audit.Add(actingUserId, "ParticipantEliminated", nameof(GroupUser), id.ToString(), new { justification });
        await _db.SaveChangesAsync(ct);
    }

    /// <summary>Loads the global user and their participant membership in the current
    /// group (the membership carries the per-group active/eliminated flags).</summary>
    private async Task<(User User, GroupUser Membership)> LoadParticipantAsync(Guid id, CancellationToken ct)
    {
        var groupId = await _current.GetGroupIdAsync(ct);
        var membership = await _db.GroupUsers.FirstOrDefaultAsync(
            gu => gu.GroupId == groupId && gu.UserId == id && gu.Role == GroupRole.Participant, ct)
            ?? throw new NotFoundException("notFound.participant");

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == id, ct)
            ?? throw new NotFoundException("notFound.participant");

        return (user, membership);
    }
}
