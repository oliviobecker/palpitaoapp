namespace Palpitao.Api.Entities;

/// <summary>
/// Marker for tenant-root entities that carry their owning <c>GroupId</c> directly
/// (<see cref="Season"/>, <see cref="Round"/>, <see cref="Standing"/>,
/// <see cref="RoundParticipantResult"/>). The <see cref="Data.AppDbContext"/> uses it to
/// apply the multi-tenant global query filter and to auto-stamp the current request's
/// group on insert, so a forgotten <c>GroupId</c> assignment can never silently place a
/// row in the default group.
/// </summary>
public interface IGroupOwned
{
    Guid GroupId { get; set; }
}
