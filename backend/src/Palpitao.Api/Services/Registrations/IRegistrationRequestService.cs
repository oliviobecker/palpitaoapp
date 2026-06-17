using Palpitao.Api.DTOs.Admin;

namespace Palpitao.Api.Services.Registrations;

public interface IRegistrationRequestService
{
    /// <summary>Pending membership requests of the current group.</summary>
    Task<IReadOnlyList<RegistrationRequestDto>> ListPendingAsync(CancellationToken ct);

    /// <summary>A single membership request (by GroupUser id) within the current group.</summary>
    Task<RegistrationRequestDto> GetAsync(Guid groupUserId, CancellationToken ct);

    /// <summary>Approves a pending membership request in the current group.</summary>
    Task ApproveAsync(Guid groupUserId, Guid adminId, CancellationToken ct);

    /// <summary>Rejects a pending membership request in the current group.</summary>
    Task RejectAsync(Guid groupUserId, string? reason, Guid adminId, CancellationToken ct);
}
