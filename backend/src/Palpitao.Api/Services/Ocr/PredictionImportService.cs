using Microsoft.EntityFrameworkCore;
using Palpitao.Api.Common;
using Palpitao.Api.Data;
using Palpitao.Api.Entities;
using Palpitao.Api.Enums;
using Palpitao.Api.Services.Audit;
using Palpitao.Api.Services.Groups;

namespace Palpitao.Api.Services.Ocr;

/// <summary>
/// Orchestrates OCR import: parses text via <see cref="OcrTextParser"/>, matches
/// participants/matches via <see cref="OcrTeamMatcher"/>, and persists a confirmed
/// batch. The parsing and matching logic itself lives in those two helpers.
/// </summary>
public class PredictionImportService : IPredictionImportService
{
    private readonly AppDbContext _db;
    private readonly IAuditService _audit;
    private readonly ICurrentGroupService _current;
    private readonly IOcrAliasService _aliases;

    public PredictionImportService(
        AppDbContext db, IAuditService audit, ICurrentGroupService current, IOcrAliasService aliases)
    {
        _db = db;
        _audit = audit;
        _current = current;
        _aliases = aliases;
    }

    public IReadOnlyList<ParsedPrediction> Parse(string text) => OcrTextParser.Parse(text);

    public IReadOnlyList<OcrPredictionCandidate> BuildCandidates(
        Guid batchId,
        Guid roundId,
        string text,
        IReadOnlyList<RoundMatch> matches,
        IReadOnlyList<User> participants,
        IReadOnlyDictionary<string, Guid>? participantAliases = null)
    {
        // One text, nothing to compare it with: none of the reading checks apply, and without a
        // catalogue the approximate tier stays off — exactly what this path always did.
        var context = new OcrImportContext(matches, participants, participantAliases);
        var reading = BuildReading(batchId, roundId, new OcrReading("text", text, 1f), context, null, DateTime.UtcNow);
        return reading.Rows.Select(r => Finish(r, [])).ToList();
    }

    public OcrImportResult BuildCandidates(
        Guid batchId, Guid roundId, IReadOnlyList<OcrReading> readings, OcrImportContext context)
    {
        if (readings.Count == 0)
        {
            return new OcrImportResult(new OcrReading("none", string.Empty, 0f), [], 0, []);
        }

        var now = DateTime.UtcNow;
        var fileParticipant = FileParticipant(context);
        var built = readings
            .Select(r => BuildReading(batchId, roundId, r, context, fileParticipant, now))
            .ToList();

        // The reading that tied the most fixtures of this round wins; the engine's confidence only
        // breaks ties. Confidence alone kept a reading with fewer fixtures on 6 of 51 screenshots
        // measured (557 of 575 fixtures instead of 565). Distinct fixtures, not lines: a reading
        // that read one line twice has not read more of the image.
        var chosen = built
            .OrderByDescending(b => b.ResolvedFixtures)
            .ThenByDescending(b => b.Reading.Confidence)
            .First();
        var others = built.Where(b => !ReferenceEquals(b, chosen)).SelectMany(b => b.Rows).ToList();

        var candidates = chosen.Rows
            .Select(row => Finish(row, ScoreDoubts(row, others, context.Language)))
            .ToList();

        return new OcrImportResult(
            chosen.Reading,
            candidates,
            chosen.IgnoredRounds.Count,
            chosen.IgnoredRounds.Distinct().Order().ToList());
    }

    /// <summary>One parsed line on its way to becoming a candidate, with what it was resolved from.</summary>
    private sealed record Row(OcrPredictionCandidate Candidate, ParsedPrediction Parsed, List<string> Notes);

    private sealed record ReadingBuild(OcrReading Reading, List<Row> Rows, List<int> IgnoredRounds)
    {
        public int ResolvedFixtures => Rows
            .Where(r => r.Candidate.RoundMatchId is not null)
            .Select(r => r.Candidate.RoundMatchId)
            .Distinct()
            .Count();
    }

    /// <summary>The participant the upload's file name resolves to, when it names one.</summary>
    private sealed record FileNameParticipant(string Name, Guid? UserId);

    private static FileNameParticipant? FileParticipant(OcrImportContext context)
    {
        var name = OcrTextParser.NameFromFileName(context.FileName);
        return name is null
            ? null
            : new FileNameParticipant(
                name,
                OcrTeamMatcher.ResolveParticipantFromFileName(name, context.Participants, context.ParticipantAliases));
    }

