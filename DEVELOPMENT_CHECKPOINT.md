# DEVELOPMENT_CHECKPOINT

_Last updated: 2026-08-22 (public standings link — [PR #45](https://github.com/oliviobecker/palpitaoapp/pull/45), released as v1.17.0 and deployed: a key-addressed, account-free standings and scoring audit)._

## 0. Status at a glance

| Check | Result |
|---|---|
| Backend build (`dotnet build`) | ✅ 0 errors (1 pre-existing xUnit2012 analyzer warning) |
| Backend tests (`dotnet test`) | ✅ **855** passed, 0 failed |
| Frontend build (`ng build` prod) | ✅ success |
| Frontend lint (`ng lint`) | ✅ 0 errors |
| Frontend unit tests (Vitest) | ✅ **149** passed (26 files) |
| Frontend e2e (Playwright) | ✅ **64** passed |
| Frontend prod budgets | ✅ within budget (no warnings) |
| i18n parity | ✅ 787 = 787 (`en-US` / `pt-BR`) |
| Working tree | `main` at `d1daf6a`. Clean apart from in-progress `delete-rounds` work. |

> Measured on `main` at `d1daf6a` (2026-08-22), after PRs #43–#46.
>
> ⚠️ **`format:check` fails locally and that is expected.** The working copy is CRLF
> (`core.autocrlf=true`) while Prettier's default `endOfLine` is `lf`, so ~56 files report as
> unformatted with no real diff. `prettier --check --end-of-line auto` passes; CI checks out LF
> and passes too. Do not "fix" it by rewriting every file.
>
> ⚠️ **The PR-level CI workflow is `disabled_manually`** — `gh pr checks` reports nothing, ever.
> Run the gates locally, **after merging `main` into the branch**: `main` moves during long
> sessions (PR #45 was cut before #43/#44 landed and had to be re-verified on the merged tree).
> What a push to `main` *does* run: staging deploy, then semantic-release → production deploy,
> and `deploy-iis.yml` runs the full `dotnet test` + `npm run build` before publishing.

## 1. Project overview

**Palpitão / FanPicks** — a multi-group football **prediction pool** ("bolão"). Each group is an
independent pool with its own admins, participants, seasons, rounds, matches, predictions and
standings; data never crosses groups. Two tournament types (per **season**): **Palpitão England**
(Premier League, FA Cup, Championship, League One) and **FIFA World Cup** (national teams).
Mobile-first. PT/EN at runtime.

Flow: admin creates a round with matches → participants predict scores until the first kickoff →
admin locks, enters results and scores → system applies multipliers, absences and the Flávio Rule →
overall standings update.

## 2. Stack

- **Backend:** C# / .NET 10, ASP.NET Core Web API (controllers), EF Core 10 (code-first) + PostgreSQL 16.
  Auth: JWT Bearer (access + rotating refresh tokens) + BCrypt. Sentry. Tesseract (OCR).
- **Frontend:** Angular 21 (standalone, signals), TypeScript, Bootstrap 5 (mobile-first),
  ngx-translate, **Lucide icons** (`@lucide/angular` via the `<app-icon>` wrapper), **light/dark
  theme** (Bootstrap `data-bs-theme` + CSS-variable tokens). Tests: Vitest
  (`@angular/build:unit-test`) + Playwright (e2e).
- **Repo:** monorepo — `backend/` (`src/Palpitao.Api`, `tests/Palpitao.Api.Tests`), `frontend/`,
  `docker-compose.yml` (Postgres 16), `README.md`.

## 3. Implemented features

**Core domain**
- Round lifecycle `Draft → Published → Locked → Scored` (+ `Cancelled`), now with a **guided stepper**
  in the admin round-detail screen (one primary action per state, prerequisites enforced).
- **Reopen** a Scored round back to Locked (admin), keeping scores until recalculated.
- Predictions per match (editable while Published, deadline = **one minute before** the first
  kickoff); prediction **mirror** released when predictions close (or live, per season setting);
  **scoring** by column/exact score with **multipliers** by competition/phase/classic.
- **Absences** (progressive penalties, elimination on the 5th) + **Flávio Rule** (England: from the
  season's configured round, live leader; World Cup: quarter-finals+, leader captured at
  publication). Both **configurable per season** in admin → *Regras de pontuação*.
- **Overall standings** (idempotent recompute); **temporary standings** while in play.
- **Two tournament types** per season (England / FIFA World Cup), fixed after creation; World Cup uses
  seeded national-team world champions for the knockout "classic" multiplier.
- Admin: manual predictions, **OCR import** (Tesseract, review-before-confirm), external **fixture
  import** (OneFootball default; the four England competitions + FIFA World Cup), **results refresh**
  (OneFootball provider) + periodic background refresh.

**Multi-tenant / auth**
- Groups (tenants) with per-group `GroupAdmin`/`Participant` roles; public **create-group** and
  **register-into-a-group** (per-group approval); group switcher; `X-Group-Id` header validated
  server-side on every call (`CurrentGroupService`).
- **Refresh tokens** (rotate on `/auth/refresh`, revoke on `/auth/logout`, stored hashed); frontend
  interceptor refreshes-and-retries once on 401.
- **Awaiting-approval screen** (`/pending`): after login with no active group, shows pending/rejected/
  **deactivated** memberships, with re-check and logout.
- **Per-group deactivation** is now a real access gate: a deactivated member (`GroupUser.IsActive=false`)
  is blocked from that group (403 `group.membershipInactive`); SuperAdmin bypasses; `User.IsActive`
  remains the global account login gate.

**Usability / polish**
- HTTP error messages localized (PT/EN) via `errors.*` keys; per-page **error states** with retry on
  the 5 participant fetch screens (predictions, results, rounds, standings, temporary-standings);
  shared `error-state` component.
- aria-labels on icon-only buttons; confirmations before Lock/Finalize/Reopen; multiplier justification
  visibly required; tournament type locked on edit (UI + backend).
- Shared `round-results-editor` (used inline in round-detail; dedicated admin `/results` route removed).

**UI modernization pack (this session)**
- Shared `page-header` (trail + title + actions) unifying screen headers; **error-state triad**
  (loading/error/empty) now also on the admin home + all admin list screens (`admin-rounds`,
  `admin-participants`, `admin-seasons`, `admin-registration-requests`, `admin-audit`, `admin-matches`).
- **Skeleton loaders** (`app-skeleton` + `app-skeleton-list`) replacing spinners on dashboard, rounds,
  standings, admin-rounds; subtle `.fade-in-up` entrance (both honour `prefers-reduced-motion`).
- **Light/dark theme**: `ThemeService` (persisted, follows OS until overridden), no-FOUC inline script
  in `index.html`, dark token overrides + a navbar sun/moon toggle.
- **Lucide icons** via `<app-icon name="…">` (registered in `app.config.ts`) replacing emoji across the
  **whole UI** — shell, state components, dashboards, participant **and** admin screens (`.icon-tile`
  accent flows to the icon via `currentColor`). Only brand `⚽` logos, WhatsApp message content
  (`*-message.util.ts`) and a `<select>` `✓` remain as text by design.
- **Predictions local draft**: edits persist to `localStorage` (per round) and restore on return; a
  sticky status bar shows remaining/all-filled + an unsaved badge; draft cleared on successful save.

**OCR + admin flow smoothing (this session)**
- **OCR review**: image preview beside the candidates (sticky on desktop), client-side file guard
  (extension + 10 MB), debounced **autosave per candidate** (no per-card Save; Confirm blocked while
  saves are in flight), confidence % + missing-field reasons on each card, **discard candidate**
  (`DELETE admin/ocr-imports/{batchId}/candidates/{id}`) so noise no longer blocks the batch, and
  post-confirm navigation back to the round detail.
- **OCR backend guards**: confirm/cancel/update reject already-confirmed batches; confirm requires
  Processed/Reviewed and rejects duplicate participant+match candidates; `Confidence` recalculated on
  edit; per-admin **rate limit** on the upload endpoint (`RateLimiting:Ocr`, 5/min default); request
  size limit derived from the shared 10 MB constant.
- **Results entry**: `round-results-editor` no longer prefills 0×0 — only complete score pairs are
  saved (pair validator + count on the button), so partial entry can't mark unplayed matches finished;
  typing a score auto-advances the focus.
- **Prediction coverage**: `GET admin/rounds/{id}/predictions/coverage` + a Published-step panel in the
  round detail showing who still hasn't predicted; manual predictions navigate back after saving.

**Stored OCR images**
- Uploads are now **persisted in Postgres** (`OcrImportImages`, `bytea`) instead of being discarded
  after Tesseract runs. It is a **1:1 side table**, never a column on `OcrImportBatch` — EF has no
  lazy scalars, so a blob on the batch would be pulled by every candidate autosave. The dead
  `StoredFilePath` column was dropped in the same migration.
- **Viewing**: `GET admin/rounds/{id}/ocr-imports` (summaries, no bytes) and
  `GET admin/ocr-imports/{id}/image` (bytes, `nosniff` + CSP `default-src 'none'; sandbox` +
  `private, immutable` cache + SHA-256 ETag → 304). Own rate-limit policy `RateLimiting:OcrImage`
  (60/min) — the 5/min upload throttle would reject a gallery. The frontend fetches images as blobs
  through `HttpClient` (so the bearer/group interceptors apply) and wraps them in object URLs
  (`OcrImageService`), shown in a root-level lightbox (`ImageViewer`, modelled on `ConfirmDialog`).
  New page `/admin/rounds/:id/import-history`; the review screen keeps its image across a reload via
  a `?batch=` query param.
- **Validation**: uploads are now sniffed by **magic bytes** (PNG/JPEG/WebP) and cross-checked against
  the claimed extension → `ocr.contentMismatch` (422) *before* any row is written. A fake `.png` no
  longer produces a junk `Failed` batch.
- **Storage footprint** (`OcrStorage` in appsettings): `StoreImages` kill switch,
  `MaxImagesPerRound` (10) pruned synchronously on upload, `RetentionDays` (180) swept daily by
  `OcrImageRetentionBackgroundService` (Postgres advisory lock, single-runner). Pruning removes
  **only the bytes** — batches, candidates and audit survive. Expect ~1 GB/season/group otherwise.

**Reading real WhatsApp screenshots (this session)**
- Diagnosed from production: no image had actually failed OCR. The two red "Falhou" cards were
  imports the admin had **discarded** (`CancelAsync` stored `Failed`), and the real problem was the
  parser losing the participant, so every card had to be fixed by hand.
- **Parser** (`OcrTextParser`): strips the screenshot clock before anything else (its colon read as
  `Name: content` and ate the last fixture of the block), makes the comma optional in
  `<Nome>, Rodada N` (while rejecting the season title in that same shape), reads `PALPITES <nome>`
  including ALL-CAPS, strips WhatsApp emphasis/quotes around a name, ignores the app's own
  vocabulary (`Hoje`, `Palpites`, …), accepts a letter-`O` score pair when it stands alone as its
  own token, and tries every score pair on a line rather than only the first.
- **Matcher**: the one-edit tier now also bridges through `FootballReference.Aliases`, so `Weolves`
  → `Wolves` → Wolverhampton Wanderers. Guarded by a catalogue-wide sweep.
- **`OcrBatchStatus.Cancelled`** split from `Failed`, so a discarded import no longer reads as a
  broken one (rows cancelled before this keep `Failed`; string column, no data migration).
- **Batch participant selector** on the review screen: files every candidate against one person
  through the existing per-candidate autosave — one screenshot is one person's predictions.
- **Learned aliases** (`OcrParticipantAliases`, group-scoped): a correction the admin makes on
  confirm is remembered and used on the next import (`Paraguaio` → `PL`). Only names the matcher
  could not resolve itself, only when every row bearing that name agreed, and a later confirmation
  re-points a mapping learned from junk.
- **Truncation**: `MatchTextRaw`/`ParticipantNameRaw` are clamped to their column widths in
  `BuildCandidates`. An over-long line failed the insert *inside* the import's `try`, and the
  `catch` that records the failure re-saved the same tracked entities — so it failed again and the
  admin got an opaque 500 instead of "could not read this image".

**Admin screen for the learned aliases (this session)**
- New tab **Admin → Apelidos** (`/admin/ocr-aliases`): lists what the group has learned, re-points an
  alias at another participant, deletes one, and teaches one by hand before any screenshot needs it.
  Closes the loop opened by the alias learning — a wrong mapping no longer waits for the next import
  to be corrected.
- **`OcrAliasService` now owns `OcrParticipantAliases`** for both the import (`GetForGroupAsync`,
  `LearnAsync`, moved out of `PredictionImportService`) and the screen (list/create/update/delete),
  so the normalization and the one-meaning-per-group rule live in one place. `LearnAsync` still
  enqueues on the caller's unit of work — aliases and the predictions they describe land together.
- Rules: the alias **text is immutable** (it is the normalized lookup key — delete and create
  instead); a duplicate is rejected (`ocr.aliasAlreadyExists`) rather than silently re-pointed,
  since the existing row is already on screen; the target must be on the group's roster
  (eliminated members included, as an alias may predate the elimination).
- No migration — the table shipped with the learning itself.

**Public standings link (this session)**

- Every **season** carries an auto-generated **public key** (12 uppercase hex, shown as
  `A7C3-9F2E-4BD8`, stored unhyphenated) addressing an **account-free** standings and scoring
  audit at `/p/<key>` — also `/p?key=…`, `?rodada=N`, `?participante=<id>`. The point is to settle
  "why did Flávio get 6 on that match?" in the group chat without an admin narrating an audit
  screen: every match line prints the prediction, the category, `base × multiplier = points` and
  the rule context (classic pair, manual override, phase, absence, Flávio Rule).
- **Publishing is opt-in.** `Season.PublicStandingsEnabled` defaults to `false`, so the deploy
  exposed nothing on its own; the admin turns it on per season, can **preview** before sharing, and
  can **regenerate** the key (the old link dies immediately). Both actions are audited.
- **Two tabs.** *Geral* is the official standings with podium, name tiles, desktop columns, a name
  search and the gap to the leader; opening a row reveals the **round-by-round history**, each chip
  a deep link into that round. *Rodada* slices a round **by participant** or **by match** — the
  latter transposes the same payload client-side to show what everybody predicted on one fixture.
- **Only closed rounds** (`Locked`/`Scored`) are ever exposed, so a prediction is never readable
  while it could still be copied. A `Scored` round reports exactly what the scoring pass persisted;
  a `Locked` one is computed live and flagged `isPartial` (no absences/elimination/Flávio, per §25
  of the README). Unknown key, malformed key and unpublished season all return the **same 404**.
- **Anonymous read path, first of its kind here.** The endpoints carry a new `[IgnoreRequestGroup]`
  so `RequestGroupContext` reports no group even when a signed-in browser sends `X-Group-Id`
  (which would otherwise filter the season away and 404 a valid link). With no request group the EF
  global filter matches *every* group rather than none, so `PublicStandingsService` derives the
  tenant from the resolved season and scopes each query explicitly with `IgnoreQueryFilters()`.
  A **separate controller is mandatory**: `RequireGroup*` are action filters, so `[AllowAnonymous]`
  on an existing controller would switch off `[Authorize]` while the filter still returned 403.
  A reflection test locks that down.
- **Distribution.** The copy-ready WhatsApp **closing message** now ends with a deep link to the
  round that just closed, so the group gets the numbers and the way to check them in one paste.
  `index.html` gained Open Graph tags with a deliberately **generic** cover (`public/og-cover.jpg`):
  the crawler does not run JS and could never see a season, and a card naming the group would give
  away exactly what `noindex` protects.
- **Migration** `20260822165521_AddSeasonPublicKey` backfills a distinct key per existing season in
  SQL *before* creating the unique index (the EF-generated version would break on any database with
  two seasons), and `AppDbContext.SaveChanges` stamps one on insert, mirroring `StampCurrentGroup`.
  Applied to production on 2026-08-22 as part of v1.17.0.
- **Deduplication done in passing:** `initials()`/`avatarColor()` existed in both `standings.ts` and
  `dashboard.ts` with different lightness (the same person got a different colour per screen), and
  `.rank-avatar` was defined twice in different sizes. Now `shared/utils/avatar.util.ts` and one
  rule in `styles.scss`, alongside `.podium*`.

**Absence = nothing sent (this session).**

- **The rule changed, not the label.** `DetectAbsenteesAsync` used to flag anyone who had not
  predicted **every** match of the round, so 10 of 11 was an absence: round zeroed, a rung up the
  punishment ladder, −20 at the 3rd and elimination at the 5th. It now flags only participants who
  sent **nothing**; an incomplete set is present and scores 0 on what it skipped.
- **A real round in production triggered it** — a participant showing `Ausente` and `+11` at the
  same time in the temporary standings. Since every write path demands the full set
  (`prediction.allMatchesRequired`), a partial one is almost always what a **match added to an
  already published round** leaves behind: the participant was being punished for an admin edit.
- **The Flávio gate had to move with it** (`FlavioRuleService`): with an incomplete set no longer an
  absence, exempting it from the halving would leave the leader strictly better off omitting one
  match than sending everything late.
- **One definition across the three readers** — absence detection, the predictions mirror and the
  reactivation candidate list. The admin *coverage* list deliberately keeps the old test: "who has
  not finished" and "who will be marked absent" are now different questions, and it shows both.
- **The absence override finally has a screen.** `POST /admin/rounds/{id}/absences/override` had
  shipped with the absence module and no component ever called it, so a wrong call could not be
  corrected from the app. The coverage panel on **/admin/rounds/:id** now runs while `Published`
  **and** `Locked` and offers *Marcar presente* / *Marcar ausente* with a mandatory justification.
- **No history was rewritten** — deliberately; see §4.

## 4. Pending / not implemented (roadmap)

- **Server-side autosave** of predictions (current draft is client-side only; needs partial/incremental
  save on the backend).
- **Batch import** of predictions across participants (grid or CSV) — deferred by request.
- OCR: async processing queue if image volume ever grows (Tesseract still runs on the request
  thread). Storing the uploaded image is **done** — see "Stored OCR images" above.
- Public create-group requires a **new** email (existing user creating another group not supported).
- `AdminSentryController` (diagnostics) still uses the global role.
- Real secrets must be configured via env/user-secrets/GitHub Secrets; rotate the 3 once-public secrets.

**Public standings link — known limits (§3):**

- **`og:image` is root-relative** (`/og-cover.jpg`). No public domain is versioned anywhere in the
  repo, so an absolute URL would have to be invented; most crawlers resolve it, but if a paste
  preview ever shows no image, that one line is the place to look.
- **No per-season link preview.** The unfurl is generic by design *and* by constraint: the SPA is
  client-rendered, so a card naming the season would need SSR or a prerender endpoint.
- **The link is only as private as its holders.** There is no expiry and no per-viewer access —
  regenerating the key is the whole revocation story, and it revokes for everyone at once.

**Absence rule change — deliberately left alone (§3):**

- **Rounds already scored keep the absences the old rule recorded.** The new definition applies from
  the next scoring pass on. Revising them means `recalculate`, which also resets eliminations and
  re-scores the whole season — and lands on the §7a.1 bug. Worth deciding as its own change, with
  the numbers in hand.
- **An override can be flipped but never removed.** `ApplyOverrideAsync` upserts and there is no
  `DELETE`, so from the first click a participant stays on a manual decision for that round rather
  than returning to the automatic rule. A `DELETE /absences/override` would close it.
- **The match nobody could predict still scores 0.** The fix at the root is not letting a match into
  a published round without reopening submission for whoever already answered — a much bigger
  change than the absence rule, and it does not affect the punishment any more.

**Deferred from the security/performance hardening (intentional, with rationale):**

- **H5 — refresh token to HttpOnly cookie**: still in `localStorage`; the cookie migration depends on
  same-site vs cross-origin deployment topology (+ CSRF handling) and changes the auth flow + e2e.
- **Full RFC 7807 ProblemDetails**: error body keeps the `{ status, message, traceId }` shape; switching
  to `problem+json` would break the frontend `body.message` parser and e2e error mocks (marginal gain).
- **API versioning** (`/v1`): premature for a single first-party SPA (adds a dependency + routing).
- **`tessdata/*.traineddata` → Git LFS** (~38 MB): no history rewrite needed (they were never
  committed), but GitHub's free LFS tier gives 1 GB/month of bandwidth and every CI checkout would
  pull 38 MB — roughly 26 runs. The deploy workflows fetch them from a pinned `tessdata` commit
  with a SHA256 check instead, so only the deploy pays for them.
- **Temp-standings cache (M2)** and **list pagination (M4)**: optimizations, low urgency at current scale.

## 5. Key technical decisions

- **Certame type lives on `Season`** (`TournamentType`), not the group; immutable after creation
  (UI disables it on edit and the backend ignores changes on update).
- **Tenant isolation:** `GroupId` only on tenant roots (Season/Round/Standing/RoundParticipantResult/
  AuditLog/GroupUser); per-round entities derive the group from their parent. `Team` is a **global**
  catalogue (clubs + national teams). `CurrentGroupService` is the single access chokepoint.
- **Per-group `IsActive`/`IsEliminated`** on `GroupUser` (roster/scoring use these); `User.IsActive` is
  the account-level login gate only.
- **Defence-in-depth tenant isolation (hardening):** in addition to `CurrentGroupService`, an EF Core
  **global query filter** (driven by the DB-free `IRequestGroupContext`, via the `IGroupOwned` marker)
  scopes tenant roots to the request group, and `AppDbContext.SaveChanges` **stamps** the current group
  on inserts with an unset `GroupId`. Both are **inert** when there is no HTTP context (background
  results refresh, EF seeding, design-time, unit tests), preserving cross-group/system operations.
- **Auth abuse protection:** per-IP rate limiting on the unauthenticated auth endpoints
  (`Program.cs`, `RateLimiting:Auth`). **Scoring** runs in a DB transaction; the **background results
  refresh** is single-runner across instances via a Postgres advisory lock; external fixture/results
  HTTP clients use a **transient-retry** `DelegatingHandler` (`Common/TransientHttpRetryHandler`).
- **Scoring is idempotent:** re-scoring a round clears its `PredictionScores`/`RoundParticipantResults`
  and recomputes standings; reopening just flips status (no data wiped).
- **Club catalogue is season-versioned by hand:** the seed carries the **2026/2027** rosters (20 PL /
  24 Championship / 24 League One). A club's PK is an MD5 of its *name*, so promotion/relegation is a
  one-column `UpdateData`; clubs relegated out of the three divisions keep their row with a **null**
  `Division` (never deleted — `RoundMatch`/`ScoringClassicTeam` FKs are `Restrict`). `Division` is
  global and season-less, so the admin match editor keeps already-selected clubs in the dropdown even
  when their current division no longer matches. Provider name variants resolve through
  `FootballReference.Canonical` before import/results matching, so a spelling can't fork the catalogue.
- **Active season:** one per group; frontend resolves it via `GET /api/seasons/active` (not `rounds[0]`).
- **i18n:** runtime switching (ngx-translate); backend localizes via `Accept-Language` + `DomainMessages`.
  `en-US.json`/`pt-BR.json` kept at **key parity** (508 keys each).
- Dates/times stored in **UTC**, displayed in pt-BR locale.

## 6. Main business rules

- **Base points:** column-only 1; exact score Traditional 3 / Medium 5 / Uncommon 7 / Extra-uncommon 10;
  wrong = 0. `final = base × multiplier`.
- **Multipliers (England):** PL Big-Seven derby ×2; FA Cup semi ×2 / final ×3 (Big-Seven derby ×2);
  Championship playoffs ×2; League One every match ×2 (max 1 per round). Manual override needs justification.
- **Multipliers (World Cup):** group ×1; round of 32/16 ×2; QF/SF/3rd/final ×3; doubled for a knockout
  **classic** (both teams former world champions). Phase prevails, no stacking.
- **Absences:** absent = **sent no prediction at all**; an incomplete set counts as present and
  scores 0 on what it skipped. An admin override wins over both. 1st–2nd none; 3rd–4th −20 total;
  5th → eliminated (manual reactivate only). Per-group. The penalty, the eliminating ordinal and the
  first counting round are **per-season settings** (`SeasonScoringConfig`); those are the defaults.
- **Flávio Rule:** leader gets a 24h (or 12h) special deadline; missing it = lose half the round,
  an incomplete set included; sending **nothing** = treated as absence; ties apply to all leaders.
  The starting round is a per-season setting (default 16); the World Cup variant goes by phase.
- **FA Cup per season:** `Season.FaCupEnabled` (default on, in **/admin/seasons**, England only).
  Off → FA Cup fixtures are dropped from the fixture search and rejected on manual add/import
  (`season.faCupDisabled`); matches already in a round keep working and stay editable.
- **Login/access:** account requires `Status=Approved` + `User.IsActive`; group access additionally
  requires an `Approved` + active `GroupUser`.

## 7. Commands

**Database (Postgres via Docker):**
```bash
cp .env.example .env
docker compose up -d
```
**Backend** (`backend/`):
```bash
dotnet ef database update --project src/Palpitao.Api   # apply migrations + seed
dotnet run   --project src/Palpitao.Api                 # https://localhost:7099, http://localhost:5146
dotnet build Palpitao.slnx
dotnet test  tests/Palpitao.Api.Tests/Palpitao.Api.Tests.csproj
```
**Frontend** (`frontend/`):
```bash
npm install
npm start                  # ng serve → http://localhost:4200
npm run build              # ng build (prod)
npm run lint               # ng lint
npm test -- --watch=false  # Vitest (run once)
npm run e2e                # Playwright (starts ng serve, mocks the API)
npm run format:check       # Prettier
```
**i18n parity check:**
```bash
node -e "const f=o=>Object.entries(o).flatMap(([k,v])=>v&&typeof v==='object'?f(v).map(s=>k+'.'+s):[k]);const en=require('./frontend/public/i18n/en-US.json'),pt=require('./frontend/public/i18n/pt-BR.json');const a=new Set(f(en)),b=new Set(f(pt));console.log('onlyEn',[...a].filter(k=>!b.has(k)),'onlyPt',[...b].filter(k=>!a.has(k)));"
```

Seed dev admin: `admin@palpitao.local` / `Admin@123`.

**Database reset — keep only the super-admin (go-live wipe):**
- **Staging / production:** Actions → *Reset database (keep super-admin)*
  (`.github/workflows/reset-db.yml`) — manual dispatch; pick the environment and type its name to
  confirm. Keeps the chosen admin (by email) + the global `Teams` catalogue and wipes everything else
  (all groups too when `drop_all_groups` is on). The admin's password is preserved; only refresh
  tokens are cleared (re-login needed). Runs the SQL via `scripts/run-sql.cs` (a .NET 10 file-based
  app) using each environment's `BACKEND_CONNECTION_STRING` secret.
- **Local (Docker):** `./scripts/reset-db-keep-admin.ps1 -DropAllGroups` mirrors that wipe (omit the
  switch to keep the admin's own groups). Both paths run the same portable
  `scripts/reset-db-keep-admin.sql`.

**Scoped deletes (`scripts/delete-{group,season,rounds}.sql`)** — for removing one tenant, one
season or a season's rounds without wiping the database. There is **no delete endpoint in the app**
for any of the three, so this is the only route. Pick the narrowest one: `delete-rounds` empties a
season, `delete-season` drops a season inside a group, `delete-group` takes the whole tenant.

- Each runs **dry-run by default** (`v_dry_run := true`): every `DELETE` really executes, the true
  row counts print, then the block `RAISE`s and rolls back. Flip to `false` to commit. So the
  foreign-key order is proven against the target database before anything is lost.
- The order matters and is not obvious: five FKs into `"Groups"` are **`Restrict`**, so a plain
  `DELETE FROM "Groups"` fails; `PredictionScores.RoundMatchId` is `Restrict` while its `RoundId` is
  `Cascade`, so scores must precede matches; and `AuditLogs.GroupId` has **no FK at all**, so those
  rows orphan themselves unless deleted explicitly.
- `delete-rounds` takes every round of the season by default; `v_only_numbers` narrows it to
  specific round numbers (`'{1}'` = just Rodada 1) and `v_only_status` to one lifecycle state. A
  filter matching nothing aborts and lists the rounds that do exist.
- `delete-rounds` also resets `Standings` and `GroupUser.IsEliminated` by default — both are
  season-scoped, so deleting rounds otherwise leaves the standings screen showing points from rounds
  that no longer exist (`v_reset_standings := false` if you would rather hit *Recalcular* in the UI).
- Group/season names are matched ignoring case, padding and **dash style** (`-` vs `–` vs `—`): the
  names are typed in the UI and copied by eye, and an en dash is invisible on screen but never
  compares equal. A miss lists every existing group/season so the right name can be copied.

**Rehearsal season on staging (`scripts/rehearsal/`):** Actions → *Seed rehearsal season (STAGING
ONLY)* (`.github/workflows/seed-rehearsal-staging.yml`) seeds a full, already-played English 2025/26
season (real fixtures + real final scores from the frozen feeds in `scripts/rehearsal/fixtures/`),
12 test participants, and an empty `Draft` round to run by hand — then scores every round through
the real API and asserts the result. Phases: `all | seed | score | verify | reset-scoring`.

- **Staging only by construction:** the job hardcodes `environment: staging`; there is no
  `environment` input, so `BACKEND_CONNECTION_STRING` cannot resolve to production. Needs
  `STAGING_API_BASE_URL` (var) plus `STAGING_ADMIN_PASSWORD` and `SEED_PARTICIPANT_PASSWORD`
  (secrets) on that environment.
- **The seeder writes raw rows only** (users, memberships, season, rounds, matches, predictions).
  `PredictionScores`, `RoundParticipantResults`, `Absences`, `Standings` and `GroupUsers.IsEliminated`
  come exclusively from `POST /rounds/{id}/score`, driven in ascending order by
  `scripts/rehearsal/score-season.ps1` — the Flávio rule reads live standings, so order is load-bearing.
- **Re-scoring:** use `phase: reset-scoring`, never `POST /seasons/{id}/recalculate` — see §7a.
- `SEED_DRY_RUN=true` (the `dry_run` input) runs the whole seed in a transaction and rolls it back.

## 7a. Known scoring bugs found while building the rehearsal tooling

1. **`RecalculateSeasonAsync` is not idempotent for `PalpitaoEngland`.**
   `RoundScoringService.RecalculateSeasonCoreAsync` deletes `PredictionScores` /
   `RoundParticipantResults` / `Absences` but **never deletes `Standings`**, then re-scores with
   `updateStandings: false`. `FlavioRuleService.GetLeadersBeforeRoundAsync` therefore reads the
   previous run's *end-of-season* standings for every round ≥ 16, penalising last run's champion
   instead of the leader at that point. README §16 currently claims it is idempotent.
   Same root cause makes re-scoring a single middle round unfaithful; `reopen` + re-score is only
   correct for the highest-numbered scored round.
2. **`AdminMatches.remove()` sends no justification** (`admin-matches.ts:446`) while
   `MatchesService.remove()` supports one — deleting a match on a closed round always 422s from the UI.

## 8. Recommended next steps

1. **Fix `RecalculateSeasonCoreAsync` (§7a.1) before publicising any public link.** It never clears
   `Standings`, and the Flávio Rule reads that table — a screen that promises to explain every point
   is exactly what makes the inconsistency visible to the whole group. This is now the highest-value
   fix on the list, and its priority went up the moment the public link shipped.
2. **Smoke-test the public link on staging**: turn publishing on for one season in *Admin → Seasons*,
   open `/p/<key>` in a private window (proves it works with no session), and check a `Scored` round
   against `/admin/rounds/:id/audit` — the numbers must match exactly. Then regenerate the key and
   confirm the old link 404s.
3. Decide the **deferred hardening items** (§4): H5 token-cookie migration (needs deployment
   topology), `tessdata` → Git LFS, then the optional perf items (temp-standings cache, pagination).
4. Resume the product roadmap (§4): **server-side autosave** of predictions (highest value).

## 9. Files changed this session (highlights)

**Public standings link (PR #45).** Backend: new `Services/Standings/{I,}PublicStandingsService.cs`,
`Controllers/PublicStandingsController.cs`, `DTOs/Public/PublicStandingsDtos.cs`,
`Common/PublicKeyGenerator.cs`, `Auth/IgnoreRequestGroupAttribute.cs`, migration
`20260822165521_AddSeasonPublicKey`; touched `Entities/Season.cs`, `Data/AppDbContext.cs`
(key stamping + unique index), `Services/Groups/RequestGroupContext.cs`,
`Services/Seasons/SeasonService.cs` (+ regenerate endpoint), `Program.cs` (`"public"` rate-limit
policy). Frontend: new `features/public/public-standings.ts`,
`core/services/public-standings.service.ts`, `shared/utils/{public-link,avatar}.util.ts`,
`public/{robots.txt,og-cover.jpg}`; touched `core/interceptors/{http-context,group,auth}` (the new
`SKIP_TENANT_HEADERS` opt-out), `features/admin/{admin-seasons,admin-round-detail}.ts`,
`shared/utils/closing-message.util.ts` (the link in the group message), `index.html` (Open Graph),
`styles.scss` (`.podium*`/`.rank-avatar` moved out of two component stylesheets), both i18n files.
Docs: README §24 and §28, `PUBLIC_STANDINGS_PLAN.md`.

### Earlier sessions

**Backend:** `Services/Rounds/RoundService.cs` (+`IRoundService`, `RoundsController` — reopen);
`Services/Groups/{CurrentGroupService,GroupService,IGroupService}.cs` + `DTOs/Groups/GroupDtos.cs`
(per-group access gate, pending memberships, `MyGroupDto.IsActive`); `Controllers/AuthController.cs`
(`my-groups/pending`); `Services/Seasons/SeasonService.cs` (immutable tournament type);
`Common/DomainMessages.cs` (`round.onlyScoredReopened`, `group.membershipInactive`); tests in
`tests/Palpitao.Api.Tests/{Rounds,Groups,Admin}`.
**Frontend:** new `shared/components/{error-state,round-results-editor}`, `features/admin/round-stepper.ts`,
`features/groups/awaiting-approval.ts`; rewritten `features/admin/admin-round-detail.ts` (stepper + inline
results); deleted `features/admin/admin-results.ts`; `core/{interceptors/error.interceptor,notifications/
http-error,services/{groups,rounds,group-context},models/models}.ts`; auth (`login/register/create-group`);
5 participant screens (error states); `app.routes.ts`; `layout/shell.html`; i18n JSONs.

**Frontend (UI modernization pack):** new `shared/components/{page-header,skeleton/skeleton,skeleton/skeleton-list,icon}`;
new `core/theme/theme.service.ts`; `@lucide/angular` dependency + `provideLucideIcons(...)` in `app.config.ts`;
`index.html` (no-FOUC theme script); `styles.scss` (dark tokens, skeleton/fade-in keyframes, icon-tile
accent colour, `.app-theme`); `layout/{shell.ts,shell.html,shell.scss}` (theme toggle + Lucide chrome);
`app.ts` (ThemeService init); migrated `features/{dashboard,admin/admin,rounds/{rounds,results,predictions},
standings/standings}` + admin list screens (headers + error states + skeletons + icons);
`features/rounds/predictions.{ts,html}` (local draft + status bar); shared `error-state`/`empty-state` (Lucide).
