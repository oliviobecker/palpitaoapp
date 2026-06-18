using Palpitao.Api.Enums;

namespace Palpitao.Api.Entities;

public class Season
{
    public Guid Id { get; set; }

    /// <summary>Owning group (tenant). Defaults to the seeded default group.</summary>
    public Guid GroupId { get; set; }

    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// The kind of certame this season runs. Determines allowed competitions/phases,
    /// scoring multipliers and the Regra Flávio variant. Existing seasons default to
    /// <see cref="TournamentType.PalpitaoEngland"/>. Set on creation and immutable after.
    /// </summary>
    public TournamentType TournamentType { get; set; } = TournamentType.PalpitaoEngland;

    public DateOnly StartDate { get; set; }

    public DateOnly EndDate { get; set; }

    public bool IsActive { get; set; }

    /// <summary>
    /// When true, approved participants may view others' predictions (the mirror),
    /// still subject to the round being locked/scored. Off by default for privacy.
    /// </summary>
    public bool AllowParticipantsToViewOthersPredictions { get; set; }

    /// <summary>
    /// When true (default), participants submit/edit their own predictions in the app.
    /// When false, only the admin enters predictions (manual / OCR) and the participant
    /// submission endpoints are blocked (403); existing predictions are kept.
    /// </summary>
    public bool AllowParticipantsToSubmitPredictions { get; set; } = true;

    public DateTime CreatedAt { get; set; }

    // Navigation
    public Group? Group { get; set; }
    public ICollection<Round> Rounds { get; set; } = new List<Round>();
    public ICollection<Standing> Standings { get; set; } = new List<Standing>();
}
