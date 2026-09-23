using System.Text;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Palpitao.Api.Data;
using Palpitao.Api.Entities;
using Palpitao.Api.Services.Ocr;
using Xunit;
using Xunit.Abstractions;

namespace Palpitao.Api.Tests.Ocr;

/// <summary>
/// Runs a folder of real screenshots through the real engine and the real import, and reports
/// what came out — the measurement to take before changing anything in the OCR import, and again
/// after. Deduced fixes have gone to production twice here and missed; measured ones did not.
///
/// Off unless <c>OCR_SAMPLES_DIR</c> is set, because the screenshots carry real people's names and
/// never enter the repository. Layout of that folder:
/// <list type="bullet">
/// <item><c>&lt;round number&gt;/fixtures.txt</c> — one "Home | Away" line per fixture, catalogue names;
/// a folder without it is skipped.</item>
/// <item><c>&lt;round number&gt;/*.png|jpg|jpeg|webp</c> — the screenshots, named after the participant
/// as the admin names them.</item>
/// <item><c>participants.txt</c> (optional) — the group's participant names, one per line.</item>
/// </list>
/// <c>OCR_TESSDATA</c> points at the language models (defaults to <c>backend/tessdata</c> found above
/// the test binaries); <c>OCR_SAMPLES_REPORT</c>, when set, also receives the report as a file.
/// </summary>
public class OcrSamplesTests(ITestOutputHelper output)
{
    private const string SamplesVariable = "OCR_SAMPLES_DIR";

