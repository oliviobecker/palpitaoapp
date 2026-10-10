# Development guide

Everything needed to run, change and test the app locally. For the big picture start with
[architecture.md](architecture.md).

## Product name and branding

The application is a football prediction platform.

- **English product name:** FanPicks
- **Portuguese product name:** Palpitão

Groups and tournaments can have custom names, such as:

- Palpitão England · Palpitão World Cup · Palpitão Brasileirão
- England Predictions · World Cup 2026 · Friends League · custom group names

Names like **"England 2025/2026"** are examples of **groups or seasons**, not the application
name. The product name is shown in the active language (FanPicks in `en-US`, Palpitão in
`pt-BR`) via the `app.name` translation key; the current group name is shown separately in the
header. The seeded default group is named _Palpitão England 2025/2026_ — that is a group/season
name, not the app's name.

## Stack and UI conventions

| Layer | Technology |
|---|---|
| Backend | C# / .NET 10, ASP.NET Core Web API (controllers) |
| ORM / Database | EF Core 10 (code-first) + PostgreSQL 16 |
| Auth | JWT Bearer (access + rotating refresh tokens) + BCrypt |
| Backend tests | xUnit + SQLite in-memory |
| Frontend | Angular 21 (standalone, signals), TypeScript |
| UI | Bootstrap 5 (mobile-first), Lucide icons (`@lucide/angular`), light/dark theme (`data-bs-theme`) |
| Frontend tests | Vitest (Angular 21 default runner) + Playwright e2e against a mocked API |

> **Why Bootstrap** (instead of Material/Tailwind): mobile-first by default, already integrated,
> ready-made components (navbar, cards, toasts, modals) and a lean bundle for a simple UI.

**UX conventions:**

- **Light/dark theme** — `ThemeService` applies Bootstrap's `data-bs-theme` (follows the OS until the
  user toggles it in the navbar); custom CSS-variable tokens flip alongside. An inline script in
  `index.html` sets the theme before first paint to avoid a flash.
- **Icons** — a single `<app-icon name="…">` wrapper over Lucide (icons registered in `app.config.ts`).
  Emoji are reserved for the brand logo and WhatsApp message content.
- **Loading/empty/error** — shared `app-skeleton`/`app-skeleton-list`, `app-empty-state` and
  `app-error-state` (with retry); a shared `app-page-header` unifies screen headers. Animations honour
  `prefers-reduced-motion`.
- **Predictions draft** — in-progress scores persist to `localStorage` per round and restore on return
  (cleared on save); a status bar shows remaining/all-filled and an unsaved indicator.

## Prerequisites