    private static ReadingBuild BuildReading(
        Guid batchId,
        Guid roundId,
        OcrReading reading,
        OcrImportContext context,
        FileNameParticipant? file,
        DateTime now)
    {
        var rows = new List<Row>();
        var ignoredRounds = new List<int>();

        foreach (var parsed in OcrTextParser.Parse(reading.Text))
        {
            var match = OcrTeamMatcher.Resolve(
                parsed.HomeTeamRaw, parsed.AwayTeamRaw, context.Matches, context.CatalogueTeams);

            if (match.MatchId is null && OtherRoundOf(parsed, context) is { } otherRound)
            {
                ignoredRounds.Add(otherRound);
                continue;
            }

            var notes = new List<string>();
            var readUserId = OcrTeamMatcher.ResolveParticipant(
                parsed.ParticipantName, context.Participants, context.ParticipantAliases);
            var userId = readUserId;

            // The file name is the admin's own label for the screenshot, so it wins over whatever OCR
            // made of the header. A header that plainly names someone else is still worth a look —
            // the image may hold two people's messages — so the row keeps the file's participant
            // but goes to review.
            if (file?.UserId is { } fileUserId)
            {
                userId = fileUserId;
                if (parsed.ParticipantAnnounced && readUserId is { } said && said != fileUserId)
                {
                    notes.Add(DomainMessages.Format("ocr.review.nameConflict", context.Language, parsed.ParticipantName));
                }
            }

            if (match.Approximate)
            {
                notes.Add(DomainMessages.Format(
                    "ocr.review.approximateMatch", context.Language, $"{parsed.HomeTeamRaw} x {parsed.AwayTeamRaw}"));
            }

            rows.Add(new Row(
                new OcrPredictionCandidate
                {
                    Id = Guid.NewGuid(),
                    OcrImportBatchId = batchId,
                    RoundId = roundId,
                    UserId = userId,
                    // Truncated, not trusted: these carry raw OCR output, and a noisy screenshot
                    // produces lines longer than any real fixture. An over-long value fails the
                    // insert inside the import's try, and the catch that records the failure saves
                    // the very same tracked entities — so it fails again and the admin gets an
                    // opaque 500 instead of "could not read this image".
                    ParticipantNameRaw = Clamp(parsed.ParticipantName, OcrPredictionCandidate.MaxParticipantNameLength),
                    RoundMatchId = match.MatchId,
                    MatchTextRaw = Clamp(parsed.MatchText, OcrPredictionCandidate.MaxMatchTextLength),
                    PredictedHomeScore = parsed.HomeScore,
                    PredictedAwayScore = parsed.AwayScore,
                    CreatedAt = now,
                    UpdatedAt = now,
                },
                parsed,
                notes));
        }

        return new ReadingBuild(reading, rows, ignoredRounds);
    }

    /// <summary>
    /// The round a line belongs to when it is not this round's: it is one of the season's other
    /// fixtures, read cleanly. People send two rounds in one screenshot ("Ezaú, Rodada 6 e 7"),
    /// and those lines used to land as a dozen rows the admin deleted one by one. Only an exact
    /// fixture of another round counts — a line that fits nothing anywhere stays for review, so
    /// a prediction is never dropped just because it could not be read.
    /// </summary>
    private static int? OtherRoundOf(ParsedPrediction parsed, OcrImportContext context)
    {
        if (context.OtherRoundMatches is not { Count: > 0 } others)
        {
            return null;
        }

        var id = OcrTeamMatcher.ResolveMatch(parsed.HomeTeamRaw, parsed.AwayTeamRaw, others);
        return id is null ? null : others.First(m => m.Id == id).Round?.Number ?? 0;
    }

    /// <summary>
    /// Why this row's score should not be trusted as read, if it should not. The other readings of
    /// the same image are the check: a score they read differently is a coin toss — on 51
    /// screenshots the reading kept got "1x1" as "0x1" and "1x0" as "1x6", and the others had it
    /// right — and a score read off a letter other than O ("bxO0") is a guess unless another
    /// reading got the same score from real digits.
    /// </summary>
    private static List<string> ScoreDoubts(Row row, IReadOnlyList<Row> others, string language)
    {
        var candidate = row.Candidate;
        if (candidate.RoundMatchId is null)
        {
            return [];
        }

        var sameFixture = others
            .Where(o => o.Candidate.RoundMatchId == candidate.RoundMatchId
                && (o.Candidate.UserId is null || candidate.UserId is null || o.Candidate.UserId == candidate.UserId))
            .ToList();

        var scores = sameFixture
            .Select(o => (o.Candidate.PredictedHomeScore, o.Candidate.PredictedAwayScore))
            .Prepend((candidate.PredictedHomeScore, candidate.PredictedAwayScore))
            .Distinct()
            .ToList();

        if (scores.Count > 1)
        {
            var read = string.Join(" / ", scores.Select(s => $"{s.PredictedHomeScore}x{s.PredictedAwayScore}"));
            return [DomainMessages.Format("ocr.review.scoreDisagreement", language, read)];
        }

        var confirmedByDigits = sameFixture.Any(o => !o.Parsed.ScoreFromLetter);
        return row.Parsed.ScoreFromLetter && !confirmedByDigits
            ? [DomainMessages.Resolve("ocr.review.scoreFromLetter", language)]
            : [];
    }

