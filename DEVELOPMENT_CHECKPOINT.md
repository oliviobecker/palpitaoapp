# DEVELOPMENT_CHECKPOINT

_Last updated: 2026-06-17 (end of session)._

## 0. Where we are right now

- **Default branch:** `main` (= tag **v1.1.0**). It contains the two prediction features at the
  **group** level **and a broken migration** (`AddGroupPredictionVisibility`) that crashes the app at
  boot on PostgreSQL — so a fresh deploy of `main` fails until the fix below is merged.
- **Active work branch:** `feature/participant-submission-mode` → open as **PR #8** into `main`.
  This branch **supersedes** the group-level approach and carries:
  1. **fix(db):** removes the invalid empty `UpdateData` (PostgreSQL `42601` syntax error) — also fixes `main`.
  2. **refactor:** moves the prediction settings from `Group` to **`Season`**.
  3. **feat(admin):** dashboard highlights **Create season**.
  4. **ci:** deploys apply EF migrations explicitly; `/health/db` flags pending migrations.
- **Open items needing the human (not code):**
  - Local `appsettings.json` / `appsettings.Development.json` currently hold the **real staging
    connection string** (used to run the migration manually) — **do not commit**; revert with
    `git checkout backend/src/Palpitao.Api/appsettings*.json`.
  - Run `dotnet ef database update` on the **staging DB** (and prod) to unblock it now.
  - Merge **PR #8** so `main` stops breaking on deploy.
  - **Rotate** the 3 secrets that were once public (api-sports key, Postgres password, Sentry DSN).

## 1. Overview

**Palpitão (pt) / FanPicks (en)** — a multi-group football prediction platform. Each **group** is an
independent pool (tenant) with its own seasons, rounds, matches, predictions, standings, members and
audit. An admin creates rounds with matches; participants predict scores until the first kickoff; the
system scores them, applies absences and the Flávio Rule, and keeps the overall standings — per group,
fully isolated. Two tournament types: **Palpitão England** (PL/FA Cup/Championship/League One) and
**FIFA World Cup**.

## 2. Stack

| Layer | Tech |
|---|---|
| Backend | C# / .NET 10, ASP.NET Core Web API (routes without `api/` prefix — IIS mounts at `/api`) |
| ORM / DB | EF Core 10.0.9 (code-first) + PostgreSQL (Npgsql 10) |
| Auth | JWT Bearer + BCrypt; multi-tenant via the `X-Group-Id` header |
| Backend tests | xUnit + SQLite in-memory (**332** tests) |
| Frontend | Angular 21 (standalone, signals), TypeScript, Bootstrap 5 (mobile-first) |
| i18n | ngx-translate (frontend) + `Accept-Language`/`DomainMessages` (backend), PT-BR / EN-US |
| Frontend tests | Vitest (**35** unit) + Playwright (**30** e2e, API mocked) |
| OCR | Tesseract (`por`/`eng` traineddata, not committed) |
| Monitoring | Sentry (optional, DSN via env, off by default) |
| CI/CD | GitHub Actions: CI (build/test/lint/actionlint), staging on push to main, semantic-release → prod |

## 3. Implemented features

- **Auth & accounts:** login (JWT), public self-registration into a group with admin approval,
  status gate (`PendingApproval`/`Approved`/`Rejected`/`Inactive`), audited login blocks.
- **Multi-group (multi-tenant):** `Group` tenant + `GroupUser` membership (`GroupAdmin`/`Participant`,
  per-group `IsActive`/`IsEliminated`); `CurrentGroupService` validates `X-Group-Id`;
  `[RequireGroupAdmin]`/`[RequireGroupParticipant]`. Platform **SuperAdmin** (global `UserRole.Admin`)
  gets GroupAdmin on any group. Full isolation (cross-group ⇒ 403/404).
- **Tournament types:** `Group.TournamentType` (England / FIFA World Cup); allowed competitions/phases,
  scoring multipliers, classics (Big Seven / world champions) and Flávio variant per type.
- **Rounds & matches:** lifecycle `Draft→Published→Locked→Scored`/`Cancelled`; create by period +
  external fixture import (OneFootball default; FixtureDownload/ApiFootball/TheSportsDb alternatives).
- **Predictions:** participant submit/edit before deadline; **prediction mirror** (after lock);
  admin manual entry; **OCR import** (Tesseract) with mandatory review; `Source`
  (`Participant`/`AdminManual`/`AdminOcr`).
- **Scoring/standings:** column/exact-score categories + multipliers (incl. manual override);
  absences with progressive penalties + elimination on the 5th; **Flávio Rule**; idempotent
  recalculation; temporary (live) standings.
- **Prediction visibility (per Season):** `Season.AllowParticipantsToViewOthersPredictions`
  (default false) — participants can open the mirror only when enabled (admins always); 403 otherwise.
