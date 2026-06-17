using Palpitao.Api.Enums;

namespace Palpitao.Api.Entities;

/// <summary>
/// Per-match scoring detail of a participant's prediction (computed when a round
/// is scored).
/// </summary>
public class PredictionScore
{
    public Guid Id { get; set; }

    public Guid RoundId { get; set; }

    public Guid RoundMatchId { get; set; }

    public Guid UserId { get; set; }

    public Guid PredictionId { get; set; }

    public int BasePoints { get; set; }

    public int Multiplier { get; set; }

    public int FinalPoints { get; set; }

    public ScoreCategory ScoreCategory { get; set; }

    public bool IsExactScore { get; set; }

    public bool IsCorrectColumn { get; set; }

    public DateTime CreatedAt { get; set; }

    // Navigation
    public Round? Round { get; set; }
    public RoundMatch? RoundMatch { get; set; }
    public User? User { get; set; }
}
