using Palpitao.Api.Entities;

namespace Palpitao.Api.Services.Ocr;

/// <summary>A single prediction parsed from OCR text, before matching.</summary>
public record ParsedPrediction(
    string? ParticipantName,
    string MatchText,
    string HomeTeamRaw,
    int HomeScore,
    string AwayTeamRaw,
    int AwayScore);

public interface IPredictionImportService
{
    /// <summary>Parses raw OCR text into prediction lines (pure, no matching).</summary>
    IReadOnlyList<ParsedPrediction> Parse(string text);

    /// <summary>
    /// Turns parsed lines into review candidates, matching participants and
    /// matches; ambiguous/unresolved items are flagged with NeedsReview = true.
    /// </summary>
    /// <param name="participantAliases">
    /// Names this group's admins have already confirmed for a participant, keyed by
    /// <see cref="OcrTeamMatcher.NormalizeAlias"/> (see <see cref="ConfirmAsync"/>). Optional:
    /// without it the resolution is exactly what it always was.
    /// </param>
    IReadOnlyList<OcrPredictionCandidate> BuildCandidates(
        Guid batchId,
        Guid roundId,
        string text,
        IReadOnlyList<RoundMatch> matches,
        IReadOnlyList<User> participants,
        IReadOnlyDictionary<string, Guid>? participantAliases = null);

    /// <summary>
    /// Confirms a reviewed batch, saving candidates as Source = AdminOcr and learning the
    /// participant names the admin had to correct (via <see cref="IOcrAliasService"/>), so the
    /// next import resolves them itself.
    /// </summary>
    Task ConfirmAsync(Guid batchId, Guid adminId, CancellationToken ct);
}