    /// <summary>A <see cref="FactAttribute"/> that only runs when the samples folder is configured.</summary>
    private sealed class OcrSamplesFactAttribute : FactAttribute
    {
        public OcrSamplesFactAttribute()
        {
            if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(SamplesVariable)))
            {
                Skip = $"Set {SamplesVariable} to a folder of screenshots to measure the OCR import.";
            }
        }
    }

    private sealed class StubHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Testing";
        public string ApplicationName { get; set; } = "Palpitao.Api";
        public string ContentRootPath { get; set; } = Directory.GetCurrentDirectory();
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private static readonly string[] ImageExtensions = [".png", ".jpg", ".jpeg", ".webp"];

    [OcrSamplesFact]
    public void Measures_every_screenshot_in_the_samples_folder()
    {
        var samples = Environment.GetEnvironmentVariable(SamplesVariable)!;
        var engine = new TesseractOcrEngine(
            new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?> { ["Ocr:TessdataPath"] = TessdataPath() })
                .Build(),
            new StubHostEnvironment(),
            NullLogger<TesseractOcrEngine>.Instance);
        Assert.Empty(engine.MissingLanguages("por"));

        var roster = ReadLines(Path.Combine(samples, "participants.txt"))
            .Select(name => new User { Id = Guid.NewGuid(), Name = name })
            .ToList();
        var rounds = Directory.GetDirectories(samples)
            .Where(dir => File.Exists(Path.Combine(dir, "fixtures.txt")))
            .Select(dir => (Dir: dir, Number: int.TryParse(Path.GetFileName(dir), out var n) ? n : 0))
            .OrderBy(r => r.Number)
            .Select(r => (r.Dir, Round: RoundOf(r.Number, ReadLines(Path.Combine(r.Dir, "fixtures.txt")))))
            .ToList();
        var import = new PredictionImportService(null!, null!, null!, null!);

        var report = new StringBuilder();
        int fixtures = 0, resolved = 0, flagged = 0, ignored = 0, prints = 0, named = 0;
        foreach (var (dir, round) in rounds)
        {
            var others = rounds.Where(r => r.Round.Id != round.Id).SelectMany(r => r.Round.Matches).ToList();
            report.AppendLine($"## Rodada {round.Number} ({round.Matches.Count} jogos)");

            foreach (var image in Directory.GetFiles(dir)
                .Where(f => ImageExtensions.Contains(Path.GetExtension(f), StringComparer.OrdinalIgnoreCase))
                .Order(StringComparer.OrdinalIgnoreCase))
            {
                var file = Path.GetFileName(image);
                var readings = engine.ReadVariants(File.ReadAllBytes(image), "por");
                var result = import.BuildCandidates(
                    Guid.NewGuid(),
                    round.Id,
                    readings,
                    new OcrImportContext(
                        round.Matches.ToList(), roster, null, file, CatalogueNames.Value, others, "pt"));

                var matched = result.Candidates.Where(c => c.RoundMatchId is not null)
                    .Select(c => c.RoundMatchId).Distinct().Count();
                var people = result.Candidates.Select(c => c.UserId).Distinct()
                    .Select(id => roster.FirstOrDefault(p => p.Id == id)?.Name ?? "?")
                    .ToList();
                var review = result.Candidates.Where(c => c.NeedsReview).ToList();

                prints++;
                fixtures += round.Matches.Count;
                resolved += matched;
                flagged += review.Count;
                ignored += result.IgnoredLineCount;
                named += people is [not "?"] ? 1 : 0;

                report.AppendLine(
                    $"{file,-16} {result.Reading.Variant,-18} jogos {matched,2}/{round.Matches.Count,-2} "
                    + $"linhas {result.Candidates.Count,2}  revisar {review.Count,2}  ignoradas {result.IgnoredLineCount,2}"
                    + (result.IgnoredRoundLabels.Count > 0 ? $" (rodada {string.Join(",", result.IgnoredRoundLabels)})" : string.Empty)
                    + $"  participante [{string.Join(" / ", people)}]");
                foreach (var c in review)
                {
                    report.AppendLine($"    revisar: {c.MatchTextRaw}  {c.ReviewNotes}");
                }
            }
        }

        report.AppendLine(
            $"TOTAL: {prints} prints, jogos {resolved}/{fixtures}, participante único resolvido em {named}, "
            + $"{flagged} linhas para revisar, {ignored} linhas de outra rodada ignoradas.");

        output.WriteLine(report.ToString());
        if (Environment.GetEnvironmentVariable("OCR_SAMPLES_REPORT") is { Length: > 0 } reportPath)
        {
            File.WriteAllText(reportPath, report.ToString());
        }
    }

    /// <summary>A round with the listed fixtures, each club a <see cref="Team"/> by name as the catalogue spells it.</summary>
    private static Round RoundOf(int number, IEnumerable<string> fixtureLines)
    {
        var round = new Round { Id = Guid.NewGuid(), Number = number };
        foreach (var line in fixtureLines)
        {
            var sides = line.Split('|', StringSplitOptions.TrimEntries);
            round.Matches.Add(new RoundMatch
            {
                Id = Guid.NewGuid(),
                RoundId = round.Id,
                Round = round,
                HomeTeam = new Team { Name = sides[0] },
                AwayTeam = new Team { Name = sides[1] },
            });
        }

        return round;
    }

    private static IEnumerable<string> ReadLines(string path) =>
        File.Exists(path)
            ? File.ReadAllLines(path).Select(l => l.Trim()).Where(l => l.Length > 0 && !l.StartsWith('#'))
            : [];

    private static string TessdataPath()
    {
        if (Environment.GetEnvironmentVariable("OCR_TESSDATA") is { Length: > 0 } configured)
        {
            return configured;
        }

        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, "backend", "tessdata");
            if (File.Exists(Path.Combine(candidate, "por.traineddata")))
            {
                return candidate;
            }
        }

        return Path.Combine(AppContext.BaseDirectory, "tessdata");
    }

    private static readonly Lazy<IReadOnlyCollection<string>> CatalogueNames = new(() =>
    {
        using var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options;
        using var db = new AppDbContext(options);
        db.Database.EnsureCreated();
        return db.Teams.AsNoTracking().Select(t => t.Name).ToList();
    });
}
