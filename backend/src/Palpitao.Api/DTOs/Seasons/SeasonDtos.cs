using Palpitao.Api.Enums;

namespace Palpitao.Api.DTOs.Seasons;

public class SeasonDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public bool IsActive { get; set; }

    /// <summary>The kind of certame this season runs (set on creation, immutable after).</summary>
    public TournamentType TournamentType { get; set; }

    /// <summary>Whether participants may view others' predictions (drives the UI option).</summary>
    public bool AllowParticipantsToViewOthersPredictions { get; set; }

    /// <summary>Whether participants submit predictions in the app (false = admin-only).</summary>
    public bool AllowParticipantsToSubmitPredictions { get; set; } = true;

    /// <summary>Whether FA Cup fixtures are offered for this season (England certames only).</summary>
    public bool FaCupEnabled { get; set; } = true;

    /// <summary>
    /// True when participant-submitted predictions already exist in this season — the UI
    /// warns before switching to admin-only.
    /// </summary>
    public bool HasParticipantPredictions { get; set; }

    /// <summary>Key of the public standings link. Admin-only: it is the link's credential.</summary>
    public string PublicKey { get; set; } = string.Empty;

    /// <summary>Whether the public link resolves. Off until an admin publishes it.</summary>
    public bool PublicStandingsEnabled { get; set; }
}

public class SeasonRequest
{
    public string Name { get; set; } = string.Empty;

    public DateOnly StartDate { get; set; }

    public DateOnly EndDate { get; set; }

    public bool IsActive { get; set; }

    /// <summary>The kind of certame this season runs. Only honored on creation; ignored on update.</summary>
    public TournamentType TournamentType { get; set; }

    /// <summary>Allow participants to view others' predictions (default false).</summary>
    public bool AllowParticipantsToViewOthersPredictions { get; set; }

    /// <summary>How predictions are submitted: participants in the app (default true) or admin-only.</summary>
    public bool AllowParticipantsToSubmitPredictions { get; set; } = true;

    /// <summary>
    /// Offer FA Cup fixtures for this season (default true). England certames only; the
    /// World Cup certame never allows the FA Cup regardless of this flag.
    /// </summary>
    public bool FaCupEnabled { get; set; } = true;

    /// <summary>
    /// Publish the public standings link (default false). Turning it on makes the season's
    /// standings — and the predictions of its closed rounds — readable by anyone holding the
    /// key, regardless of <see cref="AllowParticipantsToViewOthersPredictions"/>. The key
    /// itself is never taken from the request; it is generated and regenerated server-side.
    /// </summary>
    public bool PublicStandingsEnabled { get; set; }
}
