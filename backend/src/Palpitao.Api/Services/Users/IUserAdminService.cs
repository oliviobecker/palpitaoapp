using Palpitao.Api.DTOs.Admin;

namespace Palpitao.Api.Services.Users;

public interface IUserAdminService
{
    Task<IReadOnlyList<ParticipantDto>> ListParticipantsAsync(CancellationToken ct);
    Task<ParticipantDto> CreateAsync(CreateParticipantRequest request, Guid actingUserId, CancellationToken ct);
    Task<ParticipantDto> UpdateAsync(Guid id, UpdateParticipantRequest request, Guid actingUserId, CancellationToken ct);
    Task SetActiveAsync(Guid id, bool active, Guid actingUserId, CancellationToken ct);
    Task EliminateAsync(Guid id, string justification, Guid actingUserId, CancellationToken ct);
}
