/**
 * Short team names for the copy-ready WhatsApp messages. The seeded catalogue uses
 * the clubs' full legal names ("Wolverhampton Wanderers x Brighton & Hove Albion"),
 * which wrap onto two lines on a phone; the group reads the short forms anyway.
 *
 * The round trip is the constraint: participants edit their scores into the copied
 * message, reply on WhatsApp, and the admin screenshots that reply for the OCR
 * import. `OcrTeamMatcher` matches the OCR'd name back against the full `Team.Name`
 * with an alias table plus two-way substring containment, so most short names below
 * resolve for free (they are prefixes of the full name). The three that are not —
 * "Wolves", "QPR", "Sheffield Utd" — have a matching row in
 * `Services/Ocr/OcrTeamMatcher.TeamAliases`; `team-name.util.spec.ts` fails if that
 * set ever changes without the backend following.
 *
 * Deliberately an explicit table rather than a suffix-stripping rule: the rule would
 * collapse "Manchester City" and "Manchester United" into the same token, and it
 * would silently mangle teams created on the fly by the fixture import (national
 * teams arrive in Portuguese — "Bélgica", "Nova Zelândia", "RD Congo"). Anything not
 * listed here is emitted verbatim, which is what keeps those safe.
 */
export const TEAM_SHORT_NAMES: Readonly<Record<string, string>> = {
  // Premier League
  'Brighton & Hove Albion': 'Brighton',
  'Leeds United': 'Leeds',
  'Manchester City': 'Man City',
  'Manchester United': 'Man Utd',
  'West Ham United': 'West Ham',
  'Wolverhampton Wanderers': 'Wolves',
  // Championship
  'Birmingham City': 'Birmingham',
  'Blackburn Rovers': 'Blackburn',
  'Charlton Athletic': 'Charlton',
  'Coventry City': 'Coventry',
  'Derby County': 'Derby',
  'Hull City': 'Hull',
  'Ipswich Town': 'Ipswich',
  'Leicester City': 'Leicester',
  'Norwich City': 'Norwich',
  'Oxford United': 'Oxford',
  'Preston North End': 'Preston',
  'Queens Park Rangers': 'QPR',
  'Sheffield United': 'Sheffield Utd',
  'Sheffield Wednesday': 'Sheffield Wed',
  'Stoke City': 'Stoke',
  'Swansea City': 'Swansea',
  'West Bromwich Albion': 'West Brom',
  // League One
  'AFC Wimbledon': 'Wimbledon',
  'Bolton Wanderers': 'Bolton',
  'Bradford City': 'Bradford',
  'Burton Albion': 'Burton',
  'Cardiff City': 'Cardiff',
  'Doncaster Rovers': 'Doncaster',
  'Exeter City': 'Exeter',
  'Huddersfield Town': 'Huddersfield',
  'Lincoln City': 'Lincoln',
  'Luton Town': 'Luton',
  'Mansfield Town': 'Mansfield',
  'Northampton Town': 'Northampton',
  'Peterborough United': 'Peterborough',
  'Plymouth Argyle': 'Plymouth',
  'Rotherham United': 'Rotherham',
  'Stockport County': 'Stockport',
  'Wigan Athletic': 'Wigan',
  'Wycombe Wanderers': 'Wycombe',
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
