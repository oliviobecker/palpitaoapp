using Palpitao.Api.Entities;

namespace Palpitao.Api.Services.Ocr;

/// <summary>A single prediction parsed from OCR text, before matching.</summary>
/// <param name="ParticipantAnnounced">
/// The name came from a line that names the participant outright ("&lt;Nome&gt;, Rodada N",
/// "PALPITES &lt;nome&gt;", "Nome: fixtures") rather than from a bare name-shaped line.
/// </param>
/// <param name="ScoreFromLetter">
/// A score glyph was a letter other than O ("bxO0" for a 5x0): the digit is a guess, not a reading.
/// </param>
public record ParsedPrediction(
    string? ParticipantName,
    string MatchText,
    string HomeTeamRaw,
    int HomeScore,
    string AwayTeamRaw,
    int AwayScore,
    bool ParticipantAnnounced = false,
    bool ScoreFromLetter = false);

/// <summary>
/// Everything the import needs besides the OCR text itself. Only <see cref="Matches"/> and
/// <see cref="Participants"/> are required; each optional part switches one behaviour on.
/// </summary>
/// <param name="ParticipantAliases">Names already confirmed for this group (see <see cref="OcrAliasService"/>).</param>
/// <param name="FileName">
/// The uploaded file's name. Admins name each screenshot after its participant ("Valter.png"), which
/// is a far better signal than anything OCR reads off the image.
/// </param>
/// <param name="CatalogueTeams">
/// Every club name in the catalogue. Switches on the approximate tier of
/// <see cref="OcrTeamMatcher.Resolve"/>, which needs it to refuse a line whose "garbled" side is
/// really another club.
/// </param>
/// <param name="OtherRoundMatches">
/// The fixtures of the season's other rounds, with <see cref="RoundMatch.Round"/> loaded. A line
/// that is one of them — people send two rounds in one screenshot — is left out instead of
/// becoming a row the admin has to delete.
/// </param>
/// <param name="Language">"pt" or "en", for the review notes written on the candidates.</param>
public sealed record OcrImportContext(
    IReadOnlyList<RoundMatch> Matches,
    IReadOnlyList<User> Participants,
    IReadOnlyDictionary<string, Guid>? ParticipantAliases = null,
    string? FileName = null,
    IReadOnlyCollection<string>? CatalogueTeams = null,
    IReadOnlyList<RoundMatch>? OtherRoundMatches = null,
    string Language = "pt");

/// <summary>What one image produced: the reading chosen, its candidates, and what was left out.</summary>
/// <param name="IgnoredLineCount">Lines that were another round's fixtures and were not turned into candidates.</param>
/// <param name="IgnoredRoundLabels">
/// The rounds those lines belong to, as labels ("7", "10.1" for a part), ascending.
/// </param>
public sealed record OcrImportResult(
    OcrReading Reading,
    IReadOnlyList<OcrPredictionCandidate> Candidates,
    int IgnoredLineCount,
    IReadOnlyList<string> IgnoredRoundLabels);

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
    /// Builds the candidates of every reading the engine made of one image and keeps the reading
    /// that resolved the most fixtures. A score the readings disagree on, or one read off a letter,
    /// is flagged for review instead of being trusted.
    /// </summary>
    OcrImportResult BuildCandidates(
        Guid batchId, Guid roundId, IReadOnlyList<OcrReading> readings, OcrImportContext context);

    /// <summary>
    /// Confirms a reviewed batch, saving candidates as Source = AdminOcr and learning the
    /// participant names the admin had to correct (via <see cref="IOcrAliasService"/>), so the
    /// next import resolves them itself.
    /// </summary>
    Task ConfirmAsync(Guid batchId, Guid adminId, CancellationToken ct);
}
