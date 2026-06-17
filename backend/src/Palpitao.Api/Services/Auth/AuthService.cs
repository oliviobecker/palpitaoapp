using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Palpitao.Api.Auth;
using Palpitao.Api.Common;
using Palpitao.Api.Data;
using Palpitao.Api.DTOs.Auth;
using Palpitao.Api.Entities;
using Palpitao.Api.Enums;
using Palpitao.Api.Services.Audit;
using Group = Palpitao.Api.Entities.Group;

namespace Palpitao.Api.Services.Auth;

public partial class AuthService : IAuthService
{
    private readonly AppDbContext _db;
    private readonly IJwtTokenService _jwt;
    private readonly IAuditService _audit;

    public AuthService(AppDbContext db, IJwtTokenService jwt, IAuditService audit)
    {
        _db = db;
        _jwt = jwt;
        _audit = audit;
    }

    // At least 8 chars, with at least one letter and one digit.
    [GeneratedRegex(@"^(?=.*[A-Za-z])(?=.*\d).{8,}$")]
    private static partial Regex StrongPassword();

    public async Task RegisterAsync(RegisterRequest request, CancellationToken ct)
    {
        var name = (request.Name ?? string.Empty).Trim();
        var email = (request.Email ?? string.Empty).Trim();

        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(email))
        {
            throw new BusinessRuleException("validation.required");
        }

        if (request.Password != request.ConfirmPassword)
        {
            throw new BusinessRuleException("auth.passwordMismatch");
        }

        if (string.IsNullOrEmpty(request.Password) || !StrongPassword().IsMatch(request.Password))
        {
            throw new BusinessRuleException("auth.weakPassword");
        }

        if (request.GroupId == Guid.Empty)
        {
            throw new BusinessRuleException("group.required");
        }

        var group = await _db.Groups.FirstOrDefaultAsync(g => g.Id == request.GroupId && g.IsActive, ct)
            ?? throw new NotFoundException("group.notFound");

