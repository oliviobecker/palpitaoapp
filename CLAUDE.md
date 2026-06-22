# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

**Palpitão (PT) / FanPicks (EN)** — a **multi-group football prediction pool**. Each group is an
isolated tenant (its own admins, participants, seasons, rounds, matches, predictions, standings);
data never crosses groups. Monorepo: `.NET 10` backend (`backend/`) + `Angular 21` frontend
(`frontend/`). Mobile-first, PT/EN at runtime.

The full domain spec (scoring tables, multipliers, absences, Flávio Rule, fixture/results
providers, multi-tenant rules) lives in [README.md](README.md) — sections are numbered; consult it
before changing business logic. [DEVELOPMENT_CHECKPOINT.md](DEVELOPMENT_CHECKPOINT.md) tracks the
current working state, build/test status, and the roadmap.

## Environment note

The dev environment is **Windows / PowerShell** (a Bash tool is also available). Do not assume a
POSIX shell — PowerShell here-strings (`@'...'@`) are not bash heredocs, and vice-versa.

## Commands

Backend (`backend/`):
```bash
dotnet build Palpitao.slnx
dotnet test  tests/Palpitao.Api.Tests/Palpitao.Api.Tests.csproj
dotnet test  --filter "FullyQualifiedName~ScoringServiceTests"   # one test class/method
dotnet run   --project src/Palpitao.Api                          # https://localhost:7099
dotnet ef database update    --project src/Palpitao.Api          # apply migrations + seed
dotnet ef migrations add <Name> --project src/Palpitao.Api
```

Frontend (`frontend/`):
```bash
npm start                  # ng serve → http://localhost:4200
npm run build              # ng build (prod) — respects bundle budgets
npm run lint               # ng lint (must be 0 errors)
npm test -- --watch=false  # Vitest, run once
npm run e2e                # Playwright (starts ng serve, mocks the API)
npm run format:check       # Prettier (CI enforces this — run npm run format before committing)
```

Database: `docker compose up -d` (Postgres 16). Seed dev admin: `admin@palpitao.local` / `Admin@123`.

**i18n parity is mandatory** — `en-US.json` and `pt-BR.json` must have identical key sets. Every new
string goes in both. Verify:
```bash
node -e "const f=o=>Object.entries(o).flatMap(([k,v])=>v&&typeof v==='object'?f(v).map(s=>k+'.'+s):[k]);const en=require('./frontend/public/i18n/en-US.json'),pt=require('./frontend/public/i18n/pt-BR.json');const a=new Set(f(en)),b=new Set(f(pt));console.log('onlyEn',[...a].filter(k=>!b.has(k)),'onlyPt',[...b].filter(k=>!a.has(k)));"
```

## Backend architecture

ASP.NET Core controllers → services (in `src/Palpitao.Api/Services/`, one folder per area:
`Scoring`, `Tournaments`, `Rounds`, `Predictions`, `Absences`, `Flavio`, `Standings`, `Groups`,
`Fixtures`, `Results`, `Ocr`, `Auth`, …) → EF Core (`Data/AppDbContext.cs`) → PostgreSQL. Tests are
xUnit + **SQLite in-memory**.

Three patterns to understand before touching backend logic:

1. **Multi-tenant isolation chokepoint.** `GroupId` lives only on tenant *roots* (`Season`, `Round`,
   `Standing`, `RoundParticipantResult`, `AuditLog`, `GroupUser`); per-round entities
   (`Prediction`, `RoundMatch`, `Absence`, `Ocr*`…) derive their group from the parent. The frontend
   sends an `X-Group-Id` header on every authenticated call (`group.interceptor`); the backend
   **always revalidates** it in `CurrentGroupService` (the primary access chokepoint — requires an
   `Approved` + active `GroupUser`), guarded by `[RequireGroupAdmin]`/`[RequireGroupParticipant]`.
   Never trust the client's group claim. `Team` is the one **global** (non-tenant) catalogue.
   *Defence in depth:* tenant roots implement `IGroupOwned`, so `AppDbContext` adds an EF Core
   **global query filter** (driven by the DB-free `IRequestGroupContext` / `RequestGroupContext`)
   that scopes reads to the request group, and `SaveChanges` **stamps** the group on inserts with an
   unset `GroupId`. Both are **inert when there is no HTTP context** (background refresh, EF seeding,
   design-time, unit tests) — so a test or background job sees all groups; don't rely on the filter there.

2. **Two access gates, don't conflate them.** `User.IsActive` + `UserStatus.Approved` = the global
   account *login* gate. `GroupUser.IsActive` + `GroupUser.Status = Approved` = the per-group *access*
   gate (deactivation/elimination here are per-group → 403 `group.membershipInactive`). Scoring,
   roster and standings read the **per-group** flags, not the global role.

