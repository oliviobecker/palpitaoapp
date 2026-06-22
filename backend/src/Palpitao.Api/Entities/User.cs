using Palpitao.Api.Enums;

namespace Palpitao.Api.Entities;

public class User
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string PasswordHash { get; set; } = string.Empty;

    public UserRole Role { get; set; } = UserRole.Participant;

    /// <summary>Approval lifecycle. Public sign-ups start as PendingApproval.</summary>
    public UserStatus Status { get; set; } = UserStatus.Approved;

    /// <summary>
    /// Account-level master switch (login gate). Per-bolão deactivation lives on
    /// <see cref="GroupUser.IsActive"/> — this only disables the whole account.
    /// </summary>
    public bool IsActive { get; set; } = true;

    // --- Approval / rejection audit fields ----------------------------------
    public DateTime? ApprovedAt { get; set; }
    public Guid? ApprovedByUserId { get; set; }
    public DateTime? RejectedAt { get; set; }
    public Guid? RejectedByUserId { get; set; }
    public string? RejectionReason { get; set; }

    public DateTime CreatedAt { get; set; }

    // Navigation
    public ICollection<Prediction> Predictions { get; set; } = new List<Prediction>();
    public ICollection<Standing> Standings { get; set; } = new List<Standing>();
    public ICollection<Absence> Absences { get; set; } = new List<Absence>();
}
