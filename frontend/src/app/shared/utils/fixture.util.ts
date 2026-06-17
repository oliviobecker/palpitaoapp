import { FixtureCandidate } from '../../core/models/models';
import { ImportFixtureItem } from '../../core/services/admin.service';

/** Local date (yyyy-MM-dd) N days from today — for default fixture-search windows. */
export function isoDateFromToday(days: number): string {
  const d = new Date();
  d.setDate(d.getDate() + days);
  const month = `${d.getMonth() + 1}`.padStart(2, '0');
  const day = `${d.getDate()}`.padStart(2, '0');
  return `${d.getFullYear()}-${month}-${day}`;
}

/** Maps a searched fixture candidate to the import payload item. */
export function toImportItem(f: FixtureCandidate): ImportFixtureItem {
  return {
    externalId: f.externalId,
    competition: f.competition,
    phase: f.phase,
    homeTeamName: f.homeTeamName,
    awayTeamName: f.awayTeamName,
    startsAt: f.startsAt,
    source: f.source,
  };
}