- [.NET SDK 10](https://dotnet.microsoft.com/) (10.0.100 or later — `global.json` rolls forward to the newest
  10.0 feature band installed) · [Node.js 22](https://nodejs.org/) and npm
- [Docker](https://www.docker.com/) (for PostgreSQL) **or** a local PostgreSQL
- EF Core CLI: a local tool pinned in `.config/dotnet-tools.json` — run `dotnet tool restore` once

## Start PostgreSQL

```bash
cp .env.example .env        # adjust user/password/port if you want
docker compose up -d        # PostgreSQL on localhost:5432
```

The default connection string (`backend/src/Palpitao.Api/appsettings.json`) already points to
`Host=localhost;Port=5432;Database=palpitao;Username=palpitao;Password=palpitao`.

## Apply migrations

```bash
dotnet tool restore
cd backend
dotnet ef database update --project src/Palpitao.Infrastructure --startup-project src/Palpitao.Api
```

> `dotnet ef database update` applies to whatever `ConnectionStrings:DefaultConnection` resolves to —
> `appsettings.Development.json`, then user-secrets, then environment variables. Check it points at
> the database you mean before running it.

This creates all tables and the **initial seed**: the club catalogue (Premier League, Championship
and League One — currently the **2026/2027** rosters), the seven national-team **world champions**
(for World Cup certames) and the **default group**.

### The development admin

Sign in as **`admin@palpitao.local` / `Admin@123`** — a platform admin and the default group's
GroupAdmin. **Development only.** The account is not part of the EF model's seed: in Development the
API inserts it (with its membership) at startup into a database that has **no users at all**, so it
can never reappear on a populated database. A database built by the migrations already has it — the
early migrations created it — and outside Development the API logs an **error** at startup (which
reaches Sentry) for as long as that account still has this password.
For new migrations: `dotnet ef migrations add <Name> --project src/Palpitao.Infrastructure --startup-project src/Palpitao.Api`. The migrations live in the Infrastructure project; the Api is
the startup project that supplies the configuration.

## Run the backend

```bash
cd backend
dotnet run --project src/Palpitao.Api
# API at https://localhost:7099 (and http://localhost:5146)
# Health: GET /health, GET /health/db and GET /health/ocr
# OpenAPI (dev): GET /openapi/v1.json
```

## Run the frontend

```bash
cd frontend
npm install
npm start                   # ng serve → http://localhost:4200
```

The development `apiBaseUrl` (`src/environments/environment.development.ts`) points to
`https://localhost:7099`; the backend CORS allows `http://localhost:4200`. Deployed builds call `/api`,
where IIS mounts the API as a sub-application — locally the same routes are served from the root.

## Environment variables

**Backend** (`backend/.env.example`) — override `appsettings*.json`:

```
ASPNETCORE_ENVIRONMENT=Development
ConnectionStrings__DefaultConnection=Host=localhost;Port=5432;Database=palpitao;Username=palpitao;Password=palpitao
Jwt__Issuer=palpitao
Jwt__Audience=palpitao
Jwt__Key=<long random secret, >= 32 bytes>
```

**Root** (`.env.example`) — used by docker-compose: `POSTGRES_USER/PASSWORD/DB/PORT`.

**Frontend** (`frontend/.env.example`) — reference only; the effective value lives in
`src/environments/*.ts`.

## Languages (Portuguese / English)

- **Frontend**: [ngx-translate](https://github.com/ngx-translate/core) (language switching at
  **runtime**, no rebuild — that's why it's preferred over native Angular i18n). Detects
  `navigator.language` (`pt*` → `pt-BR`, otherwise `en-US`), persists it in `localStorage`, and
  there is a **PT/EN** selector in the top bar. Translations in
  [public/i18n/pt-BR.json](../frontend/public/i18n/pt-BR.json) and
  [en-US.json](../frontend/public/i18n/en-US.json).
- **Backend**: `LocalizationService` resolves the language by the **`Accept-Language`** header
  (`pt*` → Portuguese, otherwise English) and centralizes messages. Angular sends `Accept-Language`
  on every call (interceptor).

> **Note:** the i18n infrastructure is complete (detection, switching, interceptor, key messages
> and new translated screens). Extracting **all** strings from the legacy screens into the
> translation files is incremental work still in progress.

## Running the tests

```bash
# Backend — unit tests on SQLite in-memory (1,061) and architecture tests (19)
dotnet test backend/Palpitao.slnx
dotnet test backend/Palpitao.slnx --filter "FullyQualifiedName~ScoringServiceTests"   # one class

# Backend — the schema must match the migrations (no database needed; CI runs it too)
dotnet ef migrations has-pending-model-changes --project backend/src/Palpitao.Infrastructure --startup-project backend/src/Palpitao.Api

# Frontend — Vitest unit tests (199)
cd frontend && npm test -- --watch=false

# Frontend — Playwright e2e (93); starts `ng serve` and mocks every API call in e2e/support.ts
cd frontend && npm run e2e

# Lint and formatting gates (CI runs the same)
cd frontend && npm run lint && npm run format:check
```

- No test touches the network or a real database: providers are exercised through a stubbed
  `HttpMessageHandler`, persistence through SQLite in-memory.
- `OcrSamplesTests` runs real screenshots through the real Tesseract engine and is skipped unless
  `OCR_SAMPLES_DIR` points at a folder of them — see
  [Entering predictions](features/prediction-import.md#ocr-import-tesseract).
- `en-US.json` and `pt-BR.json` must keep identical key sets; [CONTRIBUTING.md](../CONTRIBUTING.md)
  has the one-line check.

## Tesseract language files

OCR needs `por.traineddata` and `eng.traineddata` in `backend/tessdata/` (gitignored, ~38 MB). See
[backend/tessdata/README.md](../backend/tessdata/README.md) for where to get them; deployed
environments download pinned, checksum-verified copies during the deploy.
