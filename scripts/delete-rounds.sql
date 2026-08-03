-- ---------------------------------------------------------------------------
-- Physically delete ROUNDS of one season -- the season, its scoring config, the
-- group, its members and every other season stay. Use this to empty a season
-- that was filled with test rounds without recreating it.
--
-- HOW TO USE (DBeaver: Alt+X / "Execute SQL Script"):
--   1. Run it as-is. v_dry_run = true, so every DELETE really runs, the real row
--      counts are printed, and then it ROLLS BACK. Not a simulation: a wrong
--      delete order would fail here rather than half-way through the real run.
--   2. Read the NOTICE output. If the numbers match, set v_dry_run := false and
--      run again to commit.
--
-- WHAT IS DELETED: the rounds, their matches, predictions, prediction scores,
-- absences, per-round results and OCR imports.
--
-- WHAT SURVIVES: the season row and its scoring configuration (score table,
-- multipliers, classic pairs), the group, memberships, users, teams and the
-- audit log.
--
-- ABOUT v_reset_standings (default true): "Standings" is season-scoped, not
-- round-scoped, and StandingsService rebuilds it from RoundParticipantResults.
-- Deleting rounds without clearing it leaves the Classificação screen showing
-- points earned in rounds that no longer exist, and participants eliminated by
-- absences that no longer exist. With the flag on, both are reset, so the season
-- reads as genuinely empty. Turn it off only if you plan to run "Recalcular" in
-- the admin UI straight afterwards, which rebuilds the same two things.
--
-- SAFETY: one atomic DO block; any RAISE EXCEPTION rolls it all back. Aborts if
-- the season name matches zero rows or more than one, and lists what exists.
-- ---------------------------------------------------------------------------

DO $$
DECLARE
    -- >>> The season whose rounds you want to delete. <<<
    -- Case, extra spaces and dash style (- vs – vs —) do not matter.
    v_season_name     text    := 'TESTE - Palpitão England 26/27';
    -- Optional: only needed when two groups have a season with the same name.
    v_group_name      text    := NULL;

    -- Optional: delete only rounds in this state. RoundStatus is stored as text
    -- (HasConversion<string>), so these are the literal values in the column:
    -- 'Draft', 'Published', 'Locked', 'Scored', 'Cancelled'.
    -- NULL = every round of the season.
    v_only_status     text    := NULL;

    -- Reset the season's standings and un-eliminate its participants (see above).
    v_reset_standings boolean := true;

    -- >>> false = actually delete. true = count and roll back. <<<
    v_dry_run         boolean := true;

    v_season_id       uuid;
    v_found_name      text;
    v_group_id        uuid;
    v_group_label     text;
    v_matches         bigint;
    v_list            text;

    v_round_ids       uuid[];
    v_batch_ids       uuid[];

    v_season_norm     text;
    v_group_norm      text;
    v_n               bigint;
    v_total           bigint := 0;
