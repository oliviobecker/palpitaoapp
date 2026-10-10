# FanPicks / Palpitão — web app

The Angular single-page app of [FanPicks / Palpitão](../README.md): participants send predictions,
follow rounds and standings; group admins run seasons, rounds, results, absences and OCR imports.
Mobile-first, Portuguese and English at runtime, light and dark themes.

## Stack

| | |
|---|---|
| Framework | Angular 21 — standalone components, signals, `OnPush` everywhere, **zoneless** |
| Language | TypeScript 5.9 (strict, strict templates) |
| UI | Bootstrap 5, Lucide icons (through `<app-icon>`), CSS-variable design tokens |
| i18n | ngx-translate, switched at runtime (`public/i18n/pt-BR.json`, `en-US.json`) |
| Tests | Vitest through the Angular unit-test builder; Playwright end to end |
| Quality | ESLint (angular-eslint + layer rules), Prettier |

## Scripts

```bash
npm ci                     # install exactly what package-lock.json pins
npm start                  # dev server on http://localhost:4200 (API: https://localhost:7099)
npm run build              # production build, with the bundle budgets enforced
npm run lint               # ESLint — must report 0 errors
npm test -- --watch=false  # unit tests, once
npm run e2e                # Playwright: starts the dev server and mocks the API
npm run format:check       # Prettier, as CI runs it (npm run format to fix)
```

`npm start`/`build`/`test` first stamp the version into `src/version.ts` (generated, git-ignored).

## Layout

```
src/app/
  core/           app-wide singletons, no UI
    auth/           session, token storage, route guards
    interceptors/   JWT + one refresh-and-retry on 401, X-Group-Id, Accept-Language, error toasts, loading bar
    models/         the API's shapes, one <area>.models.ts per backend area, behind the @core/models barrel
    services/       one HTTP service per API area; admin/ holds one per backend admin controller
    i18n/, theme/, notifications/, guards/
  shared/         reusable building blocks
    components/     page header, empty/error/skeleton states, badges, podium, language switcher, …
    utils/          pure functions (message builders, deadlines, names, tournament rules), each with a spec
    validators/, pipes/
  layout/         the responsive shell: desktop top bar, mobile bottom navigation
  features/       one folder per area, lazy-loaded
    admin/          shell, dashboard, seasons, rounds, predictions, ocr, participants, scoring, teams, audit
    auth/, dashboard/, rounds/, standings/, groups/, landing/, public/
```

### Layer rules (enforced by ESLint)

- Imports never climb more than one level: use the aliases `@core/*`, `@shared/*`, `@env/*`,
  `@version`.
- `core/` imports neither `@shared` nor features or the layout; `shared/` never imports features or
  the layout.
- A feature's own files never import a sibling feature — what two features need belongs in `@shared`
  or `@core`.
- API shapes come from the `@core/models` barrel (enums, which are values, from `@core/models/enums`).

## Conventions

- **Components:** standalone, `ChangeDetectionStrategy.OnPush`, state in signals, `input()` /
  `output()`. A template or a style block that grows past ~100 / ~50 lines moves to its own file.
- **Pure logic out of components:** anything testable without a DOM lives in a `*.util.ts` beside
  the component (or in `shared/utils`) and gets a spec.
- **Screens:** `loading → error → empty → content`, with the shared `app-skeleton-list`,
  `app-error-state` (`(retry)`), `app-empty-state` and `app-page-header`.
- **Icons:** `<app-icon name="…">`, registered in `provideLucideIcons(...)` in `app.config.ts`. Emoji
  only for the ⚽ logo and inside the WhatsApp messages the app generates.
- **Styles:** component styles stay small (the `anyComponentStyle` budget is 4 kB); shared and
  heavy CSS, dark-mode tokens and keyframes live in `src/styles.scss`.
- **i18n:** every string in both `pt-BR.json` and `en-US.json` — the key sets must stay identical.
- **HTTP flags:** per-request `HttpContextToken`s opt a call out of the error toast
  (`SKIP_ERROR_TOAST`), the refresh-and-retry (`SKIP_AUTH_REFRESH`) or the session and group headers
  (`SKIP_TENANT_HEADERS`, used by the public standings link).

## Testing

- **Unit (Vitest):** utils, guards, interceptors, services and key components.
  `core/services/admin-http-contract.spec.ts` pins every admin call — verb, URL, body, flags — so a
  refactor of the services cannot change a request unnoticed.
- **End to end (Playwright):** real flows in Chromium against the dev server, with every API call
  answered by `e2e/support.ts` (`installApi` + per-test handlers; `seedAuth` signs a user in). An
  unmatched call gets an empty 200 so a screen never hangs — which is also why the contract spec
  above exists.

## Windows note

The repository stores text files with LF endings (`.gitattributes`). A checkout made before that
rule may still have CRLF files, which make `npm run format:check` fail locally on files Prettier did
not change; check a file with `npx prettier --check <file> --end-of-line auto`, or re-clone.
