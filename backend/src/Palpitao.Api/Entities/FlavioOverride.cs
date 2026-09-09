namespace Palpitao.Api.Entities;

/// <summary>Audited exemption from the Flávio reduction, scoped to one participant/round.</summary>
public class FlavioOverride
{
    public Guid Id { get; set; }
    public Guid RoundId { get; set; }
    public Guid UserId { get; set; }
    public bool IsExempt { get; set; }
    public string Justification { get; set; } = string.Empty;
    public Guid CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; }
    public Guid UpdatedByUserId { get; set; }
    public DateTime UpdatedAt { get; set; }
    public Round? Round { get; set; }
    public User? User { get; set; }
}