BEGIN
    -- --- Resolve the season, refusing anything ambiguous ---------------------
    -- Names are typed in the UI and copied by eye, so the comparison folds the
    -- things that look identical on screen but never compare equal: case,
    -- padding, repeated spaces, and en/em dashes vs a plain hyphen. A season
    -- really named "TESTE – X" (en dash) is otherwise unreachable by typing
    -- "TESTE - X".
    v_season_norm := lower(btrim(regexp_replace(translate(v_season_name, '–—' || chr(160), '-- '), '\s+', ' ', 'g')));
    v_group_norm  := lower(btrim(regexp_replace(translate(v_group_name,  '–—' || chr(160), '-- '), '\s+', ' ', 'g')));

    SELECT string_agg(
               format('%s   [group: %s]', s."Name", g."Name"),
               E'\n    ' ORDER BY g."Name", s."Name")
    INTO v_list
    FROM "Seasons" s JOIN "Groups" g ON g."Id" = s."GroupId";

    SELECT count(*) INTO v_matches
    FROM "Seasons" s JOIN "Groups" g ON g."Id" = s."GroupId"
    WHERE lower(btrim(regexp_replace(translate(s."Name", '–—' || chr(160), '-- '), '\s+', ' ', 'g'))) = v_season_norm
      AND (v_group_norm IS NULL
           OR lower(btrim(regexp_replace(translate(g."Name", '–—' || chr(160), '-- '), '\s+', ' ', 'g'))) = v_group_norm);

    IF v_matches = 0 THEN
        RAISE EXCEPTION
            E'No season matched "%".\n\n  Seasons that exist:\n    %\n\nCopy an exact name above into v_season_name. Nothing deleted.',
            v_season_name, v_list;
    END IF;
    IF v_matches > 1 THEN
        RAISE EXCEPTION
            E'% seasons share the name "%". Set v_group_name to pick one.\n\n  Seasons that exist:\n    %\n\nNothing deleted.',
            v_matches, v_season_name, v_list;
    END IF;

    SELECT s."Id", s."Name", g."Id", g."Name"
    INTO v_season_id, v_found_name, v_group_id, v_group_label
    FROM "Seasons" s JOIN "Groups" g ON g."Id" = s."GroupId"
    WHERE lower(btrim(regexp_replace(translate(s."Name", '–—' || chr(160), '-- '), '\s+', ' ', 'g'))) = v_season_norm
      AND (v_group_norm IS NULL
           OR lower(btrim(regexp_replace(translate(g."Name", '–—' || chr(160), '-- '), '\s+', ' ', 'g'))) = v_group_norm);

    -- --- Capture the rounds BEFORE deleting anything -------------------------
    SELECT coalesce(array_agg("Id"), '{}') INTO v_round_ids
    FROM "Rounds"
    WHERE "SeasonId" = v_season_id
      AND (v_only_status IS NULL OR "Status" = v_only_status);

    SELECT coalesce(array_agg("Id"), '{}') INTO v_batch_ids
    FROM "OcrImportBatches" WHERE "RoundId" = ANY(v_round_ids);

    RAISE NOTICE '=== Season (kept) ===';
    RAISE NOTICE '  %  [group: %]', v_found_name, v_group_label;
    RAISE NOTICE '';
    RAISE NOTICE '=== Rounds to delete (%) ===', cardinality(v_round_ids);
    FOR v_list IN
        SELECT format('  Rodada %s%s  -  %s  -  %s jogo(s)',
                      r."Number",
                      coalesce(' · ' || r."Title", ''),
                      r."Status",
                      (SELECT count(*) FROM "RoundMatches" m WHERE m."RoundId" = r."Id"))
        FROM "Rounds" r WHERE r."Id" = ANY(v_round_ids) ORDER BY r."Number", r."CreatedAt"
    LOOP
        RAISE NOTICE '%', v_list;
    END LOOP;

    IF cardinality(v_round_ids) = 0 THEN
        RAISE EXCEPTION 'The season has no round matching the filter. Nothing deleted.';
    END IF;

    RAISE NOTICE '';
    RAISE NOTICE '=== Rows deleted ===';

    -- --- OCR imports ---------------------------------------------------------
    -- Candidates carry RoundId/UserId/RoundMatchId as plain columns with no FK,
    -- so only the batch link ever clears them.
    DELETE FROM "OcrPredictionCandidates" WHERE "OcrImportBatchId" = ANY(v_batch_ids);
    GET DIAGNOSTICS v_n = ROW_COUNT; v_total := v_total + v_n;
    RAISE NOTICE '  % %', rpad('OcrPredictionCandidates', 26), v_n;

    DELETE FROM "OcrImportImages" WHERE "OcrImportBatchId" = ANY(v_batch_ids);
    GET DIAGNOSTICS v_n = ROW_COUNT; v_total := v_total + v_n;
    RAISE NOTICE '  % %', rpad('OcrImportImages', 26), v_n;

    DELETE FROM "OcrImportBatches" WHERE "Id" = ANY(v_batch_ids);
    GET DIAGNOSTICS v_n = ROW_COUNT; v_total := v_total + v_n;
    RAISE NOTICE '  % %', rpad('OcrImportBatches', 26), v_n;

    -- --- Scores and predictions ----------------------------------------------
    -- PredictionScores before RoundMatches: its RoundMatchId is Restrict, while
    -- its RoundId is Cascade. Same table, two behaviours.
    DELETE FROM "PredictionScores" WHERE "RoundId" = ANY(v_round_ids);
    GET DIAGNOSTICS v_n = ROW_COUNT; v_total := v_total + v_n;
    RAISE NOTICE '  % %', rpad('PredictionScores', 26), v_n;

    -- Predictions.RoundId is Restrict, so these must precede Rounds.
    DELETE FROM "Predictions" WHERE "RoundId" = ANY(v_round_ids);
    GET DIAGNOSTICS v_n = ROW_COUNT; v_total := v_total + v_n;
    RAISE NOTICE '  % %', rpad('Predictions', 26), v_n;

    DELETE FROM "RoundMatches" WHERE "RoundId" = ANY(v_round_ids);
    GET DIAGNOSTICS v_n = ROW_COUNT; v_total := v_total + v_n;
    RAISE NOTICE '  % %', rpad('RoundMatches', 26), v_n;

    -- --- Absences and per-round results --------------------------------------
    DELETE FROM "AbsenceOverrides" WHERE "RoundId" = ANY(v_round_ids);
    GET DIAGNOSTICS v_n = ROW_COUNT; v_total := v_total + v_n;
    RAISE NOTICE '  % %', rpad('AbsenceOverrides', 26), v_n;

    DELETE FROM "Absences" WHERE "RoundId" = ANY(v_round_ids);
    GET DIAGNOSTICS v_n = ROW_COUNT; v_total := v_total + v_n;
    RAISE NOTICE '  % %', rpad('Absences', 26), v_n;

    DELETE FROM "RoundParticipantResults" WHERE "RoundId" = ANY(v_round_ids);
    GET DIAGNOSTICS v_n = ROW_COUNT; v_total := v_total + v_n;
    RAISE NOTICE '  % %', rpad('RoundParticipantResults', 26), v_n;

    -- --- The rounds themselves -----------------------------------------------
    DELETE FROM "Rounds" WHERE "Id" = ANY(v_round_ids);
    GET DIAGNOSTICS v_n = ROW_COUNT; v_total := v_total + v_n;
    RAISE NOTICE '  % %', rpad('Rounds', 26), v_n;

    -- --- Stale season-level state --------------------------------------------
    IF v_reset_standings THEN
        DELETE FROM "Standings" WHERE "SeasonId" = v_season_id;
        GET DIAGNOSTICS v_n = ROW_COUNT; v_total := v_total + v_n;
        RAISE NOTICE '  % %', rpad('Standings', 26), v_n;

        UPDATE "GroupUsers" SET "IsEliminated" = false
        WHERE "GroupId" = v_group_id AND "IsEliminated";
        GET DIAGNOSTICS v_n = ROW_COUNT;
        RAISE NOTICE '  % % (un-eliminated, not deleted)', rpad('GroupUsers', 26), v_n;
    ELSE
        RAISE NOTICE '  (standings and eliminations left as they were --';
        RAISE NOTICE '   run Recalcular in the admin UI to rebuild them)';
    END IF;

    RAISE NOTICE '';
    RAISE NOTICE '  TOTAL % rows deleted', v_total;
    RAISE NOTICE '';

    IF v_dry_run THEN
        RAISE EXCEPTION
            'DRY RUN -- rolled back, nothing was deleted. The counts above are real. Set v_dry_run := false to commit.';
    END IF;

    RAISE NOTICE 'Deleted % round(s) from season "%". The season itself was kept.',
        cardinality(v_round_ids), v_found_name;
END $$;
