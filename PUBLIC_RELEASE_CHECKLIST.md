# Public Release Checklist · Palpitão / FanPicks

Status of the items reviewed while preparing this repository to go public.
`[x]` = done in the working tree · `[ ]` = still needs a manual action by the owner.

## Secrets (current files)
- [x] No real `.env` committed (`.gitignore` ignores `.env` / `.env.*`, keeps `.env.example`)
- [x] `appsettings.json` connection string replaced with a local placeholder
- [x] `appsettings.Development.json` connection string replaced with a local placeholder
- [x] Sentry DSN removed from both `appsettings*.json` (now empty; read from env in prod)
- [x] External provider API key removed from `appsettings.json` (`Fixtures:ApiKey` now empty)
- [x] JWT key in `appsettings.json` is an obvious dev placeholder (real key comes from env/CI secret)
- [x] CI/CD workflows use GitHub Secrets, no hardcoded secrets

## Secrets (git history) — ACTION REQUIRED
- [ ] **Real secrets exist in git history** (see "History exposure" below) — scrub before/around going public
- [ ] **Rotate** the exposed external-provider API key
- [ ] **Rotate** the database password (and restrict the DB host if it was reachable)
- [ ] **Rotate / invalidate** the Sentry DSN if you don't want the old one accepting events
- [ ] Decide on history rewrite (`git filter-repo` / BFG) — requires owner confirmation, not done automatically

## Privacy
- [x] No real user data, emails, phones, or WhatsApp screenshots committed
- [x] Seed admin is a clearly-labeled local dev account (`admin@palpitao.local` / `Admin@123`)
- [x] No OCR sample images or `*.traineddata` committed (`.gitignore` excludes them)
- [x] No private uploads / local databases committed

## Build & tests
- [ ] Backend builds (`dotnet build backend/Palpitao.slnx -c Release`)
- [ ] Backend tests pass (`dotnet test backend/Palpitao.slnx -c Release`)
- [ ] Frontend builds (`cd frontend && npm run build`)
- [ ] Frontend unit + e2e tests pass (`npm test -- --watch=false`, `npm run e2e`)

## Documentation
- [x] README documents product name (FanPicks / Palpitão) and that "England 2025/2026" is only a group/season example
- [x] `.env.example` present at root, `backend/`, `frontend/` with placeholders only
- [x] `docker-compose.yml` added (referenced by README, was missing)
- [x] Tesseract setup documented (download `*.traineddata` into `backend/tessdata/`)
- [x] Sentry setup documented (env vars, DSN empty by default)
- [x] External football provider setup documented

## GitHub
- [x] License chosen — Apache 2.0 (`LICENSE` added)
- [ ] Repository description / topics set
- [ ] Required GitHub Secrets configured for the deploy workflow
      (`BACKEND_CONNECTION_STRING`, `JWT_ISSUER`, `JWT_AUDIENCE`, `JWT_KEY`)

---

## History exposure (details)
The following real values were committed and remain in git history. Removing them
from the current files (done) does **not** remove them from past commits.

- External provider API key — introduced around commit `393f055`
- Sentry DSN — introduced around commit `a3d40e3`
- DB connection string with a private host IP + weak password — since the initial commit

**Recommended remediation:** rotate all three secrets first (so the leaked values are
useless), then optionally rewrite history with `git filter-repo` or BFG Repo-Cleaner and
force-push. Do not rewrite history without confirming the impact on any existing clones.
