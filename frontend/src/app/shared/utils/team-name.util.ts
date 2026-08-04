/**
 * Short team names for the copy-ready WhatsApp messages. The seeded catalogue uses
 * the clubs' full legal names ("Wolverhampton Wanderers x Brighton & Hove Albion"),
 * which wrap onto two lines on a phone; the group reads the short forms anyway.
 *
 * The round trip is the constraint: participants edit their scores into the copied
 * message, reply on WhatsApp, and the admin screenshots that reply for the OCR
 * import. `OcrTeamMatcher` matches the OCR'd name back against the full `Team.Name`
 * with two-way substring containment, so most short names below resolve for free
 * (they are prefixes of the full name). The ones that are not — "Wolves", "QPR",
 * "Sheffield Utd", "MK Dons" — are rows in `Common/FootballReference.NameAliases`,
 * the same map the fixture import uses for external spellings;
 * `team-name.util.spec.ts` fails if that set changes without the backend following.
 *
 * Deliberately an explicit table rather than a suffix-stripping rule: the rule would
 * collapse "Manchester City" and "Manchester United" into the same token, and it
 * would silently mangle teams created on the fly by the fixture import (national
 * teams arrive in Portuguese — "Bélgica", "Nova Zelândia", "RD Congo"). Anything not
 * listed here is emitted verbatim, which is what keeps those safe.
 */
// Sorted by full name, not by division: clubs are promoted and relegated every season
// (the catalogue refresh moves them around), while the names stay put.
export const TEAM_SHORT_NAMES: Readonly<Record<string, string>> = {
  'AFC Wimbledon': 'Wimbledon',
  'Birmingham City': 'Birmingham',
  'Blackburn Rovers': 'Blackburn',
  'Bolton Wanderers': 'Bolton',
  'Bradford City': 'Bradford',
  'Brighton & Hove Albion': 'Brighton',
  'Burton Albion': 'Burton',
  'Cambridge United': 'Cambridge',
  'Cardiff City': 'Cardiff',
  'Charlton Athletic': 'Charlton',
  'Coventry City': 'Coventry',
  'Derby County': 'Derby',
  'Doncaster Rovers': 'Doncaster',
  'Huddersfield Town': 'Huddersfield',
  'Hull City': 'Hull',
  'Ipswich Town': 'Ipswich',
  'Leeds United': 'Leeds',
  'Leicester City': 'Leicester',
  'Lincoln City': 'Lincoln',
  // Not a seeded club: the fixture feed spells Liverpool this way and the import creates
  // the row verbatim, so the message would print the suffix the group asked us to drop.
  // `FootballReference.NameAliases` stops new rows appearing; this keeps the existing one
  // reading right. (Hence no row in OcrShortNameRoundTripTests, which seeds from the
  // catalogue.)
  'Liverpool FC': 'Liverpool',
  'Luton Town': 'Luton',
  'Manchester City': 'Man City',
  'Manchester United': 'Man Utd',
  'Mansfield Town': 'Mansfield',
  'Milton Keynes Dons': 'MK Dons',
  'Norwich City': 'Norwich',
  'Nottingham Forest': 'Nottingham',
  'Oxford United': 'Oxford',
  'Peterborough United': 'Peterborough',
  'Plymouth Argyle': 'Plymouth',
  'Preston North End': 'Preston',
  'Queens Park Rangers': 'QPR',
  'Sheffield United': 'Sheffield Utd',
  'Sheffield Wednesday': 'Sheffield Weds',
  'Stockport County': 'Stockport',
  'Stoke City': 'Stoke',
  'Swansea City': 'Swansea',
  'West Bromwich Albion': 'West Brom',
  'West Ham United': 'West Ham',
  'Wigan Athletic': 'Wigan',
  'Wolverhampton Wanderers': 'Wolves',
  'Wycombe Wanderers': 'Wycombe',
  // Left alone on purpose: Notts County (bare "Notts" is the other Nottingham club, and
  // now that Forest prints as "Nottingham" the two must stay clearly apart) and Bristol
  // City (Bristol alone is ambiguous while Bristol Rovers exists in the pyramid).
};

const normalize = (name: string) => name.trim().replace(/\s+/g, ' ').toLowerCase();

const BY_NORMALIZED = new Map(
  Object.entries(TEAM_SHORT_NAMES).map(([full, short]) => [normalize(full), short]),
);

/**
 * Short form of a team name for the generated messages. Names outside the table —
 * national teams, clubs auto-created by the fixture import — pass through verbatim.
 *
 * Display only: the multiplier rules (`isBigSeven`, `isClassic` in `match.util.ts`)
 * key off the exact full name, so never write the result back onto a match.
 */
export function shortTeamName(fullName: string): string {
  return BY_NORMALIZED.get(normalize(fullName)) ?? fullName;
}
