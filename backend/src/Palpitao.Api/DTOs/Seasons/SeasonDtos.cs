namespace Palpitao.Api.DTOs.Seasons;

public class SeasonDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public bool IsActive { get; set; }

    /// <summary>Whether participants may view others' predictions (drives the UI option).</summary>
    public bool AllowParticipantsToViewOthersPredictions { get; set; }

    /// <summary>Whether participants submit predictions in the app (false = admin-only).</summary>
    public bool AllowParticipantsToSubmitPredictions { get; set; } = true;

    /// <summary>
    /// True when participant-submitted predictions already exist in this season — the UI
    /// warns before switching to admin-only.
    /// </summary>
    public bool HasParticipantPredictions { get; set; }
}

public class SeasonRequest
{
    public string Name { get; set; } = string.Empty;

    public DateOnly StartDate { get; set; }

    public DateOnly EndDate { get; set; }

    public bool IsActive { get; set; }

    /// <summary>Allow participants to view others' predictions (default false).</summary>
    public bool AllowParticipantsToViewOthersPredictions { get; set; }

    /// <summary>How predictions are submitted: participants in the app (default true) or admin-only.</summary>
    public bool AllowParticipantsToSubmitPredictions { get; set; } = true;
}
