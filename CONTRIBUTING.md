# Contributing

Thanks for taking a look. The [development guide](docs/development.md) gets the app running; this
page is about how changes land.

## Workflow

1. Branch from `main` (trunk-based; no long-lived branches).
2. Keep the change focused and covered by tests.
3. Open a pull request whose **title is a [Conventional Commit](https://www.conventionalcommits.org/)**
   — it becomes the squash commit, and semantic-release reads it:
   `feat` → minor release, `fix` → patch release, `BREAKING CHANGE:` → major;
   `build`, `chore`, `ci`, `docs`, `refactor`, `test` ship nothing.
4. CI must be green. Merging deploys staging; a release deploys production after review.

## Before you push

```bash
dotnet build backend/Palpitao.slnx -c Release
dotnet test backend/Palpitao.slnx -c Release

cd frontend
npm run format:check
npm run lint
npm run build
npm test -- --watch=false
npm run e2e
```

`en-US.json` and `pt-BR.json` must have identical key sets — every new string goes in both:

```bash
node -e "const f=o=>Object.entries(o).flatMap(([k,v])=>v&&typeof v==='object'?f(v).map(s=>k+'.'+s):[k]);const en=require('./frontend/public/i18n/en-US.json'),pt=require('./frontend/public/i18n/pt-BR.json');const a=new Set(f(en)),b=new Set(f(pt));console.log('onlyEn',[...a].filter(k=>!b.has(k)),'onlyPt',[...b].filter(k=>!a.has(k)));"
```

## Conventions worth knowing

- **Tenancy.** Never trust the client's `X-Group-Id`; go through `CurrentGroupService`, and scope
  explicitly on paths with no HTTP request (background jobs) — see
  [docs/architecture.md](docs/architecture.md#multi-tenancy-defence-in-depth).
- **Errors.** Throw the domain exceptions with a message key from `DomainMessages` (Portuguese and
  English); never format user text in a service.
- **Schema.** Every model change ships with its EF Core migration; CI fails on pending model changes.
- **Frontend.** Standalone components, signals and `OnPush`; icons through `<app-icon>` (registered
  in `app.config.ts`, never emoji); the shared loading → error → empty → content components; heavy
  CSS in `styles.scss` (component styles have a 4 kB budget).
- **Data.** No real people's names, phone numbers, screenshots or secrets in code, tests or docs —
  fixtures use fictional data.
