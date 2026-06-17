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
    IReadOnlyList<OcrPredictionCandidate> BuildCandidates(
        Guid batchId,
        Guid roundId,
        string text,
        IReadOnlyList<RoundMatch> matches,
        IReadOnlyList<User> participants);

    /// <summary>Confirms a reviewed batch, saving candidates as Source = AdminOcr.</summary>
    Task ConfirmAsync(Guid batchId, Guid adminId, CancellationToken ct);
}
