-- ---------------------------------------------------------------------------
-- Clears everything the scorer produced for the active season, leaving the seeded
-- rows (season, rounds, matches, predictions) untouched, so score-season.ps1 can
-- replay from round 1.
--
-- WHY THIS EXISTS -- the two obvious shortcuts are both silently wrong for a
-- PalpitaoEngland season:
--
--   * POST /seasons/{id}/recalculate deletes PredictionScores /
--     RoundParticipantResults / Absences but NEVER deletes "Standings", and
--     re-scores with updateStandings:false. FlavioRuleService.GetLeadersBeforeRound
--     therefore reads the previous run's END-OF-SEASON standings for every round
--     >= 16, so the rule is evaluated against last run's champion instead of the
--     leader at that point in the season.
--
--   * Re-scoring one middle round in place has the same root cause: it reads the
--     current (end-of-season) Standings, not the standings before that round.
--     reopen + re-score is only faithful for the highest-numbered scored round.
--
-- Clearing everything and replaying in ascending order is the only faithful way.
-- It takes well under a minute for 38 rounds.
--
-- Safe to run repeatedly. Aborts if there is no active season.
-- ---------------------------------------------------------------------------

DO $$
DECLARE
    v_season_id uuid;
    v_group_id  uuid;
    v_season    text;
    v_n         bigint;
BEGIN
    SELECT "Id", "GroupId", "Name" INTO v_season_id, v_group_id, v_season
    FROM "Seasons" WHERE "IsActive" ORDER BY "CreatedAt" DESC LIMIT 1;
    IF v_season_id IS NULL THEN
        RAISE EXCEPTION 'No active season; nothing to reset.';
    END IF;
    RAISE NOTICE 'Resetting scoring for season "%" (%)', v_season, v_season_id;

    DELETE FROM "PredictionScores"
     WHERE "RoundId" IN (SELECT "Id" FROM "Rounds" WHERE "SeasonId" = v_season_id);
    GET DIAGNOSTICS v_n = ROW_COUNT; RAISE NOTICE '  PredictionScores        %', v_n;

    DELETE FROM "RoundParticipantResults" WHERE "SeasonId" = v_season_id;
    GET DIAGNOSTICS v_n = ROW_COUNT; RAISE NOTICE '  RoundParticipantResults %', v_n;

    DELETE FROM "Absences"
     WHERE "RoundId" IN (SELECT "Id" FROM "Rounds" WHERE "SeasonId" = v_season_id);
    GET DIAGNOSTICS v_n = ROW_COUNT; RAISE NOTICE '  Absences                %', v_n;

    -- The one recalculate forgets. Without it the Flavio rule reads a stale
    -- leaderboard on the replay and penalises the wrong person.
    DELETE FROM "Standings" WHERE "SeasonId" = v_season_id;
    GET DIAGNOSTICS v_n = ROW_COUNT; RAISE NOTICE '  Standings               %', v_n;

    -- Eliminations are per-group state, not season state, so they survive the
    -- deletes above. Left set, the replay starts with a short roster from round 1.
    UPDATE "GroupUsers" SET "IsEliminated" = false, "UpdatedAt" = now()
     WHERE "GroupId" = v_group_id AND "IsEliminated";
    GET DIAGNOSTICS v_n = ROW_COUNT; RAISE NOTICE '  eliminations cleared    %', v_n;

    -- Put every played round back into a scoreable state. Rounds with no matches
    -- (the by-hand rehearsal round) stay as they are.
    UPDATE "Rounds" SET "Status" = 'Scored'
     WHERE "SeasonId" = v_season_id
       AND "Status" IN ('Scored', 'Locked')
       AND EXISTS (SELECT 1 FROM "RoundMatches" m WHERE m."RoundId" = "Rounds"."Id");
    GET DIAGNOSTICS v_n = ROW_COUNT; RAISE NOTICE '  rounds ready to score   %', v_n;

    RAISE NOTICE 'Done. Replay with score-season.ps1 from round 1 (ascending, no gaps).';
END $$;
