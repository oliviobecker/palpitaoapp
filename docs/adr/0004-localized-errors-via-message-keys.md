# ADR 0004: Errors carry stable message keys, localized per request

- **Status:** Accepted (recorded retroactively, October 2026)
- **Area:** backend, frontend, i18n

## Context

The app is bilingual (Portuguese and English) and switches language at runtime. Most user-facing
errors are business rules raised deep in services ("predictions are closed", "this round still has
matches without a result"). Formatting a sentence in the service would tie domain code to the
request's language; returning a bare code would push every message into the SPA, including the
ones that only the server can phrase.

## Decision

- Services throw a small set of exceptions — `ValidationException` (400), `ForbiddenException` (403),
  `NotFoundException` (404), `BusinessRuleException` (422) — each carrying a **stable key** such as
  `round.weekPartsOpen`.
- `DomainMessages` is the single catalogue of keys with their Portuguese and English text. The
  Portuguese text doubles as `Exception.Message`, so logs stay readable.
- One middleware maps the exception to its status and resolves the key in the caller's language
  (`Accept-Language`, sent by the SPA on every call), returning `{ status, message, traceId }`.
  Validation (FluentValidation) and the rate limiter answer in the same shape.

## Consequences

- Domain code never formats user text, and the SPA shows `message` as received.
- Every error carries a `traceId`, so a user's screenshot can be matched to the log and the Sentry
  event.
- Keys are strings: a typo surfaces as the key itself rather than at compile time. Tests assert keys,
  and the catalogue is the place to look them up.
