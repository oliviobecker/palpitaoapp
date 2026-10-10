# Roadmap

Known next steps, roughly in order. Product features are tracked with the group that uses the app;
this list is about the codebase.

## Architecture and code quality

- **Layered backend.** Split `Palpitao.Api` into Domain / Application / Infrastructure / Api projects
  with architecture tests enforcing the dependency rule; slim the composition root in `Program.cs`.
- **HTTP-level tests.** `WebApplicationFactory` tests for the pipeline itself: authentication and
  refresh rotation, tenant isolation over real requests, the anonymous public link, the error
  contract and rate limiting.
- **Problem Details.** Move the error body to RFC 7807 `application/problem+json`, keeping the
  `message` field the SPA reads.
- **Frontend structure.** Group the admin screens into sub-features with their own routes, add path
  aliases with lint-enforced layer boundaries, split the shared models and the admin API service
  per resource.

## Testing gaps

- **PostgreSQL-specific behaviour.** The suite runs on SQLite, which cannot exercise the advisory locks
  of the background jobs or serializable-isolation conflicts. A small Testcontainers suite would.
- **Time.** Services read `DateTime.UtcNow` directly; injecting `TimeProvider` would make deadline
  and window logic testable without date juggling.

## Developer experience

- **One-command local stack.** Dockerfiles and a compose profile for the API (with Tesseract's Linux
  native libraries) and the SPA.
- **Generated API client.** Generate the frontend's API types from the OpenAPI document, with a CI
  check that fails when they drift.
