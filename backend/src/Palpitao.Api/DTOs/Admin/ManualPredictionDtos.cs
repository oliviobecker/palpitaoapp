using Palpitao.Api.DTOs.Predictions;
using Palpitao.Api.Enums;

namespace Palpitao.Api.DTOs.Admin;

public class ManualPredictionRequest
{
    public Guid UserId { get; set; }

    public List<PredictionItemRequest> Predictions { get; set; } = new();

    /// <summary>When true, replaces existing predictions of the participant.</summary>
    public bool OverwriteExisting { get; set; }

    /// <summary>Required to overwrite, to register for an eliminated participant or after the deadline.</summary>
    public string? Justification { get; set; }

    /// <summary>Admin override to register predictions after the round deadline/lock.</summary>
    public bool AllowAfterDeadline { get; set; }
}

/// <summary>A participant's current predictions for a round, to preload the manual screen.</summary>
public class AdminParticipantPredictionsDto
{
    public Guid RoundId { get; set; }
    public Guid UserId { get; set; }

    /// <summary>True when the participant already has predictions (overwrite required to save).</summary>
    public bool HasPredictions { get; set; }

    public List<AdminPredictionItemDto> Predictions { get; set; } = new();
}

public class AdminPredictionItemDto
{
    public Guid RoundMatchId { get; set; }
    public int PredictedHomeScore { get; set; }
    public int PredictedAwayScore { get; set; }
    public PredictionSource Source { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

/// <summary>Who has already predicted the whole round — helps the admin decide when to
/// chase stragglers (OCR/manual entry) before locking, and who is heading for an absence.</summary>
public class PredictionCoverageDto
{
    public Guid RoundId { get; set; }
    public int MatchCount { get; set; }
    public int TotalParticipants { get; set; }

    /// <summary>Participants with a prediction for every match of the round.</summary>
    public int CompleteParticipants { get; set; }

    /// <summary>
    /// Active participants still missing at least one prediction, plus anyone the admin
    /// decided by hand — a participant who predicted everything but was forced absent has to
    /// stay visible, otherwise there is no way to undo it.
    /// </summary>
    public List<PredictionCoverageParticipantDto> Missing { get; set; } = new();
}

public class PredictionCoverageParticipantDto
{
    public Guid UserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int PredictedCount { get; set; }

    /// <summary>
    /// Whether scoring this round now would mark them absent. Comes from
    /// <c>IAbsenceService</c> (so admin overrides win) rather than being recomputed here:
    /// the label must never drift from what the scoring actually does.
    /// </summary>
    public bool WillBeAbsent { get; set; }

    /// <summary>An <c>AbsenceOverride</c> exists, so the flag above was decided by an admin.</summary>
    public bool HasOverride { get; set; }
}
