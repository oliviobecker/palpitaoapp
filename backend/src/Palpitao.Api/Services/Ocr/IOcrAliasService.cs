using Palpitao.Api.DTOs.Admin;
using Palpitao.Api.Entities;

namespace Palpitao.Api.Services.Ocr;

/// <summary>
/// Owns <see cref="OcrParticipantAlias"/> — the names an admin has confirmed for a participant.
/// Both the import flow (which learns them and reads them back) and the admin screen (which lists,
/// re-points and removes them) go through here, so the normalization and uniqueness rules live in
/// one place.
/// </summary>
public interface IOcrAliasService
{
    /// <summary>
    /// The group's aliases, keyed by <see cref="OcrTeamMatcher.NormalizeAlias"/> and ready for
    /// <see cref="IPredictionImportService.BuildCandidates"/>.
    /// </summary>
    Task<IReadOnlyDictionary<string, Guid>> GetForGroupAsync(Guid groupId, CancellationToken ct);

    /// <summary>
    /// Records the participant names a confirmed batch had to be corrected on, and returns how many
    /// were learned. Enqueues its changes on the caller's unit of work — the caller saves.
    /// </summary>
    Task<int> LearnAsync(
        ICollection<OcrPredictionCandidate> candidates,
        Guid groupId,
        Guid adminId,
        DateTime now,
        CancellationToken ct);

    /// <summary>Every alias of the current group, with the participant it points at.</summary>
    Task<List<OcrParticipantAliasDto>> ListAsync(CancellationToken ct);

    /// <summary>Teaches an alias by hand, before any screenshot has needed it.</summary>
    Task<OcrParticipantAliasDto> CreateAsync(
        CreateOcrParticipantAliasRequest request, Guid adminId, CancellationToken ct);

    /// <summary>Re-points an alias at another participant.</summary>
    Task<OcrParticipantAliasDto> UpdateAsync(
        Guid id, UpdateOcrParticipantAliasRequest request, Guid adminId, CancellationToken ct);

    /// <summary>Forgets an alias.</summary>
    Task DeleteAsync(Guid id, Guid adminId, CancellationToken ct);
}
