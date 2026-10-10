using Palpitao.Application.Common.Exceptions;
using Palpitao.Domain.Common;
using Palpitao.Domain.Enums;

namespace Palpitao.Application.Predictions;

public class PredictionItemRequest
{
    public Guid RoundMatchId { get; set; }

    public int PredictedHomeScore { get; set; }

    public int PredictedAwayScore { get; set; }
}

public class SavePredictionsRequest
{
    public List<PredictionItemRequest> Predictions { get; set; } = new();
}

public class PredictionDto
{
    public Guid RoundMatchId { get; set; }
    public int PredictedHomeScore { get; set; }
    public int PredictedAwayScore { get; set; }
    public DateTime SubmittedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class MyPredictionsDto
{
    public Guid RoundId { get; set; }
    public RoundStatus Status { get; set; }
    public DateTime? FirstMatchStartsAt { get; set; }

    /// <summary>General lock: one minute before the first kickoff. Computed on serialization.</summary>
    public DateTime? PredictionDeadlineUtc => PredictionDeadline.From(FirstMatchStartsAt);

    public List<PredictionDto> Predictions { get; set; } = new();
}
