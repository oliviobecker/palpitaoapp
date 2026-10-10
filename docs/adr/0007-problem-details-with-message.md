# ADR 0007: Errors are RFC 7807 problem details that keep a `message`

- **Status:** Accepted (October 2026)
- **Area:** backend API, frontend contract

## Context

Since [ADR 0004](0004-localized-errors-via-message-keys.md) every error has been a small JSON body,
`{ status, message, traceId }`, with `message` localized from a stable key. It worked, but it was a
private format: clients and tools that understand HTTP APIs expect
[RFC 7807 / RFC 9457](https://www.rfc-editor.org/rfc/rfc9457) problem details
(`application/problem+json`), ASP.NET Core produces them for its own failures, and the OpenAPI
document had no schema to point the error responses at. The SPA, its e2e mocks and the ocr upload
queue all read `body.message`.

Two ways to produce them in ASP.NET Core: an `IExceptionHandler` behind `UseExceptionHandler`, or the
existing exception middleware writing through `IProblemDetailsService`.

## Decision

Every error response is a problem: `type`, `title`, `status`, and `detail` — the message localized
by `Accept-Language`. Two members are added to all of them through `CustomizeProblemDetails`:
**`message`**, the same text as `detail`, so the SPA keeps working unchanged; and **`traceId`**, the
request's identifier. The exception middleware, the rate limiter's 429 and the login/refresh
responses all write through the framework's problem-details service.

The exceptions stay in a **middleware**, rewritten as one mapping table, rather than moving to
`IExceptionHandler`: exceptions handled by `UseExceptionHandler` surface on
`IExceptionHandlerFeature`, where Sentry's ASP.NET Core middleware captures them — every expected
404 or 422 would have become a Sentry event. Caught in the middleware, only the unexpected ones
reach Sentry, through their Error log line. An integration test pins that no expected failure logs
at Error.

## Consequences

- Standard tooling (the OpenAPI document, Scalar, any HTTP client) understands the errors; the
  document declares the problem responses once, through an MVC convention.
- The SPA, the e2e mocks and the OCR upload queue needed no change; `message` can be dropped in a
  later, coordinated change if the SPA moves to `detail`.
- A request the client abandoned is answered 499 and logged at Debug instead of being reported as a
  500.
- `traceId` keeps the request's `TraceIdentifier` (not the W3C activity id the framework would use),
  the same value the logs carry.
