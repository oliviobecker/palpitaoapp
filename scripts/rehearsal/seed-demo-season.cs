#:package Npgsql@10.0.2
#:package BCrypt.Net-Next@4.2.0

// Seeds a full, already-played "Palpitao England" season for rehearsing the app
// on STAGING before a real season starts. A .NET 10 "file-based app": run it with
// `dotnet run scripts/rehearsal/seed-demo-season.cs` -- no project file needed.
// Same style/exit-code contract as scripts/run-sql.cs.
//
// It writes ONLY raw data: users, memberships, the season, rounds, matches and
// predictions. It never writes PredictionScores / RoundParticipantResults /
// Absences / Standings -- those come from the real scorer, driven afterwards by
// scripts/rehearsal/score-season.ps1 (POST /rounds/{id}/score in ascending order).
//
// Fixtures and final scores are the real English 2025/26 season, read from the
// frozen feeds in scripts/rehearsal/fixtures/ (see the README there).
//
// Env vars -- connection & safety:
//   DB_CONNECTION               Npgsql connection string (required).
//   SEED_PARTICIPANT_PASSWORD   Shared password for the seeded test accounts (required).
//   SEED_CONFIRM                Must equal the target group's slug (required).
//   SEED_REPLACE                'true' to wipe and rebuild an existing demo season.
//   SEED_DRY_RUN                'true' to do everything then ROLLBACK.
// Identity:
//   SEED_TAG                    Drives every deterministic id. Default 'demo-2025-26'.
//   SEED_ADMIN_EMAIL            Default 'admin@palpitao.local'. Stays the only GroupAdmin.
//   SEED_GROUP_SLUG             Target group. Default: the only group, if there is exactly one.
//   SEED_EMAIL_DOMAIN           Default 'demo.palpitao.local' (intentionally non-routable).
//   SEED_SEASON_NAME            Default 'Palpitao England 2025/2026 (ensaio)'.
// Composition:
//   SEED_PLAYED_ROUNDS          Default 38. Rounds seeded as already played.
//   SEED_PL_PER_ROUND           Default 6.    SEED_CH_PER_ROUND      Default 4.
//   SEED_CORE_WINDOW_DAYS       Default 3.    SEED_CH_MAX_DRIFT_DAYS Default 5.
//   SEED_PUBLISH_LEAD_HOURS     Default 96 (must be > 24; see the Flavio note below).
//   SEED_REHEARSAL_ROUND        Default 'true'. Adds an empty Draft round to run by hand.
//   SEED_REHEARSAL_WINDOW_DAYS  Default 14 (days from now to the rehearsal round's window).
// Generation:
//   SEED_MASTER_SEED            Default 20252026.
//   SEED_FORM_AMPLITUDE         Default 0.22.
//   SEED_FLAVIO_ROUND           Default 18. Must be >= 16 and have full coverage.
//   SEED_ABSENCE_PLAN           'Name:r1,r2;Name:r3' (append ':partial' for a partial round).
//   SEED_FIXTURES_DIR           Default 'scripts/rehearsal/fixtures'.
//
// Exit codes: 0 ok, 1 error, 2 refused (season exists and SEED_REPLACE is not set).

using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Npgsql;

const string PremierLeague = "PremierLeague";
const string Championship = "Championship";

