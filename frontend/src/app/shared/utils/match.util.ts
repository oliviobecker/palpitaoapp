import { Competition, MatchPhase } from '../../core/models/enums';
import { RoundMatch, ScoringConfig } from '../../core/models/models';

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
 * Effective multiplier of a match, mirroring the backend scoring rules (match DTOs do
 * not carry the computed multiplier before scoring). When a season `config` is supplied
 * the per-season ruleset is used; otherwise it falls back to the historical defaults.
 */
export function computeMultiplier(match: RoundMatch, config?: ScoringConfig | null): number {
  if (match.manualMultiplierOverride != null) {
    return match.manualMultiplierOverride;
  }

  if (isUsableConfig(config)) {
    return multiplierFromConfig(match, config);
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

/** A config is usable only when it actually carries a multiplier table (guards empty/partial payloads). */
function isUsableConfig(config?: ScoringConfig | null): config is ScoringConfig {
  return !!config && Array.isArray(config.multiplierRules) && config.multiplierRules.length > 0;
}

/** Multiplier from a per-season config: (competition, phase) row, falling back to Regular. */
function multiplierFromConfig(match: RoundMatch, config: ScoringConfig): number {
  const classic = isClassicFromConfig(match, config);
  const rules = config.multiplierRules ?? [];
  const rule =
    rules.find((r) => r.competition === match.competition && r.phase === match.phase) ??
    rules.find((r) => r.competition === match.competition && r.phase === MatchPhase.Regular);
  if (!rule) return 1;
  return classic ? rule.classicMultiplier : rule.multiplier;
}

/** Both teams are classic-eligible per the season config (matches the backend by team id). */
function isClassicFromConfig(match: RoundMatch, config: ScoringConfig): boolean {
  const classicIds = new Set((config.teams ?? []).filter((t) => t.isClassic).map((t) => t.teamId));
  return classicIds.has(match.homeTeamId) && classicIds.has(match.awayTeamId);
}

export function isClassic(match: RoundMatch, config?: ScoringConfig | null): boolean {
  if (isUsableConfig(config)) {
    return isClassicFromConfig(match, config);
  }
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
