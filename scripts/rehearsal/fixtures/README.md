# Fixture snapshots — English 2025/26

Frozen copies of the public fixturedownload.com JSON feeds, used by
[`seed-demo-season.cs`](../seed-demo-season.cs) to build the rehearsal season.

| File | Source | Matches | With final score |
|---|---|---|---|
| `epl-2025.json` | <https://fixturedownload.com/feed/json/epl-2025> | 380 (rounds 1–38) | 380 |
| `championship-2025.json` | <https://fixturedownload.com/feed/json/championship-2025> | 552 (rounds 1–46) | 552 |

SHA-256, downloaded 2026-07-25:

```
76cb34e95f54738e5c6fae87841af381dceb2c8c3fe3a9be3e95374d3db20786  epl-2025.json
49e3780dfc0b3c38c82a5f3ae6ce43e5ff9238df2a87c7a56592564599bccddb  championship-2025.json
```

## Why these are committed

The upstream feed is mutable — a corrected scoreline months from now would silently
change what "the same seed" produces. Freezing them keeps the seeder reproducible:
same snapshot + same `SEED_MASTER_SEED` → byte-identical rows.

The seeder reads this directory by default. To refresh (and re-pin the hashes above):

```bash
curl -A PalpitaoSeeder/1.0 -o scripts/rehearsal/fixtures/epl-2025.json https://fixturedownload.com/feed/json/epl-2025
```

A plain `User-Agent` gets a 403 from fixturedownload.com — send one.

## Shape

```json
{"MatchNumber":1,"RoundNumber":1,"DateUtc":"2025-08-15 19:00:00Z","Location":"Anfield",
 "HomeTeam":"Liverpool","AwayTeam":"Bournemouth","Group":null,
 "HomeTeamScore":4,"AwayTeamScore":2,"Winner":"Liverpool"}
```

`DateUtc` is parsed with `AdjustToUniversal | AssumeUniversal`, matching
[`FixtureDownloadFixtureProvider.TryParseDate`](../../../backend/src/Palpitao.Api/Services/Fixtures/FixtureDownloadFixtureProvider.cs).

## Team names

The 24 Championship names match the seeded `Teams` catalogue verbatim. The Premier
League feed uses eight short forms that the seeder maps via an alias table
(`Spurs`, `Man Utd`, `Man City`, `Wolves`, `Nott'm Forest`, `Brighton`, `Leeds`,
`West Ham`). Any name that fails to resolve aborts the seed — the seeder never
auto-creates a `Teams` row, because that would silently split the catalogue in two.
