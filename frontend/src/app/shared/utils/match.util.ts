import { Competition, MatchPhase } from '../../core/models/enums';
import { RoundMatch } from '../../core/models/models';

/** Big Seven clubs of the season (used to mirror the backend multiplier rules). */
export const BIG_SEVEN = new Set<string>([
  'Arsenal',
  'Chelsea',
  'Liverpool',
  'Manchester City',
  'Manchester United',
  'Newcastle',
  'Tottenham',
]);

export function isBigSeven(teamName: string): boolean {
  return BIG_SEVEN.has(teamName);
}

/** World champion national teams (WorldCupTitles > 0), by name — mirrors the seed. */
export const WORLD_CHAMPIONS = new Set<string>([
  'Brazil',
  'Germany',
  'Argentina',
  'France',
  'Uruguay',
  'Spain',
  'England',
]);

export function isWorldChampion(teamName: string): boolean {
  return WORLD_CHAMPIONS.has(teamName);
}

/** World Cup knockout phases (Round of 32 onward) — where classics double. */
function isWorldCupKnockout(phase: MatchPhase): boolean {
  switch (phase) {
    case MatchPhase.WorldCupRoundOf32:
    case MatchPhase.WorldCupRoundOf16:
    case MatchPhase.WorldCupQuarterFinal:
    case MatchPhase.WorldCupSemiFinal:
    case MatchPhase.WorldCupThirdPlace:
    case MatchPhase.WorldCupFinal:
      return true;
    default:
      return false;
  }
}

function worldCupPhaseMultiplier(phase: MatchPhase): number {
  switch (phase) {
    case MatchPhase.WorldCupGroupStage:
      return 1;
    case MatchPhase.WorldCupRoundOf32:
    case MatchPhase.WorldCupRoundOf16:
      return 2;
    case MatchPhase.WorldCupQuarterFinal:
    case MatchPhase.WorldCupSemiFinal:
    case MatchPhase.WorldCupThirdPlace:
    case MatchPhase.WorldCupFinal:
      return 3;
    default:
      return 1;
  }
}

/**
 * Effective multiplier of a match, mirroring the backend ScoringService rules
 * (the MatchDto does not carry the computed multiplier before scoring).
 */
export function computeMultiplier(match: RoundMatch): number {
  if (match.manualMultiplierOverride != null) {
    return match.manualMultiplierOverride;
  }

  const derby = isBigSeven(match.homeTeamName) && isBigSeven(match.awayTeamName);

  switch (match.competition) {
    case Competition.PremierLeague:
      return derby ? 2 : 1;
    case Competition.FACup:
      if (match.phase === MatchPhase.FACupFinal) return 3;
      if (match.phase === MatchPhase.FACupSemiFinal) return 2;
      return derby ? 2 : 1;
    case Competition.Championship:
      if (match.phase === MatchPhase.PlayoffSemiFinal || match.phase === MatchPhase.PlayoffFinal)
        return 2;
      return 1;
    case Competition.LeagueOne:
      return 2;
    case Competition.FifaWorldCup: {
      // Phase multiplier, doubled for knockout classics (both world champions).
      const base = worldCupPhaseMultiplier(match.phase);
      return isClassic(match) ? base * 2 : base;
    }
    default:
      return 1;
  }
}

export function isClassic(match: RoundMatch): boolean {
  if (match.competition === Competition.PremierLeague) {
    return isBigSeven(match.homeTeamName) && isBigSeven(match.awayTeamName);
  }
  if (match.competition === Competition.FifaWorldCup) {
    return (
      isWorldCupKnockout(match.phase) &&
      isWorldChampion(match.homeTeamName) &&
      isWorldChampion(match.awayTeamName)
    );
  }
  return false;
}

export function isLeagueOne(match: RoundMatch): boolean {
  return match.competition === Competition.LeagueOne;
}

export function phaseLabel(phase: MatchPhase): string {
  switch (phase) {
    case MatchPhase.PlayoffSemiFinal:
      return 'Semifinal (Playoff)';
    case MatchPhase.PlayoffFinal:
      return 'Final (Playoff)';
    case MatchPhase.FACupSemiFinal:
      return 'Semifinal';
    case MatchPhase.FACupFinal:
      return 'Final';
    case MatchPhase.Other:
      return 'Outra';
    case MatchPhase.WorldCupGroupStage:
      return 'Fase de grupos';
    case MatchPhase.WorldCupRoundOf32:
      return '16-avos de final';
    case MatchPhase.WorldCupRoundOf16:
      return 'Oitavas de final';
    case MatchPhase.WorldCupQuarterFinal:
      return 'Quartas de final';
    case MatchPhase.WorldCupSemiFinal:
      return 'Semifinal';
    case MatchPhase.WorldCupThirdPlace:
      return 'Decisão de 3º lugar';
    case MatchPhase.WorldCupFinal:
      return 'Final';
    default:
      return '';
  }
}
