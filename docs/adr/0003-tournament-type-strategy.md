# ADR 0003: Tournament type is a strategy fixed when the season is created

- **Status:** Accepted (recorded retroactively, October 2026)
- **Area:** backend, domain

## Context

The original pool predicted English football across four competitions, with multipliers for
classics, knockout phases and League One, and a "Flávio Rule" that penalises a late leader from a
given round. A second group wanted the same game for the FIFA World Cup: national teams, a single
competition with phases, classics between former world champions, and the leader penalty only from
the quarter-finals. Encoding both with `if` statements on competition names would have spread the
difference across scoring, validation, fixture import and the UI.

## Decision

Each season carries a `TournamentType` (`PalpitaoEngland` or `FifaWorldCup`), chosen at creation and
**immutable afterwards**. The type is the single switch for:

- the competitions and phases a round may contain (`TournamentRules`);
- the multiplier table and the classic definition (`Services/Scoring`);
- which Flávio Rule variant applies — prior-round leaders from a configurable round (England), or the
  leader captured at publication once a quarter-final is in the round (World Cup).

New tournament behaviour branches on the season's type, not on team or competition names.

## Consequences

- A third tournament type is an extension, not a rewrite: add the enum value and its rules.
- Fixing the type at creation keeps scored history consistent — a season can never be rescored
  under another rulebook.
- The frontend mirrors a few rules (allowed competitions, World Cup Flávio phases) for display; the
  backend stays the source of truth.