3. **Tournament type is a strategy keyed on `Season.TournamentType`** (`PalpitaoEngland` /
   `FifaWorldCup`), fixed after creation. It drives the allowed competitions/phases, the multiplier
   table, and which Flávio Rule variant applies. When adding tournament behaviour, branch on this —
   see `Services/Tournaments` and `Services/Scoring`.

**Scoring is idempotent**: re-scoring a round clears its `PredictionScores`/`RoundParticipantResults`
and recomputes; `recalculate` on a season resets eliminations and re-scores finished rounds in order.
Round lifecycle is `Draft → Published → Locked → Scored` (+ `Cancelled`); a Scored round can be
reopened to Locked without wiping scores.

External integrations are isolated behind interfaces with no domain/DB access — `IFixtureProvider`
(OneFootball default; selectable via `Fixtures:Provider`) and `IResultsProvider`. Swapping a provider
is one config line; tests stub the `HttpMessageHandler` so nothing touches the network. Both provider
HTTP clients wrap a transient-retry `DelegatingHandler` (`Common/TransientHttpRetryHandler`).

Resilience / abuse-protection invariants (keep these intact when touching the relevant paths):
unauthenticated auth endpoints are **per-IP rate limited** (`Program.cs`, tunable via
`RateLimiting:Auth`; configure the real client IP behind a proxy); **scoring runs in a DB
transaction**; the **background results refresh is single-runner across instances** via a Postgres
advisory lock (`Services/Results/ResultsRefreshBackgroundService`). Passwords go through the shared
`Common/PasswordPolicy`. Error responses carry a `traceId`.

Dates/times are stored in **UTC**, displayed in pt-BR locale. Backend messages are localized via the
`Accept-Language` header (`LocalizationService` / `DomainMessages`).

## Frontend architecture

Angular 21 **standalone components + signals + OnPush** throughout. Layout: `core/` (auth,
interceptors, models, services, theme, i18n), `shared/` (reusable components + utils), `layout/`
(responsive Shell: desktop topbar + mobile bottom nav), `features/` (auth, dashboard, rounds,
standings, admin).

UX conventions (mirror existing patterns rather than inventing new ones):

- **Icons:** never use emoji as UI icons — use the `<app-icon name="…">` wrapper over Lucide. Icons
  must be registered in `provideLucideIcons(...)` in `app.config.ts` (a `Lucide*` import that isn't
  in the provider list = an unused-import lint error). Emoji are reserved for the brand `⚽` logo and
  **WhatsApp message content** in `shared/utils/*-message.util.ts` (that emoji is sent text, keep it).
- **Loading / empty / error:** use the shared `app-skeleton`/`app-skeleton-list`, `app-empty-state`,
  `app-error-state` (with `(retry)`), and `app-page-header`. Standard order: `loading → error → empty
  → content`. Animations honour `prefers-reduced-motion`.
- **Theme:** `ThemeService` (mirrors `LanguageService`) flips Bootstrap's `data-bs-theme` and custom
  CSS-variable tokens; an inline no-FOUC script in `index.html` sets it before first paint. Dark token
  overrides and shimmer/fade keyframes live in **global** `styles.scss`, not per-component (the
  `anyComponentStyle` budget is 4kB warn / 8kB error — keep component styles small, put heavy/shared
  CSS in `styles.scss`).
- **Predictions draft:** in-progress scores persist to `localStorage` per round (client-side only)
  and restore on return, cleared on save. There is **no** server-side autosave (roadmap item).

Frontend interceptors: `group.interceptor` (adds `X-Group-Id`), an auth interceptor that refreshes +
retries **once** on a 401, and `Accept-Language` injection. The active season is resolved via
`GET /api/seasons/active` (not `rounds[0]`).

## Workflow / CI

Trunk-based on `main`; CI (`.github/workflows/ci.yml`) runs backend build+tests, frontend
format-check + build + unit + e2e, and actionlint. **Conventional Commits** drive semantic-release
(`feat` → minor, `fix` → patch, `BREAKING CHANGE` → major; `chore/ci/docs/refactor/test` ship
nothing). Merge to `main` auto-deploys staging and may release+deploy production. Commit/push only
when asked; if on `main`, branch first; never force-push or rewrite `main`'s history.

Real secrets (connection string, `Jwt:Key`, `Sentry:Dsn`, `Fixtures:ApiKey`) come only from env /
user-secrets / GitHub Secrets — versioned `appsettings*.json` and `.env.example` hold placeholders
only. See [PUBLIC_RELEASE_CHECKLIST.md](PUBLIC_RELEASE_CHECKLIST.md).
