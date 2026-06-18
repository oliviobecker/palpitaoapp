# DEVELOPMENT_CHECKPOINT

_Last updated: 2026-06-18 (UI modernization pack: shared page-header, skeleton loaders, dark mode, Lucide icons, predictions local draft)._

## 0. Status at a glance

| Check | Result |
|---|---|
| Backend build (`dotnet build`) | ✅ 0 warnings / 0 errors |
| Backend tests (`dotnet test`) | ✅ **362** passed, 0 failed |
| Frontend build (`ng build` prod) | ✅ success |
| Frontend lint (`ng lint`) | ✅ 0 errors (24 pre-existing `label-has-associated-control` warnings) |
| Frontend unit tests (Vitest) | ✅ **34** passed (10 files) |
| Frontend prod budgets | ✅ within budget (no warnings) |
| i18n parity | ✅ 508 = 508 (`en-US` / `pt-BR`) |
| Working tree | Uncommitted changes on `main` (this session). Not yet committed. |

> The UI modernization pack is frontend-only; the backend is untouched (still **362** tests green).

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
- Predictions per match (editable while Published, deadline = first kickoff); prediction **mirror**
  after lock; **scoring** by column/exact score with **multipliers** by competition/phase/classic.
- **Absences** (progressive penalties, elimination on the 5th) + **Flávio Rule** (England: round 16+,
  live leader; World Cup: quarter-finals+, leader captured at publication).
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

## 4. Pending / not implemented (roadmap)

- **Server-side autosave** of predictions (current draft is client-side only; needs partial/incremental
  save on the backend).
- **Batch import** of predictions across participants (grid or CSV) — deferred by request.
- OCR: file-size guard + progress; surface candidate confidence.
- Public create-group requires a **new** email (existing user creating another group not supported).
- `AdminSentryController` (diagnostics) still uses the global role.
- Real secrets must be configured via env/user-secrets/GitHub Secrets; rotate the 3 once-public secrets.

## 5. Key technical decisions

- **Certame type lives on `Season`** (`TournamentType`), not the group; immutable after creation
  (UI disables it on edit and the backend ignores changes on update).
- **Tenant isolation:** `GroupId` only on tenant roots (Season/Round/Standing/RoundParticipantResult/
  AuditLog/GroupUser); per-round entities derive the group from their parent. `Team` is a **global**
  catalogue (clubs + national teams). `CurrentGroupService` is the single access chokepoint.
- **Per-group `IsActive`/`IsEliminated`** on `GroupUser` (roster/scoring use these); `User.IsActive` is
  the account-level login gate only.
- **Scoring is idempotent:** re-scoring a round clears its `PredictionScores`/`RoundParticipantResults`
  and recomputes standings; reopening just flips status (no data wiped).
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
- **Absences:** 1st–2nd none; 3rd–4th −20 total; 5th → eliminated (manual reactivate only). Per-group.
- **Flávio Rule:** leader gets a 24h (or 12h) special deadline; missing it = lose half the round; no
  prediction = treated as absence; ties apply to all leaders.
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

## 8. Recommended next steps

1. **Commit** this session's work on a branch and open a PR (working tree is currently dirty on `main`).
2. Manual end-to-end pass (needs DB + API + ng serve up): toggle **dark/light** across screens; confirm
   **skeletons** on load and **error states** (incl. admin) with retry; verify the **predictions draft**
   restores on return and clears on save.
3. Finish the **Phase 4 remainder** (admin-form/auth emoji → Lucide) for a fully emoji-free UI.
4. Optional: redirect to `/pending` on a `group.membershipInactive` 403 (mid-session deactivation).

## 9. Files changed this session (highlights)

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