- **Prediction submission mode (per Season):** `Season.AllowParticipantsToSubmitPredictions`
  (default true) — when false, the in-app submit endpoint returns 403 (`prediction.appSubmitDisabled`)
  and the participant screen is read-only; admin manual/OCR unaffected (never writes `Participant`).
- **Admin UI:** dashboard (hero = **Create season**), seasons (with the two prediction settings +
  "disable" warning), participants, registration requests, audit, rounds/results/scout/manual/OCR.
- **i18n / branding:** FanPicks (en) / Palpitão (pt) via `app.name`; FluentValidation localized PT/EN.
- **App version footer** (`vX.Y.Z` from the latest git tag) + PWA manifest.
- **CI/CD:** trunk-based; PR → CI; merge to main → staging; **semantic-release** tags + GitHub Release
  → production deploy (now applies EF migrations before swapping the app).

## 4. Pending / not done

- **Merge PR #8** (season-level settings + migration fix + deploy hardening). Until merged, `main`
  deploy is broken.
- **Run migrations** on the staging/prod databases; **rotate** the 3 exposed secrets.
- Real OneFootball results provider validation + enable `ResultsProvider:Enabled` (off by default).
- Email/push notifications (round open/close, approve/reject), refresh token / session renewal.
- Integration tests for the group gates; ESLint on the frontend.
- PWA PNG icons (192/512); SuperAdmin platform-admin UI; World Cup x4/x6 multiplier badges.
- Pagination/indexes for AuditLog & Standings in long seasons.

## 5. Important technical decisions

- **Prediction settings live on `Season`** (the certame instance), resolved per round via
  `Round.Season`; exposed on `RoundDto`/`RoundSummaryDto` so the participant UI gates without extra
  calls. (Earlier they were on `Group`; v1.1.0 still has that — PR #8 moves them.)
- **Mirror is the single source** for "others' predictions" (no separate endpoint); released only
  after `Locked`/`Scored` for everyone.
- **`GroupId` only on tenant roots** (Season/Round/Standing/RoundParticipantResult/AuditLog/GroupUser);
  per-round entities derive the group from the parent.
- **Migrations apply at startup** via `db.Database.Migrate()` **but the call swallows errors** (logs +
  continues) — which masked the broken migration. Mitigated by: deploys now run
  `dotnet ef database update` explicitly (fail the deploy on error) + `/health/db` reports pending
  migrations (guarded so `EnsureCreated` dev/test DBs stay healthy).
- **EF gotcha:** an empty `UpdateData(columns: [], values: [])` renders as `UPDATE … SET WHERE …`,
  valid on SQLite but a syntax error on PostgreSQL — never scaffold/keep one.
- Secrets only via env / user-secrets / GitHub Secrets; `appsettings*.json` hold placeholders.
- Releases are automatic (semantic-release, Conventional Commits); footer version comes from the tag.

## 6. Main business rules

- **Scoring base:** miss=0, column only=1, exact Traditional=3 / Medium=5 / Uncommon=7 / Extra=10;
  `final = base × multiplier` (miss stays 0). World Cup adds phase multipliers + champion classics.
- **Absences:** 1st–2nd = 0; 3rd–4th = −20; 5th = elimination (admin can reactivate).
- **Flávio Rule:** England from round 16 / World Cup from the quarter-finals; leader gets a special
  deadline (24h, or 12h if published <24h before kickoff); late ⇒ half points (rounded down).
- **Prediction visibility:** participant sees the mirror iff the season flag = true **and** round
  `Locked`/`Scored`; admins always (post-lock). **Submission:** participants submit iff the season flag
  = true; else admin-only (403; backend never writes `Participant` in admin-only mode).

## 7. Commands

```bash
# DB (Docker)
cp .env.example .env && docker compose up -d            # PostgreSQL on localhost:5432

# Backend
cd backend
dotnet ef database update --project src/Palpitao.Api     # apply migrations
dotnet run   --project src/Palpitao.Api                  # https://localhost:7099 (Health: /api/health, /api/health/db)
dotnet build Palpitao.slnx -c Release
dotnet test  Palpitao.slnx -c Release                    # 332 tests

# Frontend
cd frontend
npm install
npm start                                                # http://localhost:4200
npm run build
npm run format:check
npm test -- --watch=false                                # Vitest (35)
npm run e2e                                              # Playwright (30)

# Release (automatic): merge Conventional Commits to main → semantic-release tags + deploys.
```

## 8. Recommended next steps

1. **Unblock & ship the fix:** run `dotnet ef database update` on staging/prod; merge **PR #8**;
   after it deploys, confirm `/api/health/db` is `ok` and the footer shows the new version.
2. **Rotate** the 3 secrets (api-sports key, Postgres password, Sentry DSN) and revert the local
   `appsettings*.json` to placeholders.
3. Validate the real OneFootball results provider and enable it; then notifications / refresh token.
