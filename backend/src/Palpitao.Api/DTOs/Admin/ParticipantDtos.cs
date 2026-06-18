namespace Palpitao.Api.DTOs.Admin;

public class ParticipantDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public bool IsEliminated { get; set; }
    public int TotalPoints { get; set; }
    public int AbsenceCount { get; set; }
    public int PenaltyPoints { get; set; }
}

public class CreateParticipantRequest
{
    public string Name { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;
}

public class UpdateParticipantRequest
{
    public string Name { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;
}

public class EliminateRequest
{
    public string Justification { get; set; } = string.Empty;
}
