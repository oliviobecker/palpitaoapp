# Architecture decision records

Short records of decisions that shaped the code, in the
[Context → Decision → Consequences](https://cognitect.com/blog/2011/11/15/documenting-architecture-decisions)
format. The first five were recorded retroactively, after the code had lived with them in
production; new decisions get a record when they are made.

| # | Decision | Status |
|---|---|---|
| [0001](0001-multi-tenancy-shared-schema.md) | Multi-tenancy in a shared schema, defended in depth | Accepted |
| [0002](0002-anonymous-public-link.md) | The public standings link is a separate, explicitly scoped controller | Accepted |
| [0003](0003-tournament-type-strategy.md) | Tournament type is a strategy fixed when the season is created | Accepted |
| [0004](0004-localized-errors-via-message-keys.md) | Errors carry stable message keys, localized per request | Accepted |
| [0005](0005-ocr-always-reviewed.md) | OCR imports are always reviewed before anything is saved | Accepted |
| [0006](0006-clean-architecture-ef-core.md) | Clean Architecture, with EF Core as the Application's data access | Accepted |
| [0007](0007-problem-details-with-message.md) | Errors are RFC 7807 problem details that keep a `message` | Accepted |
| [0008](0008-frontend-feature-folders-lint-boundaries.md) | Frontend feature folders with lint-enforced layer boundaries | Accepted |
