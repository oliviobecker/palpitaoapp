# ADR 0008: Frontend feature folders with lint-enforced layer boundaries

- **Status:** Accepted (October 2026)
- **Area:** frontend

## Context

The SPA had the usual `core/`, `shared/`, `layout/` and `features/` folders, but nothing held them
apart. The admin area had grown into one flat folder of 44 files; a single `AdminService` carried 41
methods for eleven backend controllers; one `models.ts` held 67 interfaces and 13 more were declared
inside services; 545 relative imports, most climbing two or more levels, made a moved file a chore;
and the public standings page was a single 1,130-line component. The backend had just gained
architecture tests that fail the build when a layer reaches past its neighbours.

## Decision

- **Feature folders, sub-features where they grow.** `features/admin/` is split into shell,
  dashboard, seasons, rounds, predictions, ocr, participants, scoring, teams and audit, with its own
  `admin.routes.ts` lazy-loaded under the unchanged `admin` route.
- **Services and models mirror the API.** One HTTP service per backend controller
  (`core/services/admin/`), one `<area>.models.ts` per backend area behind a type-only
  `@core/models` barrel.
- **Path aliases** (`@core/*`, `@shared/*`, `@env/*`, `@version`) instead of climbing imports.
- **Boundaries in ESLint**, with the built-in `no-restricted-imports` and no extra dependency: no
  import climbs more than one level; `core/` imports neither `shared/`, features nor the layout;
  `shared/` imports no feature; a feature never imports a sibling feature; models come through the
  barrel.
- **Components stay small and presentational where they can:** pure logic moves to tested
  `*.util.ts` files, long templates to `.html`, a page with several views keeps its state and hands
  the rendering to presentational children (`public-standings` and its three views).

## Consequences

- A misplaced import fails `npm run lint` — and CI — instead of waiting for review, the same way the
  backend's architecture tests guard its layers.
- The admin HTTP contract is pinned by a table-driven spec over every call, which is what made it
  safe to split the service: the e2e mock answers unknown URLs with an empty 200, so e2e alone would
  not have caught a wrong URL.
- Refactors were proven behaviour-neutral by the unit and e2e suites plus, for the public page,
  byte-compared screenshots of fourteen page states.
- Deliberately left for later, because they change behaviour or looks: unifying the theme toggles
  and the shell/landing language switchers, `canMatch` for the admin guard, feature-level
  providers, `httpResource`.
