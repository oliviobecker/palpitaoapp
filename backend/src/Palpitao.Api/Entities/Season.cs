namespace Palpitao.Api.Entities;

public class Season
{
    public Guid Id { get; set; }

    /// <summary>Owning group (tenant). Defaults to the seeded default group.</summary>
    public Guid GroupId { get; set; }

    public string Name { get; set; } = string.Empty;

    public DateOnly StartDate { get; set; }

    public DateOnly EndDate { get; set; }

    public bool IsActive { get; set; }

    public DateTime CreatedAt { get; set; }

    // Navigation
    public Group? Group { get; set; }
    public ICollection<Round> Rounds { get; set; } = new List<Round>();
    public ICollection<Standing> Standings { get; set; } = new List<Standing>();
}