    /// <summary>Settles a row's review state once every note about it is known.</summary>
    private static OcrPredictionCandidate Finish(Row row, IReadOnlyList<string> extraNotes)
    {
        var candidate = row.Candidate;
        var notes = row.Notes.Concat(extraNotes).ToList();

        candidate.Confidence = (candidate.UserId is not null ? 0.5 : 0.0) + (candidate.RoundMatchId is not null ? 0.5 : 0.0);
        candidate.NeedsReview = candidate.UserId is null || candidate.RoundMatchId is null || notes.Count > 0;
        candidate.ReviewNotes = notes.Count > 0
            ? Clamp(string.Join(" ", notes), OcrPredictionCandidate.MaxReviewNotesLength)
            : null;
        return candidate;
    }

    private static string? Clamp(string? value, int max) =>
        value is not null && value.Length > max ? value[..max] : value;

    public async Task ConfirmAsync(Guid batchId, Guid adminId, CancellationToken ct)
    {
        var groupId = await _current.GetGroupIdAsync(ct);
        var batch = await _db.OcrImportBatches
            .Include(b => b.Candidates)
            .FirstOrDefaultAsync(b => b.Id == batchId && b.Round!.GroupId == groupId, ct)
            ?? throw new NotFoundException("notFound.ocrBatch");

        if (batch.Status == OcrBatchStatus.Confirmed)
        {
            throw new BusinessRuleException("ocr.batchAlreadyConfirmed");
        }

        if (batch.Status is not (OcrBatchStatus.Processed or OcrBatchStatus.Reviewed))
        {
            throw new BusinessRuleException("ocr.batchNotReviewable");
        }

        var incomplete = batch.Candidates.Any(c =>
            c.UserId is null || c.RoundMatchId is null ||
            c.PredictedHomeScore is null || c.PredictedAwayScore is null ||
            c.PredictedHomeScore < 0 || c.PredictedAwayScore < 0);

        if (batch.Candidates.Count == 0 || incomplete)
        {
            throw new BusinessRuleException("ocr.incompleteCandidates");
        }

        // Two candidates for the same participant+match would silently race on the same
        // Prediction row below (the second DB lookup cannot see the first pending insert).
        var hasDuplicates = batch.Candidates
            .GroupBy(c => (c.UserId, c.RoundMatchId))
            .Any(g => g.Count() > 1);

        if (hasDuplicates)
        {
            throw new BusinessRuleException("ocr.duplicateCandidates");
        }

        var now = DateTime.UtcNow;
        foreach (var c in batch.Candidates)
        {
            var existing = await _db.Predictions
                .FirstOrDefaultAsync(p => p.RoundMatchId == c.RoundMatchId && p.UserId == c.UserId, ct);

            if (existing is null)
            {
                _db.Predictions.Add(new Prediction
                {
                    Id = Guid.NewGuid(),
                    RoundId = batch.RoundId,
                    RoundMatchId = c.RoundMatchId!.Value,
                    UserId = c.UserId!.Value,
                    PredictedHomeScore = c.PredictedHomeScore!.Value,
                    PredictedAwayScore = c.PredictedAwayScore!.Value,
                    SubmittedAt = now,
                    Source = PredictionSource.AdminOcr,
                    CreatedByUserId = adminId,
                });
            }
            else
            {
                existing.PredictedHomeScore = c.PredictedHomeScore!.Value;
                existing.PredictedAwayScore = c.PredictedAwayScore!.Value;
                existing.UpdatedAt = now;
                existing.Source = PredictionSource.AdminOcr;
                existing.UpdatedByUserId = adminId;
            }
        }

        batch.Status = OcrBatchStatus.Confirmed;
        batch.ConfirmedAt = now;

        // Enqueued on this unit of work, saved by the SaveChanges below — the aliases and the
        // predictions they describe land together or not at all.
        var learned = await _aliases.LearnAsync(batch.Candidates, groupId, adminId, now, batch.OriginalFileName, ct);

        _audit.Add(adminId, "OcrImportConfirmed", nameof(OcrImportBatch), batch.Id.ToString(),
            new { batch.RoundId, count = batch.Candidates.Count, aliases = learned });

        await _db.SaveChangesAsync(ct);
    }

}
