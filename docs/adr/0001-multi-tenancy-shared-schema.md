# ADR 0001: Multi-tenancy in a shared schema, defended in depth

- **Status:** Accepted (recorded retroactively, October 2026)
- **Area:** backend, data model

## Context

The app started as one pool for one group of friends and became a platform where any group runs its
own pool: its own admins, participants, seasons, rounds, predictions and standings. A group's data
must never reach another group — a leak would show one group's predictions or standings to
strangers. The app runs on a single small server with one PostgreSQL database.

Options considered: a database per group, a schema per group, or one shared schema with a
`GroupId` discriminator.

## Decision

One shared schema. `GroupId` lives only on the **tenant roots** (`Season`, `Round`, `Standing`,
`RoundParticipantResult`, `SeasonScoringConfig`, `OcrParticipantAlias`, plus `GroupUser` and
`AuditLog`); per-round entities (`Prediction`, `RoundMatch`, `Absence`, OCR rows…) reach their group
through their parent. Isolation is enforced in layers:

1. **Access chokepoint.** Every authenticated request names its group in `X-Group-Id`;
   `CurrentGroupService` revalidates it against an approved, active `GroupUser` and the
   `[RequireGroupParticipant]` / `[RequireGroupAdmin]` filters refuse anything else.
2. **Read filter.** Roots implement `IGroupOwned` and get an EF Core global query filter scoped to the
   request's group.
3. **Write stamp.** `SaveChanges` stamps the request's group on new roots that left `GroupId` unset.

Layers 2 and 3 read a DB-free `IRequestGroupContext` and are inert outside an HTTP request.

## Consequences

- One database to migrate, back up and operate; cross-group features (a user in several groups, a
  platform super-admin) stay simple.
- A forgotten `WHERE GroupId = …` or a forgotten assignment is caught by the filter and the stamp
  rather than becoming a leak; dedicated tests pin the isolation.
- Background jobs, seeding and tests see every group, because there is no request group. Code on
  those paths must scope explicitly — the price of a filter that cannot misfire outside a request.
- Migrations stay lean: per-round tables carry no `GroupId`.
