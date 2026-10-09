# ADR 0002: The public standings link is a separate, explicitly scoped controller

- **Status:** Accepted (recorded retroactively, October 2026)
- **Area:** backend, security

## Context

Groups wanted to share standings and a per-round scoring audit with people who have no account —
in practice, a link pasted into the group's WhatsApp after each round. Everything else in the API
assumes a signed-in user and a validated `X-Group-Id` ([ADR 0001](0001-multi-tenancy-shared-schema.md)).
Three details make a naive anonymous endpoint wrong:

- The group checks are **action filters**, not authorization filters: adding `[AllowAnonymous]` to an
  action of an existing controller turns off `[Authorize]` but the group filter still answers 403.
- With no request group, the EF global query filter matches **every** group, not none.
- A signed-in browser opening the link may still send a stale `X-Group-Id`, which would filter the
  season away and turn a valid link into a 404.

## Decision

- A dedicated `PublicStandingsController` under `/public/seasons/{key}/…`, anonymous and rate-limited
  per IP. The season's random 12-hex-digit **public key is the credential**.
- The controller is marked `[IgnoreRequestGroup]`, so `RequestGroupContext` reports no group whatever
  headers arrive.
- `PublicStandingsService` resolves the season from the key, derives the tenant from it and scopes
  every query explicitly with `IgnoreQueryFilters()` — it never relies on the global filter.
- Publishing is off by default per season; an unknown, malformed or unpublished key all return the
  same 404. Only closed rounds are exposed, never e-mails or admin justifications. Responses carry
  `X-Robots-Tag: noindex`, and the frontend sends neither token nor group (`SKIP_TENANT_HEADERS`).

## Consequences

- The anonymous surface is small and visible in one place, and a reviewer can check its scoping
  without reasoning about filters.
- Anyone holding the key can read closed rounds' predictions, regardless of the in-app visibility
  setting; the admin screen says so before the toggle, and regenerating the key kills old links.