        // The global account is a stable identity. If the email is new, create it as
        // approved/active (group membership — not the global account — gates access).
        // If it already exists, reuse it without touching credentials.
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == email, ct);
        if (user is null)
        {
            user = new User
            {
                Id = Guid.NewGuid(),
                Name = name,
                Email = email,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
                Role = UserRole.Participant,
                Status = UserStatus.Approved,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
            };
            _db.Users.Add(user);
        }

        if (await _db.GroupUsers.AnyAsync(gu => gu.GroupId == group.Id && gu.UserId == user.Id, ct))
        {
            throw new BusinessRuleException("group.alreadyMember");
        }

        var now = DateTime.UtcNow;
        _db.GroupUsers.Add(new GroupUser
        {
            Id = Guid.NewGuid(),
            GroupId = group.Id,
            UserId = user.Id,
            Role = GroupRole.Participant,
            Status = GroupUserStatus.PendingApproval,
            CreatedAt = now,
            UpdatedAt = now,
        });

        _audit.Add(user.Id, "RegistrationSubmitted", nameof(GroupUser), user.Id.ToString(),
            new { user.Email, GroupId = group.Id, GroupName = group.Name }, group.Id);

        await _db.SaveChangesAsync(ct);
    }

    public async Task CreateGroupAsync(DTOs.Groups.CreateGroupRequest request, CancellationToken ct)
    {
        var groupName = (request.GroupName ?? string.Empty).Trim();
        var adminName = (request.AdminName ?? string.Empty).Trim();
        var email = (request.Email ?? string.Empty).Trim();

        if (string.IsNullOrWhiteSpace(groupName))
        {
            throw new BusinessRuleException("group.nameRequired");
        }

        if (string.IsNullOrWhiteSpace(adminName) || string.IsNullOrWhiteSpace(email))
        {
            throw new BusinessRuleException("validation.required");
        }

        if (request.Password != request.ConfirmPassword)
        {
            throw new BusinessRuleException("auth.passwordMismatch");
        }

        if (string.IsNullOrEmpty(request.Password) || !StrongPassword().IsMatch(request.Password))
        {
            throw new BusinessRuleException("auth.weakPassword");
        }

        if (await _db.Users.AnyAsync(u => u.Email == email, ct))
        {
            throw new BusinessRuleException("user.emailExists");
        }

        var slug = await GenerateUniqueSlugAsync(groupName, ct);
        var now = DateTime.UtcNow;

        var admin = new User
        {
            Id = Guid.NewGuid(),
            Name = adminName,
            Email = email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            // Group role (GroupAdmin) is granted via the membership below; the global
            // role stays Participant (no platform-wide admin via the public flow).
            Role = UserRole.Participant,
            Status = UserStatus.Approved,
            IsActive = true,
            CreatedAt = now,
        };
        _db.Users.Add(admin);

        var group = new Group
        {
            Id = Guid.NewGuid(),
            Name = groupName,
            Slug = slug,
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description!.Trim(),
            CreatedByUserId = admin.Id,
            OwnerUserId = admin.Id,
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now,
        };
        _db.Groups.Add(group);

        _db.GroupUsers.Add(new GroupUser
        {
            Id = Guid.NewGuid(),
            GroupId = group.Id,
            UserId = admin.Id,
            Role = GroupRole.GroupAdmin,
            Status = GroupUserStatus.Approved,
            ApprovedAt = now,
            CreatedAt = now,
            UpdatedAt = now,
        });

        _audit.Add(admin.Id, "GroupCreated", nameof(Group), group.Id.ToString(),
            new
            {
                group.Name,
                group.Slug,
            }, group.Id);

        await _db.SaveChangesAsync(ct);
    }

    private async Task<string> GenerateUniqueSlugAsync(string name, CancellationToken ct)
    {
        var baseSlug = Slugify(name);
        if (string.IsNullOrEmpty(baseSlug))
        {
            baseSlug = "grupo";
        }

        var slug = baseSlug;
        var suffix = 2;
        while (await _db.Groups.AnyAsync(g => g.Slug == slug, ct))
        {
            slug = $"{baseSlug}-{suffix++}";
        }
        return slug;
    }

    [GeneratedRegex(@"[^a-z0-9]+")]
    private static partial Regex NonSlugChars();

    private static string Slugify(string value)
    {
        var lowered = value.Trim().ToLowerInvariant();
        // Strip accents so "São Paulo" -> "sao-paulo".
        var normalized = lowered.Normalize(System.Text.NormalizationForm.FormD);
        var sb = new System.Text.StringBuilder(normalized.Length);
        foreach (var ch in normalized)
        {
            if (System.Globalization.CharUnicodeInfo.GetUnicodeCategory(ch)
                != System.Globalization.UnicodeCategory.NonSpacingMark)
            {
                sb.Append(ch);
            }
        }
        var ascii = sb.ToString().Normalize(System.Text.NormalizationForm.FormC);
        return NonSlugChars().Replace(ascii, "-").Trim('-');
    }

    public async Task<LoginOutcome> LoginAsync(LoginRequest request, CancellationToken ct)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == request.Email, ct);

        if (user is null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
        {
            return LoginOutcome.Invalid();
        }

        var blockedKey = user switch
        {
            { Status: UserStatus.PendingApproval } => "auth.pendingApproval",
            { Status: UserStatus.Rejected } => "auth.rejected",
            { Status: UserStatus.Inactive } => "auth.accountInactive",
            { IsActive: false } => "auth.accountInactive",
            { Status: not UserStatus.Approved } => "auth.accountInactive",
            _ => null,
        };

        if (blockedKey is not null)
        {
            _audit.Add(user.Id, "LoginBlocked", nameof(User), user.Id.ToString(),
                new { Status = user.Status.ToString(), user.IsActive, Reason = blockedKey });
            await _db.SaveChangesAsync(ct);
            return LoginOutcome.Blocked(blockedKey);
        }

        var (token, expiresAt) = _jwt.GenerateToken(user);

        return LoginOutcome.Ok(new LoginResponse
        {
            Token = token,
            ExpiresAtUtc = expiresAt,
            User = new UserDto
            {
                Id = user.Id,
                Name = user.Name,
                Email = user.Email,
                Role = user.Role,
                IsActive = user.IsActive,
            },
        });
    }
}
