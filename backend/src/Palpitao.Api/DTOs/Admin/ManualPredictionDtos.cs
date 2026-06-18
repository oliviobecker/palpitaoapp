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
