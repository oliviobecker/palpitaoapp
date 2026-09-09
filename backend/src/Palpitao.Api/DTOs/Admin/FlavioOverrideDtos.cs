namespace Palpitao.Api.DTOs.Admin;

public class FlavioOverrideRequest
{
    public Guid UserId { get; set; }
    public bool IsExempt { get; set; }
    public string Justification { get; set; } = string.Empty;
}

public record RoundFlavioOverridesDto(
    Guid RoundId, bool Applies, DateTime? DeadlineUtc,
    IReadOnlyList<FlavioParticipantDto> Participants);

public record FlavioParticipantDto(
    Guid UserId, string Name, bool IsTarget, DateTime? SubmittedAt,
    int? GrossPoints, int? FinalPoints, bool FlavioRuleApplied,
    bool IsExempt, string? Justification, Guid? UpdatedByUserId, DateTime? UpdatedAt);
