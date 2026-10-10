# ADR 0006: Clean Architecture, with EF Core as the Application's data access

- **Status:** Accepted (October 2026)
- **Area:** backend

## Context

The backend grew as one ASP.NET Core project organised by technical folder (`Controllers/`,
`Services/`, `Entities/`, `Data/`, `DTOs/`). Nothing stopped a service from reading `HttpContext`,
calling BCrypt or `SentrySdk` directly, or opening a transaction its own way — and three services did
each of those. The rules that matter most (scoring, the tournament strategy, the round lifecycle)
lived next to the code that talks to PostgreSQL, OneFootball and Tesseract, so the boundaries
existed only by convention.

## Decision

Split the backend into four projects with one dependency rule — Api → Infrastructure →
Application → Domain:

- **Domain**: entities, enums and the rules that need no I/O. No references.
- **Application**: the use cases, one folder per area, and the ports they need (`Abstractions/`).
- **Infrastructure**: the adapters — EF Core on PostgreSQL, the external providers, JWT and BCrypt,
  Tesseract, the background jobs.
- **Api**: controllers, filters, error handling, the request-bound ports and the composition root.

**EF Core stays the Application's data-access abstraction** instead of a repository layer. The
port, `IAppDbContext`, exposes the `DbSet`s, `Entry` and `SaveChangesAsync`. The Application
references only the EF Core base package: no provider, no `Relational`, and no `Database` on the
port — so connections, raw SQL and transactions live in the Infrastructure (`ITransactionRunner`,
the advisory-locked jobs). The other seams cut in the same pass: `ICurrentUser` instead of
`HttpContext`, `IPasswordHasher` instead of BCrypt, `ILogger` instead of the Sentry SDK.

The split was done as pure moves, each proven behaviour-neutral: the idempotent migration script
stayed byte-identical, the EF model dump (including every query filter and seed row) identical, and
the Release publish output the same files plus the new assemblies.

## Consequences

- Each rule of the domain can be read, and tested, without the web host or the database; the
  Domain project compiles against the base library alone.
- The boundaries are checked, not trusted: `Palpitao.ArchitectureTests` fails the build when a layer
  references one it must not, or a controller takes a `DbContext`.
- Services keep their LINQ: `DbSet<T>` already is a repository with a query language, and generic
  repositories would have hidden the queries without isolating anything. The trade-off is that the
  Application knows EF Core's API (`Include`, `AsNoTracking`, `ToListAsync`) — accepted, since the
  provider and the SQL do not leak in.
- Unit tests keep running the use cases against SQLite through the real `AppDbContext`, so
  constraints and transactions stay exercised.
- Migrations now live in the Infrastructure: every `dotnet ef` call names it with `--project` and the
  Api with `--startup-project`.