// ---------------------------------------------------------------------------
// 1. Configuration
// ---------------------------------------------------------------------------
string? Env(string k) => Environment.GetEnvironmentVariable(k) is { } v && !string.IsNullOrWhiteSpace(v) ? v : null;
string Str(string k, string d) => Env(k) ?? d;
bool Flag(string k) => string.Equals(Env(k), "true", StringComparison.OrdinalIgnoreCase);
int Int(string k, int d) => int.TryParse(Env(k), NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : d;
double Dbl(string k, double d) => double.TryParse(Env(k), NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : d;

var conn = Env("DB_CONNECTION");
if (conn is null) { Console.Error.WriteLine("ERROR: DB_CONNECTION is not set."); return 1; }

var participantPassword = Env("SEED_PARTICIPANT_PASSWORD");
if (participantPassword is null) { Console.Error.WriteLine("ERROR: SEED_PARTICIPANT_PASSWORD is not set."); return 1; }
if (participantPassword.Length < 8) { Console.Error.WriteLine("ERROR: SEED_PARTICIPANT_PASSWORD must be at least 8 characters."); return 1; }

var confirm = Env("SEED_CONFIRM");
if (confirm is null) { Console.Error.WriteLine("ERROR: SEED_CONFIRM is not set (type the target group's slug)."); return 1; }

var tag = Str("SEED_TAG", "demo-2025-26");
var adminEmail = Str("SEED_ADMIN_EMAIL", "admin@palpitao.local");
var groupSlugWanted = Env("SEED_GROUP_SLUG");
var emailDomain = Str("SEED_EMAIL_DOMAIN", "demo.palpitao.local");
var seasonName = Str("SEED_SEASON_NAME", "Palpitao England 2025/2026 (ensaio)");
var playedRounds = Int("SEED_PLAYED_ROUNDS", 38);
var plPerRound = Int("SEED_PL_PER_ROUND", 6);
var chPerRound = Int("SEED_CH_PER_ROUND", 4);
var coreWindowDays = Int("SEED_CORE_WINDOW_DAYS", 3);
var chMaxDriftDays = Int("SEED_CH_MAX_DRIFT_DAYS", 5);
var publishLeadHours = Int("SEED_PUBLISH_LEAD_HOURS", 96);
var withRehearsal = !string.Equals(Env("SEED_REHEARSAL_ROUND"), "false", StringComparison.OrdinalIgnoreCase);
var rehearsalWindowDays = Int("SEED_REHEARSAL_WINDOW_DAYS", 14);
var masterSeed = (ulong)Int("SEED_MASTER_SEED", 20252026);
var formAmplitude = Dbl("SEED_FORM_AMPLITUDE", 0.22);
// 18 is not arbitrary: with the default seed the leader has a strong round there
// (20 gross), so the halving is clearly visible on screen instead of a -2 footnote.
var flavioRound = Int("SEED_FLAVIO_ROUND", 18);
var fixturesDir = Str("SEED_FIXTURES_DIR", Path.Combine("scripts", "rehearsal", "fixtures"));
var replace = Flag("SEED_REPLACE");
var dryRun = Flag("SEED_DRY_RUN");

// The Flavio deadline is min(PublishedAt + 24h, FirstMatchStartsAt), and drops to a
// 12h window when the round is published less than 24h before kickoff. Publishing
// well over 24h ahead keeps every round on the predictable 24h branch.
if (publishLeadHours <= 24) { Console.Error.WriteLine("ERROR: SEED_PUBLISH_LEAD_HOURS must be > 24."); return 1; }
if (flavioRound < 16 || flavioRound > playedRounds)
{
    Console.Error.WriteLine($"ERROR: SEED_FLAVIO_ROUND={flavioRound} must be between 16 (FlavioRuleService.FirstApplicableRound) and SEED_PLAYED_ROUNDS={playedRounds}.");
    return 1;
}
if (playedRounds is < 1 or > 38) { Console.Error.WriteLine("ERROR: SEED_PLAYED_ROUNDS must be between 1 and 38."); return 1; }

Console.WriteLine($"""
    Palpitao rehearsal seeder
      tag / season      : {tag} / "{seasonName}"
      played rounds     : {playedRounds} ({plPerRound} PL + {chPerRound} Championship each)
      Flavio round      : {flavioRound}    publish lead: {publishLeadHours}h
      rehearsal round   : {(withRehearsal ? $"yes (#{playedRounds + 1}, Draft, +{rehearsalWindowDays}d)" : "no")}
      master seed       : {masterSeed}
      mode              : {(dryRun ? "DRY RUN (rolls back)" : replace ? "REPLACE existing" : "create")}
    """);

// ---------------------------------------------------------------------------
// 2. Participants and scenarios
// ---------------------------------------------------------------------------
// e = P(exact score), c = P(right column, not exact), t = share of misses that are
// transpositions, herd = chance of collapsing onto the crowd's modal scoreline.
// Calibrated against the real 2025/26 results: a skill-free punter using the prior
// alone hits 6.9% exact / 34.8% column, so these all read as genuine skill.
var roster = new List<Participant>
{
    // Who ends up champion is emergent, not assigned: the per-match spread is wide
    // enough that the top parameters are well under 1 sigma apart over a season.
    new("Bruno Vilaça",    0.150, 0.335, 0.05, 0.15),
    new("Ezaú Moura",      0.128, 0.330, 0.05, 0.18),
    new("Felipe Farias",   0.122, 0.322, 0.06, 0.20),
    new("Olivio Becker",   0.118, 0.318, 0.05, 0.20),
    new("Pedro Rodrigues", 0.112, 0.312, 0.06, 0.22),
    new("Gilberto Sales",  0.106, 0.305, 0.06, 0.24),
    new("João Paulo",      0.100, 0.300, 0.07, 0.25),
    new("Lucas Antunes",   0.095, 0.292, 0.07, 0.26),
    new("Edson",           0.090, 0.285, 0.07, 0.28),
    new("Manoel Neto",     0.085, 0.278, 0.08, 0.30),
    new("Valter",          0.080, 0.268, 0.08, 0.32),
    new("Dourado",         0.074, 0.258, 0.09, 0.35),
};
foreach (var p in roster) p.Email = $"{EmailSlug(p.Name)}@{emailDomain}";

// Exercises every rung of AbsenceService.PenaltyFor: 1st/2nd free, 3rd/4th -20,
// 5th eliminates. Lucas' partial round proves the "predictions < matches" branch.
var absencePlanRaw = Str("SEED_ABSENCE_PLAN",
    "Dourado:5,11,17,23,28;Valter:7,14,20,33;Edson:9,19,30;Manoel Neto:12,25;João Paulo:21;Lucas Antunes:26:partial");

foreach (var entry in absencePlanRaw.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
{
    var parts = entry.Split(':', StringSplitOptions.TrimEntries);
    if (parts.Length is < 2 or > 3) { Console.Error.WriteLine($"ERROR: bad absence plan entry '{entry}'."); return 1; }
    var who = roster.FirstOrDefault(p => p.Name == parts[0]);
    if (who is null) { Console.Error.WriteLine($"ERROR: absence plan names unknown participant '{parts[0]}'."); return 1; }
    var partial = parts.Length == 3 && parts[2].Equals("partial", StringComparison.OrdinalIgnoreCase);
    foreach (var r in parts[1].Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
    {
        if (!int.TryParse(r, out var n) || n < 1 || n > playedRounds)
        {
            Console.Error.WriteLine($"ERROR: absence plan round '{r}' for {who.Name} is out of range 1..{playedRounds}.");
            return 1;
        }
        if (n == flavioRound)
        {
            // A leader with an incomplete card is scored as a plain absence, never as
            // a Flavio penalty -- so the Flavio round must have nobody missing.
            Console.Error.WriteLine($"ERROR: absence plan puts {who.Name} on the Flavio round {flavioRound}; it needs full coverage.");
            return 1;
        }
        if (partial) who.PartialRounds.Add(n); else who.AbsentRounds.Add(n);
    }
}

// A partial card counts as an absence too, so it advances the same ladder.
foreach (var p in roster)
{
    var ladder = p.AbsentRounds.Concat(p.PartialRounds).OrderBy(n => n).ToList();
    if (ladder.Count >= 5) p.EliminatedAfterRound = ladder[4];
}

// ---------------------------------------------------------------------------
// 3. Fixtures
// ---------------------------------------------------------------------------
List<Fixture> LoadFeed(string file, string competition, string slug, int seasonYear)
{
    var path = Path.Combine(fixturesDir, file);
    if (!File.Exists(path)) throw new FileNotFoundException($"Fixture snapshot not found: {path}");
    using var doc = JsonDocument.Parse(File.ReadAllText(path));
    var list = new List<Fixture>();
    foreach (var e in doc.RootElement.EnumerateArray())
    {
        if (e.TryGetProperty("HomeTeamScore", out var hs) && hs.ValueKind == JsonValueKind.Null) continue;
        if (e.TryGetProperty("AwayTeamScore", out var asc) && asc.ValueKind == JsonValueKind.Null) continue;
        // Same parse as FixtureDownloadFixtureProvider.TryParseDate.
        var kickoff = DateTime.Parse(e.GetProperty("DateUtc").GetString()!, CultureInfo.InvariantCulture,
            DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal);
        list.Add(new Fixture(
            competition, slug, seasonYear,
            e.GetProperty("MatchNumber").GetInt32(), e.GetProperty("RoundNumber").GetInt32(),
            DateTime.SpecifyKind(kickoff, DateTimeKind.Utc),
            e.GetProperty("HomeTeam").GetString()!, e.GetProperty("AwayTeam").GetString()!,
            e.GetProperty("HomeTeamScore").GetInt32(), e.GetProperty("AwayTeamScore").GetInt32()));
    }
    return list;
}

List<Fixture> plFeed, chFeed;
try
{
    plFeed = LoadFeed("epl-2025.json", PremierLeague, "epl", 2025);
    chFeed = LoadFeed("championship-2025.json", Championship, "championship", 2025);
}
catch (Exception ex) { Console.Error.WriteLine($"ERROR loading fixtures: {ex.Message}"); return 1; }
Console.WriteLine($"  fixtures          : {plFeed.Count} Premier League, {chFeed.Count} Championship (all with final scores)");

// The feed's short forms vs. the names in the seeded Teams catalogue.
var aliases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
{
    ["Brighton"] = "Brighton & Hove Albion",
    ["Leeds"] = "Leeds United",
    ["Man City"] = "Manchester City",
    ["Man Utd"] = "Manchester United",
    ["Nott'm Forest"] = "Nottingham Forest",
    ["Spurs"] = "Tottenham",
    ["West Ham"] = "West Ham United",
    ["Wolves"] = "Wolverhampton Wanderers",
};

// ---------------------------------------------------------------------------
// 4. Connect + preflight
// ---------------------------------------------------------------------------
await using var db = new NpgsqlConnection(conn);
db.Notice += (_, e) => Console.WriteLine($"[pg] {e.Notice.MessageText}");
try { await db.OpenAsync(); }
catch (Exception ex) { Console.Error.WriteLine($"ERROR: cannot connect: {ex.Message}"); return 1; }

async Task<object?> Scalar(string sql, params (string, object?)[] ps)
{
    await using var cmd = new NpgsqlCommand(sql, db);
    foreach (var (n, v) in ps) cmd.Parameters.AddWithValue(n, v ?? DBNull.Value);
    var r = await cmd.ExecuteScalarAsync();
    return r is DBNull ? null : r;
}

var adminId = (Guid?)await Scalar("SELECT \"Id\" FROM \"Users\" WHERE \"Email\" = @e", ("e", adminEmail));
if (adminId is null) { Console.Error.WriteLine($"ERROR: admin '{adminEmail}' not found."); return 1; }

Guid groupId;
string groupSlug;
{
    await using var cmd = new NpgsqlCommand(
        groupSlugWanted is null
            ? "SELECT \"Id\", \"Slug\" FROM \"Groups\""
            : "SELECT \"Id\", \"Slug\" FROM \"Groups\" WHERE \"Slug\" = @s", db);
    if (groupSlugWanted is not null) cmd.Parameters.AddWithValue("s", groupSlugWanted);
    await using var rd = await cmd.ExecuteReaderAsync();
    var found = new List<(Guid, string)>();
    while (await rd.ReadAsync()) found.Add((rd.GetGuid(0), rd.GetString(1)));
    if (found.Count == 0) { Console.Error.WriteLine($"ERROR: no group matched (SEED_GROUP_SLUG={groupSlugWanted ?? "<any>"})."); return 1; }
    if (found.Count > 1)
    {
        Console.Error.WriteLine($"ERROR: {found.Count} groups exist; set SEED_GROUP_SLUG to pick one: {string.Join(", ", found.Select(f => f.Item2))}");
        return 1;
    }
    (groupId, groupSlug) = found[0];
}

if (!string.Equals(confirm, groupSlug, StringComparison.Ordinal))
{
    Console.Error.WriteLine($"ERROR: SEED_CONFIRM='{confirm}' does not match the target group slug '{groupSlug}'. Nothing was changed.");
    return 1;
}
Console.WriteLine($"  target group      : {groupSlug} ({groupId})");

var seasonId = Det($"palpitao-{tag}:season");

// Guard: never seed on top of a season somebody is actually using.
{
    await using var cmd = new NpgsqlCommand("SELECT \"Id\", \"Name\" FROM \"Seasons\" WHERE \"GroupId\" = @g", db);
    cmd.Parameters.AddWithValue("g", groupId);
    await using var rd = await cmd.ExecuteReaderAsync();
    var foreignSeasons = new List<string>();
    var ours = false;
    while (await rd.ReadAsync())
    {
        if (rd.GetGuid(0) == seasonId) ours = true;
        else foreignSeasons.Add($"{rd.GetString(1)} ({rd.GetGuid(0)})");
    }
    if (foreignSeasons.Count > 0)
    {
        Console.Error.WriteLine($"ERROR: the group already has {foreignSeasons.Count} season(s) this seeder does not own; refusing:");
        foreach (var s in foreignSeasons) Console.Error.WriteLine($"  - {s}");
        return 1;
    }
    if (ours && !replace)
    {
        var existingRounds = await Scalar("SELECT count(*) FROM \"Rounds\" WHERE \"SeasonId\" = @s", ("s", seasonId));
        Console.Error.WriteLine($"REFUSED: demo season {seasonId} already exists ({existingRounds} rounds). Re-run with SEED_REPLACE=true to rebuild it.");
        return 2;
    }
}

// A persisted SeasonScoringConfig would take the classic set from ScoringClassicTeams
// instead of Teams.IsBigSevenClub, quietly collapsing every multiplier to 1.
if (Convert.ToInt64(await Scalar("SELECT count(*) FROM \"SeasonScoringConfigs\" WHERE \"SeasonId\" = @s", ("s", seasonId))!) > 0
    && !replace)
{
    Console.Error.WriteLine("ERROR: a SeasonScoringConfig exists for the demo season; it would disable the classic x2 multiplier.");
    return 1;
}

// Teams catalogue
var teamsByName = new Dictionary<string, TeamRow>(StringComparer.OrdinalIgnoreCase);
{
    await using var cmd = new NpgsqlCommand(
        "SELECT \"Id\", \"Name\", \"Division\", \"IsBigSevenClub\" FROM \"Teams\" WHERE \"TeamType\" = 'Club'", db);
    await using var rd = await cmd.ExecuteReaderAsync();
    while (await rd.ReadAsync())
        teamsByName[Normalize(rd.GetString(1))] = new TeamRow(rd.GetGuid(0), rd.GetString(1),
            rd.IsDBNull(2) ? null : rd.GetInt32(2), rd.GetBoolean(3));
}
var bigSevenIds = teamsByName.Values.Where(t => t.IsBigSeven).Select(t => t.Id).ToHashSet();
if (bigSevenIds.Count != 7)
{
    Console.Error.WriteLine($"ERROR: expected exactly 7 Big Seven clubs, found {bigSevenIds.Count} -- the x2 classic multiplier cannot be exercised.");
    return 1;
}

Guid? Resolve(string feedName)
{
    var name = aliases.TryGetValue(feedName.Trim(), out var mapped) ? mapped : feedName;
    return teamsByName.TryGetValue(Normalize(name), out var t) ? t.Id : null;
}

var unresolved = plFeed.Concat(chFeed)
    .SelectMany(f => new[] { f.Home, f.Away }).Distinct(StringComparer.OrdinalIgnoreCase)
    .Where(n => Resolve(n) is null).OrderBy(n => n).ToList();
if (unresolved.Count > 0)
{
    Console.Error.WriteLine($"ERROR: {unresolved.Count} feed team name(s) are not in the Teams catalogue. Add an alias -- never auto-create a team:");
    foreach (var n in unresolved)
    {
        var near = teamsByName.Values.OrderBy(t => Distance(Normalize(n), Normalize(t.Name))).Take(3).Select(t => t.Name);
        Console.Error.WriteLine($"  - \"{n}\"  ->  closest: {string.Join(", ", near)}");
    }
    return 1;
}
Console.WriteLine($"  teams resolved    : {teamsByName.Count} clubs in catalogue, 0 unresolved feed names");

// ---------------------------------------------------------------------------
// 5. Round composition -- pair Premier League and Championship BY DATE
// ---------------------------------------------------------------------------
// Championship matchday k runs 8 to 71 days before Premier League matchday k, so
// pairing by index would produce rounds whose matches are two months apart.
var consumedCh = new HashSet<int>();
var rounds = new List<SeededRound>();

for (var k = 1; k <= playedRounds; k++)
{
    var matchday = plFeed.Where(f => f.RoundNumber == k).OrderBy(f => f.Kickoff).ToList();
    if (matchday.Count == 0) { Console.Error.WriteLine($"ERROR: Premier League matchday {k} is empty in the feed."); return 1; }

    // Dense core: drop rescheduled outliers (matchday 31 alone spans 83 days).
    var median = matchday[matchday.Count / 2].Kickoff;
    var core = matchday.Where(f => Math.Abs((f.Kickoff - median).TotalDays) <= coreWindowDays).ToList();
    if (core.Count < plPerRound)
    {
        Console.Error.WriteLine($"ERROR: round {k} has only {core.Count} Premier League fixtures within +/-{coreWindowDays}d of the median; need {plPerRound}.");
        return 1;
    }

    var picked = new List<Fixture>();
    var usedClubs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    bool TryTake(Fixture f)
    {
        if (usedClubs.Contains(f.Home) || usedClubs.Contains(f.Away)) return false;
        picked.Add(f); usedClubs.Add(f.Home); usedClubs.Add(f.Away); return true;
    }

    // Classics first, so the x2 multiplier shows up wherever the season offers one.
    foreach (var f in core.Where(f => IsBig(f.Home) && IsBig(f.Away)).OrderBy(f => f.Kickoff))
        if (picked.Count < plPerRound) TryTake(f);
    foreach (var f in core.OrderBy(f => f.Kickoff))
        if (picked.Count < plPerRound && !picked.Contains(f)) TryTake(f);

    var plCount = picked.Count;

    // Championship: nearest unconsumed fixtures to this matchday's median.
    foreach (var f in chFeed
        .Where(f => !consumedCh.Contains(f.MatchNumber) && Math.Abs((f.Kickoff - median).TotalDays) <= chMaxDriftDays)
        .OrderBy(f => Math.Abs((f.Kickoff - median).TotalDays)))
    {
        if (picked.Count >= plPerRound + chPerRound) break;
        if (TryTake(f)) consumedCh.Add(f.MatchNumber);
    }
    var chCount = picked.Count - plCount;

    // The Championship season ends ~3 matchdays before the Premier League's, so the
    // last rounds degrade to Premier-League-only -- which is what actually happened.
    foreach (var f in core.OrderBy(f => f.Kickoff))
        if (picked.Count < plPerRound + chPerRound && !picked.Contains(f)) TryTake(f);

    var ordered = picked
        .OrderBy(f => f.Kickoff).ThenBy(f => f.Competition, StringComparer.Ordinal).ThenBy(f => f.Home, StringComparer.Ordinal)
        .ToList();

    var firstKickoff = ordered[0].Kickoff;
    var lastKickoff = ordered[^1].Kickoff;
    var publishedAt = firstKickoff.AddHours(-publishLeadHours);

    rounds.Add(new SeededRound(
        Id: Det($"palpitao-{tag}:round:{k}"),
        Number: k,
        Title: $"Rodada {k}",
        Matches: ordered,
        FirstMatchStartsAt: firstKickoff,
        LastKickoff: lastKickoff,
        PublishedAt: publishedAt,
        // min(PublishedAt + 24h, FirstMatchStartsAt) -- the 24h branch, since the lead is > 24h.
        FlavioDeadlineUtc: k >= 16 ? publishedAt.AddHours(24) : null,
        FlavioApplies: k >= 16,
        PlCount: plCount,
        ChCount: chCount));
}

var classics = rounds.Sum(r => r.Matches.Count(m => m.Competition == PremierLeague && IsBig(m.Home) && IsBig(m.Away)));
var chLight = rounds.Where(r => r.ChCount < chPerRound).Select(r => $"R{r.Number}({r.ChCount})").ToList();
Console.WriteLine($"""
      composition       : {rounds.Sum(r => r.Matches.Count)} matches over {rounds.Count} rounds
                          {classics} classic (Big7 x Big7) fixtures -> x2 multiplier
                          rounds short of {chPerRound} Championship: {(chLight.Count == 0 ? "none" : string.Join(", ", chLight))}
    """);

// ---------------------------------------------------------------------------
// 6. Predictions
// ---------------------------------------------------------------------------
// Truncated-Poisson prior over 0..5 goals a side. Lambdas sit deliberately below the
// real means because punters are conservative; the resulting 1X2 split (43.5/27.4/29.1)
// tracks the real 2025/26 Premier League season (42.6/27.4/30.0) almost exactly.
var priorCache = new Dictionary<(string, bool, bool), double[]>();
double[] Prior(string competition, bool homeBig, bool awayBig)
{
    if (priorCache.TryGetValue((competition, homeBig, awayBig), out var cached)) return cached;
    var (lh, la) = competition == PremierLeague ? (1.38, 1.08) : (1.30, 1.05);
    if (homeBig && !awayBig) { lh *= 1.25; la *= 0.80; }
    else if (awayBig && !homeBig) { lh *= 0.85; la *= 1.20; }
    else if (homeBig && awayBig) { lh *= 1.05; }

    var ph = TruncPoisson(lh);
    var pa = TruncPoisson(la);
    var table = new double[36];
    var sum = 0.0;
    for (var h = 0; h <= 5; h++)
        for (var a = 0; a <= 5; a++) { table[h * 6 + a] = ph[h] * pa[a]; sum += table[h * 6 + a]; }
    for (var i = 0; i < 36; i++) table[i] /= sum;
    priorCache[(competition, homeBig, awayBig)] = table;
    return table;
}

var predictions = new List<SeededPrediction>();
foreach (var round in rounds)
{
    var deadline = round.FlavioDeadlineUtc ?? round.PublishedAt.AddHours(24);
    foreach (var p in roster)
    {
        if (p.AbsentRounds.Contains(round.Number)) continue;
        if (p.EliminatedAfterRound is { } elim && round.Number > elim) continue; // eliminated -> not on the roster

        // A partial card is one short, which AbsenceService reads as an absence.
        var take = p.PartialRounds.Contains(round.Number) ? round.Matches.Count - 1 : round.Matches.Count;

        // Form makes the weekly winner rotate; without it one person tops every round.
        var form = 1 + formAmplitude * (2 * new SplitMix64(Hash(masterSeed, "form", p.Email, round.Number.ToString())).NextDouble() - 1);
        var e = Math.Clamp(p.Exact * form, 0.02, 0.40);
        var c = Math.Clamp(p.Column * form, 0.10, 0.55);

        // On the Flavio round everyone submits late. The rule only ever fires for whoever
        // actually leads (read live from Standings at scoring time), so marking the whole
        // round late is deterministic without the seeder having to predict the leader.
        var submittedAt = round.Number == flavioRound
            ? Between(deadline.AddHours(1), round.FirstMatchStartsAt.AddHours(-2), Hash(masterSeed, "sub-late", p.Email, round.Number.ToString()))
            : Between(round.PublishedAt.AddHours(1), round.PublishedAt.AddHours(20), Hash(masterSeed, "sub", p.Email, round.Number.ToString()));

        for (var i = 0; i < take; i++)
        {
            var m = round.Matches[i];
            var rng = new SplitMix64(Hash(masterSeed, "pred", p.Email, m.ExternalId));
            var prior = Prior(m.Competition, IsBig(m.Home), IsBig(m.Away));
            var (ph, pa) = SamplePrediction(rng, prior, m.HomeScore, m.AwayScore, e, c, p.Transpose, p.Herd);
            predictions.Add(new SeededPrediction(
                Det($"palpitao-{tag}:pred:{m.ExternalId}:{p.Email}"),
                round.Id, Det($"palpitao-{tag}:match:{m.ExternalId}"), p, ph, pa, submittedAt));
        }
    }
}

Console.WriteLine($"      predictions       : {predictions.Count} rows");
foreach (var p in roster)
{
    var mine = predictions.Count(x => x.Participant == p);
    var absent = p.AbsentRounds.Count + p.PartialRounds.Count;
    var note = p.EliminatedAfterRound is { } er ? $"eliminated after R{er}" : absent >= 3 ? $"{absent} absences" : absent > 0 ? $"{absent} absence(s)" : "";
    Console.WriteLine($"        {p.Name,-18} {mine,5} predictions  {note}");
}

// ---------------------------------------------------------------------------
// 7. Write
// ---------------------------------------------------------------------------
var now = DateTime.UtcNow;
await using var tx = await db.BeginTransactionAsync();
try
{
    async Task<int> Exec(string sql, params (string, object?)[] ps)
    {
        await using var cmd = new NpgsqlCommand(sql, db, tx) { CommandTimeout = Int("SQL_TIMEOUT_SECONDS", 300) };
        foreach (var (n, v) in ps) cmd.Parameters.AddWithValue(n, v ?? DBNull.Value);
        return await cmd.ExecuteNonQueryAsync();
    }

    // --- Users: adopt by email, so a re-run never orphans existing accounts ----
    var hash = BCrypt.Net.BCrypt.HashPassword(participantPassword);
    await using (var cmd = new NpgsqlCommand("""
        INSERT INTO "Users" ("Id","Name","Email","PasswordHash","Role","Status","IsActive","ApprovedAt","CreatedAt")
        SELECT * FROM unnest(@id::uuid[], @name::text[], @email::text[], @hash::text[],
                             @role::text[], @status::text[], @active::boolean[], @approved::timestamptz[], @created::timestamptz[])
        ON CONFLICT ("Email") DO UPDATE SET "Name" = EXCLUDED."Name"
        RETURNING "Id","Email";
        """, db, tx))
    {
        cmd.Parameters.AddWithValue("id", roster.Select(p => Det($"palpitao-{tag}:user:{p.Email}")).ToArray());
        cmd.Parameters.AddWithValue("name", roster.Select(p => p.Name).ToArray());
        cmd.Parameters.AddWithValue("email", roster.Select(p => p.Email).ToArray());
        cmd.Parameters.AddWithValue("hash", roster.Select(_ => hash).ToArray());
        cmd.Parameters.AddWithValue("role", roster.Select(_ => "Participant").ToArray());
        cmd.Parameters.AddWithValue("status", roster.Select(_ => "Approved").ToArray());
        cmd.Parameters.AddWithValue("active", roster.Select(_ => true).ToArray());
        cmd.Parameters.AddWithValue("approved", roster.Select(_ => now).ToArray());
        cmd.Parameters.AddWithValue("created", roster.Select(_ => now).ToArray());
        await using var rd = await cmd.ExecuteReaderAsync();
        var byEmail = roster.ToDictionary(p => p.Email, StringComparer.OrdinalIgnoreCase);
        while (await rd.ReadAsync()) byEmail[rd.GetString(1)].Id = rd.GetGuid(0);
    }
    if (roster.Any(p => p.Id == Guid.Empty)) throw new InvalidOperationException("Some participants did not get an id back from the upsert.");

    // Refuse to seed anyone who administers the group: GroupQueries.ApprovedMemberships
    // filters Role='Participant', so a GroupAdmin is silently excluded from all scoring.
    await using (var cmd = new NpgsqlCommand(
        "SELECT u.\"Email\" FROM \"GroupUsers\" gu JOIN \"Users\" u ON u.\"Id\" = gu.\"UserId\" " +
        "WHERE gu.\"GroupId\" = @g AND gu.\"Role\" = 'GroupAdmin' AND gu.\"UserId\" = ANY(@ids)", db, tx))
    {
        cmd.Parameters.AddWithValue("g", groupId);
        cmd.Parameters.AddWithValue("ids", roster.Select(p => p.Id).ToArray());
        await using var rd = await cmd.ExecuteReaderAsync();
        var admins = new List<string>();
        while (await rd.ReadAsync()) admins.Add(rd.GetString(0));
        if (admins.Count > 0) throw new InvalidOperationException($"These participants are GroupAdmins and would never be scored: {string.Join(", ", admins)}");
    }

    // --- Memberships: IsEliminated MUST reset, or a re-run starts mid-ladder ---
    await using (var cmd = new NpgsqlCommand("""
        INSERT INTO "GroupUsers" ("Id","GroupId","UserId","Role","Status","IsActive","IsEliminated","ApprovedAt","ApprovedByUserId","CreatedAt","UpdatedAt")
        SELECT * FROM unnest(@id::uuid[], @gid::uuid[], @uid::uuid[], @role::text[], @status::text[],
                             @active::boolean[], @elim::boolean[], @approved::timestamptz[], @by::uuid[], @created::timestamptz[], @updated::timestamptz[])
        ON CONFLICT ("GroupId","UserId") DO UPDATE
          SET "Role" = 'Participant', "Status" = 'Approved', "IsActive" = true,
              "IsEliminated" = false, "UpdatedAt" = EXCLUDED."UpdatedAt";
        """, db, tx))
    {
        cmd.Parameters.AddWithValue("id", roster.Select(p => Det($"palpitao-{tag}:membership:{p.Email}")).ToArray());
        cmd.Parameters.AddWithValue("gid", roster.Select(_ => groupId).ToArray());
        cmd.Parameters.AddWithValue("uid", roster.Select(p => p.Id).ToArray());
        cmd.Parameters.AddWithValue("role", roster.Select(_ => "Participant").ToArray());
        cmd.Parameters.AddWithValue("status", roster.Select(_ => "Approved").ToArray());
        cmd.Parameters.AddWithValue("active", roster.Select(_ => true).ToArray());
        cmd.Parameters.AddWithValue("elim", roster.Select(_ => false).ToArray());
        cmd.Parameters.AddWithValue("approved", roster.Select(_ => now).ToArray());
        cmd.Parameters.AddWithValue("by", roster.Select(_ => adminId.Value).ToArray());
        cmd.Parameters.AddWithValue("created", roster.Select(_ => now).ToArray());
        cmd.Parameters.AddWithValue("updated", roster.Select(_ => now).ToArray());
        await cmd.ExecuteNonQueryAsync();
    }

    // --- Replace: children before parents, mirroring reset-db-keep-admin.sql ---
    if (replace)
    {
        const string inSeason = "SELECT \"Id\" FROM \"Rounds\" WHERE \"SeasonId\" = @s";
        var removed = 0;
        removed += await Exec($"DELETE FROM \"OcrPredictionCandidates\" WHERE \"RoundId\" IN ({inSeason})", ("s", seasonId));
        removed += await Exec($"DELETE FROM \"OcrImportBatches\" WHERE \"RoundId\" IN ({inSeason})", ("s", seasonId));
        removed += await Exec($"DELETE FROM \"PredictionScores\" WHERE \"RoundId\" IN ({inSeason})", ("s", seasonId));
        removed += await Exec($"DELETE FROM \"Predictions\" WHERE \"RoundId\" IN ({inSeason})", ("s", seasonId));
        removed += await Exec("DELETE FROM \"RoundParticipantResults\" WHERE \"SeasonId\" = @s", ("s", seasonId));
        removed += await Exec("DELETE FROM \"Standings\" WHERE \"SeasonId\" = @s", ("s", seasonId));
        removed += await Exec($"DELETE FROM \"AbsenceOverrides\" WHERE \"RoundId\" IN ({inSeason})", ("s", seasonId));
        removed += await Exec($"DELETE FROM \"Absences\" WHERE \"RoundId\" IN ({inSeason})", ("s", seasonId));
        removed += await Exec("DELETE FROM \"ScoringScoreEntries\" WHERE \"ConfigId\" IN (SELECT \"Id\" FROM \"SeasonScoringConfigs\" WHERE \"SeasonId\" = @s)", ("s", seasonId));
        removed += await Exec("DELETE FROM \"ScoringMultiplierRules\" WHERE \"ConfigId\" IN (SELECT \"Id\" FROM \"SeasonScoringConfigs\" WHERE \"SeasonId\" = @s)", ("s", seasonId));
        removed += await Exec("DELETE FROM \"ScoringClassicTeams\" WHERE \"ConfigId\" IN (SELECT \"Id\" FROM \"SeasonScoringConfigs\" WHERE \"SeasonId\" = @s)", ("s", seasonId));
        removed += await Exec("DELETE FROM \"SeasonScoringConfigs\" WHERE \"SeasonId\" = @s", ("s", seasonId));
        removed += await Exec($"DELETE FROM \"RoundMatches\" WHERE \"RoundId\" IN ({inSeason})", ("s", seasonId));
        removed += await Exec("DELETE FROM \"Rounds\" WHERE \"SeasonId\" = @s", ("s", seasonId));
        removed += await Exec("DELETE FROM \"Seasons\" WHERE \"Id\" = @s", ("s", seasonId));
        Console.WriteLine($"  replaced          : {removed} pre-existing rows deleted");
    }

    // --- Season. EndDate is stretched so the rehearsal round's window fits. -----
    await Exec("UPDATE \"Seasons\" SET \"IsActive\" = false WHERE \"GroupId\" = @g AND \"Id\" <> @s", ("g", groupId), ("s", seasonId));
    await Exec("""
        INSERT INTO "Seasons" ("Id","GroupId","Name","TournamentType","StartDate","EndDate","IsActive",
                               "AllowParticipantsToViewOthersPredictions","AllowParticipantsToSubmitPredictions","CreatedAt")
        VALUES (@id, @gid, @name, 'PalpitaoEngland', @start, @end, true, true, true, @created);
        """,
        ("id", seasonId), ("gid", groupId), ("name", seasonName),
        ("start", DateOnly.FromDateTime(rounds[0].FirstMatchStartsAt.AddDays(-14))),
        ("end", new DateOnly(2026, 12, 31)), ("created", now));

    // --- Rounds. Seeded as 'Scored' (the scorer accepts Locked OR Scored) so the
    //     5-minute background results refresh, which sweeps every Published/Locked
    //     round across all groups, cannot rewrite 380 historical results.
    var allRounds = rounds.Select(r => (
        r.Id, r.Number, r.Title, Status: "Scored",
        StartDate: (DateTime?)r.FirstMatchStartsAt.Date, EndDate: (DateTime?)r.LastKickoff.Date.AddDays(1).AddMinutes(-1),
        First: (DateTime?)r.FirstMatchStartsAt, Published: (DateTime?)r.PublishedAt,
        Locked: (DateTime?)r.FirstMatchStartsAt, Results: (DateTime?)r.LastKickoff.AddHours(2),
        Deadline: r.FlavioDeadlineUtc, Flavio: r.FlavioApplies, Created: r.PublishedAt.AddDays(-1))).ToList();

    if (withRehearsal)
    {
        // Draft, no matches, window on the 2026/27 opening weekend so fixture import
        // returns real games. PublishAsync fills in the timing fields for real.
        var start = now.Date.AddDays(rehearsalWindowDays);
        allRounds.Add((Det($"palpitao-{tag}:round:{playedRounds + 1}"), playedRounds + 1,
            $"Rodada {playedRounds + 1} — ensaio", "Draft",
            DateTime.SpecifyKind(start, DateTimeKind.Utc), DateTime.SpecifyKind(start.AddDays(7), DateTimeKind.Utc),
            null, null, null, null, null, false, now));
    }

    await using (var cmd = new NpgsqlCommand("""
        INSERT INTO "Rounds" ("Id","GroupId","SeasonId","Number","Title","StartDate","EndDate","Status",
                              "FirstMatchStartsAt","PublishedAt","LockedAt","ResultsUpdatedAt","MirrorPublishedAt",
                              "FlavioDeadlineUtc","FlavioConflictAlert","FlavioRuleApplies","FlavioRuleTargetUserId",
                              "CreatedByUserId","CreatedAt")
        SELECT i, @gid, @sid, n, t, sd, ed, st, fm, pb, lk, ru, NULL::timestamptz, fd, false, fr, NULL::uuid, @by, cr
        FROM unnest(@i::uuid[], @n::int[], @t::text[], @sd::timestamptz[], @ed::timestamptz[], @st::text[],
                    @fm::timestamptz[], @pb::timestamptz[], @lk::timestamptz[], @ru::timestamptz[],
                    @fd::timestamptz[], @fr::boolean[], @cr::timestamptz[])
             AS x(i, n, t, sd, ed, st, fm, pb, lk, ru, fd, fr, cr);
        """, db, tx))
    {
        cmd.Parameters.AddWithValue("gid", groupId);
        cmd.Parameters.AddWithValue("sid", seasonId);
        cmd.Parameters.AddWithValue("by", adminId.Value);
        cmd.Parameters.AddWithValue("i", allRounds.Select(r => r.Id).ToArray());
        cmd.Parameters.AddWithValue("n", allRounds.Select(r => r.Number).ToArray());
        cmd.Parameters.AddWithValue("t", allRounds.Select(r => r.Title).ToArray());
        cmd.Parameters.AddWithValue("sd", allRounds.Select(r => Utc(r.StartDate)).ToArray());
        cmd.Parameters.AddWithValue("ed", allRounds.Select(r => Utc(r.EndDate)).ToArray());
        cmd.Parameters.AddWithValue("st", allRounds.Select(r => r.Status).ToArray());
        cmd.Parameters.AddWithValue("fm", allRounds.Select(r => Utc(r.First)).ToArray());
        cmd.Parameters.AddWithValue("pb", allRounds.Select(r => Utc(r.Published)).ToArray());
        cmd.Parameters.AddWithValue("lk", allRounds.Select(r => Utc(r.Locked)).ToArray());
        cmd.Parameters.AddWithValue("ru", allRounds.Select(r => Utc(r.Results)).ToArray());
        cmd.Parameters.AddWithValue("fd", allRounds.Select(r => Utc(r.Deadline)).ToArray());
        cmd.Parameters.AddWithValue("fr", allRounds.Select(r => r.Flavio).ToArray());
        cmd.Parameters.AddWithValue("cr", allRounds.Select(r => Utc(r.Created)!.Value).ToArray());
        await cmd.ExecuteNonQueryAsync();
    }

    // --- Matches -------------------------------------------------------------
    var flat = rounds.SelectMany(r => r.Matches.Select((m, i) => (Round: r, Match: m, Order: i))).ToList();
    await using (var cmd = new NpgsqlCommand("""
        INSERT INTO "RoundMatches" ("Id","RoundId","Competition","Phase","HomeTeamId","AwayTeamId","StartsAt","Order",
                                    "HomeScore","AwayScore","IsFinished","Status","ResultSource","ExternalMatchId",
                                    "LastResultUpdatedAt","CreatedAt")
        SELECT i, r, c, 'Regular', h, a, s, o, hs, ascore, true, 'Finished', 'Seed', x, lu, cr
        FROM unnest(@i::uuid[], @r::uuid[], @c::text[], @h::uuid[], @a::uuid[], @s::timestamptz[], @o::int[],
                    @hs::int[], @ascore::int[], @x::text[], @lu::timestamptz[], @cr::timestamptz[])
             AS t(i, r, c, h, a, s, o, hs, ascore, x, lu, cr);
        """, db, tx))
    {
        cmd.Parameters.AddWithValue("i", flat.Select(f => Det($"palpitao-{tag}:match:{f.Match.ExternalId}")).ToArray());
        cmd.Parameters.AddWithValue("r", flat.Select(f => f.Round.Id).ToArray());
        cmd.Parameters.AddWithValue("c", flat.Select(f => f.Match.Competition).ToArray());
        cmd.Parameters.AddWithValue("h", flat.Select(f => Resolve(f.Match.Home)!.Value).ToArray());
        cmd.Parameters.AddWithValue("a", flat.Select(f => Resolve(f.Match.Away)!.Value).ToArray());
        cmd.Parameters.AddWithValue("s", flat.Select(f => f.Match.Kickoff).ToArray());
        cmd.Parameters.AddWithValue("o", flat.Select(f => f.Order).ToArray());
        cmd.Parameters.AddWithValue("hs", flat.Select(f => f.Match.HomeScore).ToArray());
        cmd.Parameters.AddWithValue("ascore", flat.Select(f => f.Match.AwayScore).ToArray());
        cmd.Parameters.AddWithValue("x", flat.Select(f => f.Match.ExternalId).ToArray());
        cmd.Parameters.AddWithValue("lu", flat.Select(f => f.Match.Kickoff.AddHours(2)).ToArray());
        cmd.Parameters.AddWithValue("cr", flat.Select(f => f.Round.PublishedAt.AddDays(-1)).ToArray());
        await cmd.ExecuteNonQueryAsync();
    }

    // --- Predictions ---------------------------------------------------------
    // unnest keeps this to 13 parameters no matter how many rows; a multi-row VALUES
    // would be ~55k parameters, uncomfortably close to the protocol's 65535 cap.
    await using (var cmd = new NpgsqlCommand("""
        INSERT INTO "Predictions" ("Id","RoundId","RoundMatchId","UserId","PredictedHomeScore","PredictedAwayScore",
                                   "ScoreCategory","Points","SubmittedAt","UpdatedAt","Source","CreatedByUserId","UpdatedByUserId")
        SELECT i, r, m, u, ph, pa, 'None', 0, s, NULL::timestamptz, 'Participant', u, NULL::uuid
        FROM unnest(@i::uuid[], @r::uuid[], @m::uuid[], @u::uuid[], @ph::int[], @pa::int[], @s::timestamptz[])
             AS t(i, r, m, u, ph, pa, s);
        """, db, tx) { CommandTimeout = Int("SQL_TIMEOUT_SECONDS", 300) })
    {
        cmd.Parameters.AddWithValue("i", predictions.Select(p => p.Id).ToArray());
        cmd.Parameters.AddWithValue("r", predictions.Select(p => p.RoundId).ToArray());
        cmd.Parameters.AddWithValue("m", predictions.Select(p => p.MatchId).ToArray());
        cmd.Parameters.AddWithValue("u", predictions.Select(p => p.Participant.Id).ToArray());
        cmd.Parameters.AddWithValue("ph", predictions.Select(p => p.Home).ToArray());
        cmd.Parameters.AddWithValue("pa", predictions.Select(p => p.Away).ToArray());
        cmd.Parameters.AddWithValue("s", predictions.Select(p => p.SubmittedAt).ToArray());
        await cmd.ExecuteNonQueryAsync();
    }

    if (dryRun)
    {
        await tx.RollbackAsync();
        Console.WriteLine("\nDRY RUN: everything executed, transaction rolled back. Nothing was written.");
        return 0;
    }

    await tx.CommitAsync();
}
catch (PostgresException pex)
{
    await tx.RollbackAsync();
    Console.Error.WriteLine($"POSTGRES ERROR [{pex.SqlState}]: {pex.MessageText}");
    if (!string.IsNullOrEmpty(pex.Detail)) Console.Error.WriteLine($"  detail: {pex.Detail}");
    if (!string.IsNullOrEmpty(pex.Where)) Console.Error.WriteLine($"  where: {pex.Where}");
    return 1;
}
catch (Exception ex)
{
    await tx.RollbackAsync();
    Console.Error.WriteLine($"ERROR: {ex.GetType().Name}: {ex.Message}");
    return 1;
}

Console.WriteLine($"""

    Seeded successfully.
      season   {seasonId}
      rounds   {rounds.Count} scored-ready{(withRehearsal ? $" + 1 empty Draft (#{playedRounds + 1})" : "")}
      matches  {rounds.Sum(r => r.Matches.Count)}
      palpites {predictions.Count} for {roster.Count} participants

    Next: run scripts/rehearsal/score-season.ps1 so the real scorer produces
    PredictionScores / RoundParticipantResults / Absences / Standings.
    """);
return 0;

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------
bool IsBig(string feedName) => Resolve(feedName) is { } id && bigSevenIds.Contains(id);

static DateTime? Utc(DateTime? d) => d is null ? null : DateTime.SpecifyKind(d.Value, DateTimeKind.Utc);

static string Normalize(string s)
{
    // Mirrors FootballReference.Normalize: trim, collapse whitespace, lowercase.
    var collapsed = string.Join(' ', s.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
    return collapsed.ToLowerInvariant();
}

static string EmailSlug(string name)
{
    var sb = new StringBuilder();
    foreach (var ch in name.Normalize(NormalizationForm.FormD))
    {
        if (CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark) continue;
        if (char.IsLetterOrDigit(ch)) sb.Append(char.ToLowerInvariant(ch));
        else if (ch == ' ' && sb.Length > 0 && sb[^1] != '.') sb.Append('.');
    }
    return sb.ToString().Trim('.');
}

static Guid Det(string key) => new(MD5.HashData(Encoding.UTF8.GetBytes(key)));

static ulong Hash(ulong seed, string stream, string who, string what)
{
    var h = 14695981039346656037UL;
    foreach (var b in Encoding.UTF8.GetBytes($"{seed}|{stream}|{who}|{what}")) { h ^= b; h *= 1099511628211UL; }
    return h;
}

static DateTime Between(DateTime from, DateTime to, ulong seed)
{
    var span = (to - from).TotalSeconds;
    if (span <= 0) return from;
    return DateTime.SpecifyKind(from.AddSeconds(new SplitMix64(seed).NextDouble() * span), DateTimeKind.Utc);
}

static double[] TruncPoisson(double lambda)
{
    var p = new double[6];
    var term = Math.Exp(-lambda);
    var sum = 0.0;
    for (var k = 0; k <= 5; k++)
    {
        if (k > 0) term *= lambda / k;
        p[k] = term; sum += term;
    }
    for (var k = 0; k <= 5; k++) p[k] /= sum;
    return p;
}

static int Column(int h, int a) => h > a ? 0 : h == a ? 1 : 2;

// Four mutually exclusive branches keep each participant's realised exact/column
// rate exactly equal to their parameters, while every emitted scoreline still
// comes from a realistic distribution.
static (int, int) SamplePrediction(SplitMix64 rng, double[] prior, int ah, int aa, double e, double c, double transpose, double herd)
{
    var u = rng.NextDouble();
    if (u < e) return (ah, aa);

    var actualColumn = Column(ah, aa);
    var exactIndex = ah * 6 + aa;

    if (u < e + c)
        return Pick(rng, prior, i => i != exactIndex && Column(i / 6, i % 6) == actualColumn, herd, (ah, aa));

    // Miss. Transposing a real scoreline is what people actually do; a transposed
    // draw would be the exact score, so those fall through to a plain miss.
    if (ah != aa && rng.NextDouble() < transpose) return (aa, ah);
    return Pick(rng, prior, i => Column(i / 6, i % 6) != actualColumn, herd, (ah, aa));
}

static (int, int) Pick(SplitMix64 rng, double[] prior, Func<int, bool> allowed, double herd, (int, int) fallback)
{
    double total = 0, best = -1;
    var bestIndex = -1;
    for (var i = 0; i < 36; i++)
    {
        if (!allowed(i)) continue;
        total += prior[i];
        if (prior[i] > best) { best = prior[i]; bestIndex = i; }
    }
    if (bestIndex < 0 || total <= 0) return fallback;

    // Herding: everyone writing 1-1 is the most realistic thing about a mirror.
    if (rng.NextDouble() < herd) return (bestIndex / 6, bestIndex % 6);

    var target = rng.NextDouble() * total;
    for (var i = 0; i < 36; i++)
    {
        if (!allowed(i)) continue;
        target -= prior[i];
        if (target <= 0) return (i / 6, i % 6);
    }
    return (bestIndex / 6, bestIndex % 6);
}

static int Distance(string a, string b)
{
    var d = new int[a.Length + 1, b.Length + 1];
    for (var i = 0; i <= a.Length; i++) d[i, 0] = i;
    for (var j = 0; j <= b.Length; j++) d[0, j] = j;
    for (var i = 1; i <= a.Length; i++)
        for (var j = 1; j <= b.Length; j++)
            d[i, j] = Math.Min(Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1), d[i - 1, j - 1] + (a[i - 1] == b[j - 1] ? 0 : 1));
    return d[a.Length, b.Length];
}

// ---------------------------------------------------------------------------
// Types
// ---------------------------------------------------------------------------

/// <summary>SplitMix64. Deterministic across processes, unlike string.GetHashCode().</summary>
sealed class SplitMix64(ulong seed)
{
    private ulong _state = seed;

    public ulong Next()
    {
        _state += 0x9E3779B97F4A7C15UL;
        var z = _state;
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
        return z ^ (z >> 31);
    }

    public double NextDouble() => (Next() >> 11) * (1.0 / 9007199254740992.0);
}

sealed class Participant(string name, double exact, double column, double transpose, double herd)
{
    public string Name { get; } = name;
    public double Exact { get; } = exact;
    public double Column { get; } = column;
    public double Transpose { get; } = transpose;
    public double Herd { get; } = herd;
    public string Email { get; set; } = "";
    public Guid Id { get; set; }
    public HashSet<int> AbsentRounds { get; } = [];
    public HashSet<int> PartialRounds { get; } = [];
    public int? EliminatedAfterRound { get; set; }
}

sealed record TeamRow(Guid Id, string Name, int? Division, bool IsBigSeven);

sealed record Fixture(string Competition, string Slug, int SeasonYear, int MatchNumber, int RoundNumber,
    DateTime Kickoff, string Home, string Away, int HomeScore, int AwayScore)
{
    /// <summary>Same shape FixtureDownloadFixtureProvider emits, so results-side id matching lines up.</summary>
    public string ExternalId => $"fixturedownload-{Slug}-{SeasonYear}-{MatchNumber}";
}

sealed record SeededRound(Guid Id, int Number, string Title, List<Fixture> Matches,
    DateTime FirstMatchStartsAt, DateTime LastKickoff, DateTime PublishedAt,
    DateTime? FlavioDeadlineUtc, bool FlavioApplies, int PlCount, int ChCount);

sealed record SeededPrediction(Guid Id, Guid RoundId, Guid MatchId, Participant Participant,
    int Home, int Away, DateTime SubmittedAt);
