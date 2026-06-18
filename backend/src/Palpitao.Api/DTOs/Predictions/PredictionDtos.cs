using Palpitao.Api.Enums;

namespace Palpitao.Api.DTOs.Predictions;

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
    public List<PredictionDto> Predictions { get; set; } = new();
}
