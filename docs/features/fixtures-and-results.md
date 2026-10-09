# Fixtures and results

Creating rounds from real fixtures, the copy-ready group messages, and refreshing live results
into temporary standings.

## Creating a round by period and importing matches

Instead of registering each match manually, the admin can **create the round by period** and
import the matches automatically from an external provider. The default provider is **OneFootball**
(free, no key, covers the four England competitions — Premier League, Championship, League One and
FA Cup — with the **current season**, and also **FIFA World Cup** national-team fixtures via the
`fifa-world-cup-12` slug). There are also `FixtureDownload`, `ApiFootball` and `TheSportsDb` as
alternatives. Switch via `Fixtures:Provider`.

> **Off-season:** in June/July OneFootball hasn't published the next season's matches yet, so the
> search comes back **empty** — that's expected (the Premier League starts in mid-August). Within
> the season (Aug–May) the matches of the four competitions appear normally.

### How it works

1. In **/admin/rounds/new**, the admin enters name/number, **start date** and **end date**. For the
   second list of a week, **Same week as round N** creates it as the next part of the previous
   round (`10.2`, which counts as one round with `10.1` for absences — [Absences](../domain-rules.md#absences)); the preview shows the
   label it will get, and warns when the previous round is already scored (the season is replayed).
2. Clicks **"Search matches"** → the backend queries the external provider
   (`POST /api/admin/fixtures/search`) and returns the matches in the period. The request carries
   the target season (`seasonId` while creating a round, `roundId` when editing one), so the search
   only asks the provider for the competitions that season's certame runs — an England season never
   queries the World Cup, and the FA Cup is left out when the season has it disabled ([Overview](../domain-rules.md#overview)). Without
   either id the search falls back to every tracked competition.
3. The matches appear **grouped by date**, with a **checkbox**, filters (competition and search by
   team), **select all**, **clear selection** and a **counter** of selected ones. Each card shows
   the competition, date/time, home × away, classic/suggested-multiplier badges and the source.
4. On save, the system creates the round and imports only the marked matches as `RoundMatch`
   (`POST /api/admin/rounds/{roundId}/matches/import`).

The same search/selection panel is available when **editing** an existing round
(**/admin/rounds/{id}/matches** → "Import matches by period"): already-added matches appear marked
as such, and "Add selected matches" imports directly into the round. The `FixtureSelection`
component is reused on both screens.

On that matches screen the search already runs **automatically on open** (pre-search): the period is
pre-filled with the round's window when defined, otherwise with the **next 8 days**, and the list is
ready for selection **if there are matches**. The pre-search is silent — if the external source is
unavailable, it shows no error toast and the admin proceeds with manual entry.

**When no match is found**, the new-round screen shows a notice and the button becomes
**"Create round and add matches manually"** — it creates the round and takes you straight to the
matches screen (add/edit manually). On the matches screen the manual add/edit form is always
available (draft/published rounds).

### Message for the group (copyable)

Once the round has matches, the **round detail** screen shows a **"Message for the group"** card
with a ready WhatsApp-style text — title, round number, **deadline (one minute before the first
kickoff)** and the
matches grouped by competition with their multipliers/phases — plus a **Copy** button that works
even on mobile (Clipboard API with fallback). Just copy and paste it into the group.

**Closing message.** Once the round is scored, the same card offers the **closing** text: final
scores, points earned in the round and the overall rank. When the season's public standings link is
published ([Public standings link](public-standings.md)) it ends with a deep link to that round's audit, so the group receives the numbers
and the way to verify them in a single paste.

**Short team names.** The messages print the clubs the way the group says them —
`Wolverhampton Wanderers` → `Wolves`, `Queens Park Rangers` → `QPR`, `Manchester United` →
`Man Utd`, `Preston North End` → `Preston` — from the table in
`frontend/src/app/shared/utils/team-name.util.ts`. This applies to the closing and Scout messages
too; names outside the table (national teams, clubs created by the fixture import) are printed
unchanged. The short form is **display only**: multipliers and the classic rule still key off the
full name, and `OcrTeamMatcher` resolves the short name back through
`FootballReference.Canonical` — the same alias map the fixture import uses — so a screenshot of a
reply still imports (see [OCR import (Tesseract)](prediction-import.md#ocr-import-tesseract)). Adding a short name that is not a prefix of the full name
means adding a row to that map as well; the frontend spec and `OcrShortNameRoundTripTests` both
fail if you don't.

**Flávio Rule in the message:** when the Flávio Rule applies to the round (England: from the
season's configured round, default 16; World Cup: quarter-finals+), and only then, the message includes a line with the current leader(s) and
their **special deadline** (e.g. "Leader @Murilo Nery has until
23:59 on Friday (22/05/2026) to predict."). The backend computes this in `RoundDto.Flavio` (leaders
= top of the season standings; deadline = 24h, or 12h if the round was published less than 24h
before the first match, with the general lock prevailing). The line only appears when the round has
already been **published** (the deadline depends on the publish time) and there is a defined leader.

Non-existing teams are **created automatically** (with the correct `IsBigSevenClub` for the seven
giants); **duplicate** matches in the round are ignored; and a **second League One match** requires
a justification. Competitions outside the system's four are ignored.

> In June/July (off-season) the "next days" pre-search usually comes back **empty** — that's
> expected, since there are no published matches. Pick a period within the season (Aug–May).

### OneFootball provider (default, free, the four competitions)

`OneFootballFixtureProvider` queries OneFootball's public web-experience API
(`api.onefootball.com/web-experience/en/competition/{slug}/fixtures`) — one request per
competition, with a **timeout** and user-agent, no login/token. The response is a nested document of
`containers`; matches are extracted by walking the tree looking for objects with
`kickoff` + `homeTeam.name` + `awayTeam.name`, filtered by the period.

| Competition | OneFootball slug |
|---|---|
| Premier League | `premier-league-9` |
| Championship | `efl-championship-27` |
| League One | `efl-league-one-42` |
| FA Cup | `fa-cup-17` |

It is resilient: if **one** competition fails, the others continue; it only turns into the friendly
error **"Could not fetch matches from the external source right now."** when **all** fail — the
**manual flow** continues. The phase comes as `Regular` (adjust the knockout multiplier on the
matches screen for an FA Cup semi/final). ⚠️ It is an **undocumented** OneFootball API; if the
structure changes, switch to another provider in one config line.

**Configuration** (`appsettings.json` → `Fixtures`, or env `Fixtures__<Field>`):

```json
"Fixtures": {
  "Provider": "OneFootball",
  "OneFootballApiBaseUrl": "https://api.onefootball.com/web-experience/en/competition",
  "TimeoutSeconds": 15,
  "EnableExternalFixtureImport": true
}
```

### fixturedownload.com provider (alternative — only PL + Championship)

With `Fixtures:Provider=FixtureDownload`: a static JSON feed `…/feed/json/{epl|championship}-{year}`,
**free and no key**, full season, but **only** Premier League + Championship (League One and FA Cup
fall back to manual entry). More stable than OneFootball since it's a static feed.

### API-Football provider (alternative — covers all four, but paid for the current season)

With `Fixtures:Provider=ApiFootball`, it uses `ApiFootballFixtureProvider`
(`v3.football.api-sports.io`, header `x-apisports-key`, leagues 39/40/41/45). It is reliable, but
⚠️ **the Free plan only covers seasons 2022–2024** — querying the current season returns `"Free
plans do not have access to this season"` (handled as a friendly error). Live data needs a paid
plan. Configure `Fixtures:ApiKey` (preferably via env `Fixtures__ApiKey` / user-secrets).

### TheSportsDB provider (alternative)

With `Fixtures:Provider=TheSportsDb` (public key `3`). ⚠️ The free key returns only a **sample** (a
few matches per season/day), so most periods come back empty — useful only with a paid Patreon key.

### Disable external import

`Fixtures:EnableExternalFixtureImport=false` (or env `Fixtures__EnableExternalFixtureImport=false`):
the search endpoint returns a friendly error and the admin uses only manual entry.

### Switching the provider in the future

The integration is isolated behind `IFixtureProvider` (no database access nor domain rules). To use
another source (SportMonks, etc.), just implement the interface and adjust the selection in
`Program.cs` — `FixtureImportService`, the controllers and the frontend don't change.

### Test with a mock

`FixtureImportServiceTests` uses a **`FakeFixtureProvider`** (period, normalization, team creation,
deduplication, League One limit, `FirstMatchStartsAt`, auditing). `OneFootballFixtureProviderTests`,
`FixtureDownloadFixtureProviderTests`, `TheSportsDbFixtureProviderTests` and
`ApiFootballFixtureProviderTests` use an **`HttpMessageHandler` stub** (slug/league, extraction of
nested match cards, period filter, resilience to partial failure, error handling) — **no test
touches the network**. On the frontend, `fixtures-import.e2e.ts` exercises search → multi-selection →
save/import with the API mocked.

## Refreshing results and temporary standings

While a round is in progress the admin can **refresh the results** and everyone sees a **temporary
standings** (preview), without officially closing the round.

### How it works

1. In **/admin/rounds/{id}** (round detail), with the round `Published` or `Locked`, there is a
   **"Refresh results"** button.
2. It calls `POST /api/admin/rounds/{roundId}/refresh-results`, which: updates the available results
   (from the external provider, if active), stamps `Round.ResultsUpdatedAt` and does **not** change
   the round status. The response carries a summary (updated/finished/in-progress/not-started).
3. The **temporary standings** are at `GET /api/rounds/{roundId}/temporary-standings`
   (authenticated) and on the screen **/rounds/{id}/temporary-standings** (mobile cards, with the
   notice "points may change until the round ends"). Participants reach it via the link on the
   results screen.

### Temporary × official

| | Temporary | Official |
|---|---|---|
| When | round in progress (refresh) | only on **Compute scoring** |
| Round status | unchanged | becomes `Scored` |
| Matches counted | only those with a result (InProgress/Finished) | all (requires all finished) |
| Absence / elimination | does **not** apply | applies |
| Flávio Rule | does **not** apply | applies |
| Season standings | does **not** change | recalculated |

A round is finalized only once **every match is finished** (`round.allMatchesFinishedRequired`,
checked again for each scored round a season recalculation replays): a live score saved by the
refresh does not count, and a match that will not finish normally needs its result entered by hand,
which marks it `Finished`.

The temporary scoring uses the **same `ScoringService`** (categories + multipliers, including the
manual override). `projectedTotalPoints = current official scoring + the round's temporary points`.

### Persistence: on-demand calculation (Option A)

The temporary standings are **computed on demand** on the `GET` (there is no snapshot table). The
refresh only updates the results on the matches and stamps `ResultsUpdatedAt`; the `GET` recomputes
from that. Justified choice: the project is small/medium, it avoids an extra table and removes the
risk of stale snapshots; the results are already persisted on the `RoundMatch`.

### Results provider

The `IResultsProvider` abstraction (isolated, no domain rules). Shipped default is
**`OneFootballResultsProvider`** (`appsettings.json` → `ResultsProvider: { "Provider":
"OneFootball", "Enabled": true }`), which reads the same undocumented web-experience API used to
import fixtures. For each competition in the round it fetches **both** tabs —
`.../competition/{slug}/fixtures` and `.../results` — because neither is authoritative: `results`
also lists not-yet-played fixtures, and a live match shows up under `fixtures`. The same match
appears on both, so the cards are merged and the **best-informed** one wins.

A card's state lives in `period` (`PRE_MATCH`, `FIRST_HALF`, `HALF_TIME`, `SECOND_HALF`,
`FULL_TIME`, `FULL_TIME_PENALTIES`, `ABANDONED`, …) with the running clock in `timePeriod` (`"66'"`
while live, `"Full time"` once played). `Services/Results/MatchStatusParser` maps that onto
`MatchStatus` for every provider: labels are normalised to letters and digits, and a label it does
not know falls back to the clock, then to the presence of a full scoreline.

**`ManualResultsProvider`** (`Provider` set to anything else, or `Enabled=false`) fetches nothing
externally — results come from **manual entry** (the results screen, which marks the match as
`Finished`), and the refresh only recomputes the temporary standings. When no external provider is
active, the endpoint responds with a clear message ("No external results provider is active…")
**without breaking**. A manually entered result is never overwritten by a provider: it is the
pool's source of truth.

To integrate a different external site/API, configure (`appsettings.json` → `ResultsProvider`, or
env `ResultsProvider__<Field>`):

```json
"ResultsProvider": { "Provider": "ConfiguredWebsite", "BaseUrl": "https://…", "Enabled": true, "TimeoutSeconds": 15 }
```

The `ConfiguredWebsiteResultsProvider` makes **one GET** (timeout + user-agent) expecting
`{ "results": [ { "homeTeam", "awayTeam", "homeScore", "awayScore", "status", "externalMatchId?", "url?" } ] }`;
if the structure changes, it fails with `results.fetchFailed` (friendly message) and the manual
flow continues.

#### Matching a row to a match in the round

1. **By external id.** The fixture import stores the provider's id (`onefootball-{matchId}`,
   `fixturedownload-…`, …) on the `RoundMatch`, so this is an exact join that does not care how
   either side spells the clubs.
2. **By team name**, through `FootballReference.Canonical` (so "Wolves" finds "Wolverhampton
   Wanderers"), for matches added by hand. Two extra conditions keep it honest: the **competition**
   has to agree — the same two clubs meet in the league and in the cup — and a match already
   carrying an id **from the same source** is off limits, since a row that only agrees on the names
   is a different game.

Matches nothing resolved to are counted as `unmatchedMatches` in the refresh response, listed in
the `ResultsRefreshed` audit entry, and shown to the admin — a silent gap here used to be
indistinguishable from "the match has not started".

### Match status (`MatchStatus`)

`NotStarted` · `InProgress` · `Finished` · `Postponed` · `Cancelled`. Only `InProgress`/`Finished`
with a score enter the temporary standings; `Postponed`/`Cancelled` are ignored. The status rides
on `MatchDto`, so the round's match list shows an **Ao vivo / Live** pill with the score so far
while the match is being played.

### How to test

- **Endpoint (manual):** publish a round with matches, register some results in
  **/admin/rounds/{id}/results** (they become `Finished`), go back to the detail and click
  **"Refresh results"** → see the summary. `GET /api/rounds/{id}/temporary-standings` shows the
  preview. The round status **stays** `Published`/`Locked`.
- **Frontend:** the button appears for the admin on the round detail; the temporary standings open at
  **/rounds/{id}/temporary-standings** (also linked on the participant's results screen).
- **Audit:** each refresh records `ResultsRefreshed` (or `ResultsRefreshFailed`) in the AuditLog with
  the provider and counts.

### Current limitations

- With `ResultsProvider:Enabled=false` (or a provider name that is not `OneFootball` /
  `ConfiguredWebsite`), **there is no automatic fetch** — the results are manual. The
  `ConfiguredWebsiteResultsProvider` is a generic base (JSON contract above), not an integration
  with a specific site.
- A match added **by hand** (not imported from a provider) has no external id, so it is joined by
  team name — a spelling the source does not share leaves it out of the refresh, and it shows up in
  the summary's **"Sem correspondência"** count.
- The temporary standings list the **whole roster**: whoever has not predicted shows on zero, which
  is exactly the signal that they are heading for an absence. Once predictions close, those rows are
  flagged `willBeAbsent` — a label taken from the absence service itself (so overrides win and it
  cannot drift from the scoring), never applied to the preview's points.
