using Palpitao.Api.DTOs.Admin;

namespace Palpitao.Api.Services.Users;

public interface IUserAdminService
{
    Task<IReadOnlyList<ParticipantDto>> ListParticipantsAsync(CancellationToken ct);
    Task<ParticipantDto> CreateAsync(CreateParticipantRequest request, Guid actingUserId, CancellationToken ct);
    Task<ParticipantDto> UpdateAsync(Guid id, UpdateParticipantRequest request, Guid actingUserId, CancellationToken ct);
    /// <summary>
    /// Flips the per-group active flag. When activating, <paramref name="absentRoundIds"/>
    /// records the participant absent for rounds that closed while they were out; the flag
    /// and the overrides land in a single transaction. Ignored when deactivating.
    /// </summary>
    Task SetActiveAsync(
        Guid id, bool active, IReadOnlyCollection<Guid>? absentRoundIds, Guid actingUserId, CancellationToken ct);
    Task EliminateAsync(Guid id, string justification, Guid actingUserId, CancellationToken ct);
}
