using Palpitao.Api.Enums;

namespace Palpitao.Api.Entities;

public class Prediction
{
    public Guid Id { get; set; }

    public Guid RoundId { get; set; }

    public Guid RoundMatchId { get; set; }

    public Guid UserId { get; set; }

    public int PredictedHomeScore { get; set; }

    public int PredictedAwayScore { get; set; }

    // Filled in when the round is scored (later phase).
    public ScoreCategory ScoreCategory { get; set; } = ScoreCategory.None;
    public int Points { get; set; }

    public DateTime SubmittedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    /// <summary>Whether the prediction came from the participant, an admin or OCR.</summary>
    public PredictionSource Source { get; set; } = PredictionSource.Participant;

    /// <summary>Admin who created the record (when registered on behalf of a participant).</summary>
    public Guid? CreatedByUserId { get; set; }

    /// <summary>Admin who last updated the record on behalf of a participant.</summary>
    public Guid? UpdatedByUserId { get; set; }

    // Navigation
    public Round? Round { get; set; }
    public RoundMatch? RoundMatch { get; set; }
    public User? User { get; set; }
}
