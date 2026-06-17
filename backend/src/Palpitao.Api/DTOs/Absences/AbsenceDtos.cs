namespace Palpitao.Api.DTOs.Absences;

public class AbsenceDto
{
    public Guid RoundId { get; set; }
    public int RoundNumber { get; set; }
    public Guid UserId { get; set; }
    public int AbsenceNumber { get; set; }
    public int PenaltyPoints { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class AbsenceOverrideRequest
{
    public Guid UserId { get; set; }

    /// <summary>true = marcar como ausente, false = considerar presente.</summary>
    public bool IsAbsent { get; set; }

    public string Justification { get; set; } = string.Empty;
}

public class ReactivateRequest
{
    public string Justification { get; set; } = string.Empty;
}
