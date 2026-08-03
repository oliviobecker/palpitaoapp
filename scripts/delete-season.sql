-- ---------------------------------------------------------------------------
-- Delete ONE season and everything under it, leaving the group, its members and
-- every other season alone. Use this when a test season was created inside a
-- real group -- scripts/delete-group.sql would take the whole tenant with it.
--
-- HOW TO USE (DBeaver: Alt+X / "Execute SQL Script"):
--   1. Run it as-is. v_dry_run = true, so it performs every DELETE, prints the
--      real row counts, and then ROLLS BACK -- nothing is lost. Not a
--      simulation: the statements really run, so a wrong delete order would
--      surface here instead of half-way through the real thing.
--   2. Read the NOTICE output. If the numbers match the season you meant, set
--      v_dry_run := false and run again to commit.
--
-- WHAT IS DELETED: the season row, its rounds, matches, predictions, scores,
-- standings, absences, per-season scoring config and OCR imports.
--
-- WHAT SURVIVES: the group and its memberships, every OTHER season of that
-- group, all "Users", the global "Teams" catalogue, and the audit log (its rows
-- are group-scoped, not season-scoped -- they stay as history).
--
-- SAFETY: one atomic DO block; any RAISE EXCEPTION rolls it all back. Aborts if
-- the season name matches zero rows or more than one, and lists what exists.
-- ---------------------------------------------------------------------------

DO $$
DECLARE
    -- >>> The season to delete. <<<
    v_season_name text    := 'TESTE - Palpitão England 26/27';
    -- Optional: only needed when two groups have a season with the same name.
    v_group_name  text    := NULL;

    -- >>> false = actually delete. true = count and roll back. <<<
    v_dry_run     boolean := true;

    v_season_id   uuid;
    v_found_name  text;
    v_group_id    uuid;
    v_group_label text;
    v_matches     bigint;
    v_list        text;

    v_round_ids   uuid[];
    v_config_ids  uuid[];
    v_batch_ids   uuid[];

    v_season_norm text;
    v_group_norm  text;
    v_n           bigint;
    v_total       bigint := 0;
BEGIN
    -- --- Resolve the season, refusing anything ambiguous ---------------------
    -- Names are typed in the UI and copied by eye, so the comparison folds what
    -- looks identical on screen but never compares equal: case, padding,
    -- repeated spaces, and en/em dashes vs a plain hyphen. A season really named
    -- "TESTE – X" (en dash) is otherwise unreachable by typing "TESTE - X".
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

    RAISE NOTICE '=== Season ===';
    RAISE NOTICE '  Name   %', v_found_name;
    RAISE NOTICE '  Id     %', v_season_id;
    RAISE NOTICE '  Group  %  (kept)', v_group_label;

    -- --- Capture the id graph BEFORE deleting anything -----------------------
    SELECT coalesce(array_agg("Id"), '{}') INTO v_round_ids
    FROM "Rounds" WHERE "SeasonId" = v_season_id;

    SELECT coalesce(array_agg("Id"), '{}') INTO v_config_ids
    FROM "SeasonScoringConfigs" WHERE "SeasonId" = v_season_id;

    SELECT coalesce(array_agg("Id"), '{}') INTO v_batch_ids
    FROM "OcrImportBatches" WHERE "RoundId" = ANY(v_round_ids);

    RAISE NOTICE '  rounds % / scoring configs % / ocr batches %',
        cardinality(v_round_ids), cardinality(v_config_ids), cardinality(v_batch_ids);
    RAISE NOTICE '';
    RAISE NOTICE '=== Rows deleted ===';

    -- --- OCR imports ---------------------------------------------------------
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
    -- PredictionScores before RoundMatches: its RoundMatchId is Restrict.
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

    -- --- Absences, results, standings ----------------------------------------
    DELETE FROM "AbsenceOverrides" WHERE "RoundId" = ANY(v_round_ids);
    GET DIAGNOSTICS v_n = ROW_COUNT; v_total := v_total + v_n;
    RAISE NOTICE '  % %', rpad('AbsenceOverrides', 26), v_n;

    DELETE FROM "Absences" WHERE "RoundId" = ANY(v_round_ids);
    GET DIAGNOSTICS v_n = ROW_COUNT; v_total := v_total + v_n;
    RAISE NOTICE '  % %', rpad('Absences', 26), v_n;

    DELETE FROM "RoundParticipantResults"
    WHERE "SeasonId" = v_season_id OR "RoundId" = ANY(v_round_ids);
    GET DIAGNOSTICS v_n = ROW_COUNT; v_total := v_total + v_n;
    RAISE NOTICE '  % %', rpad('RoundParticipantResults', 26), v_n;

    DELETE FROM "Standings" WHERE "SeasonId" = v_season_id;
    GET DIAGNOSTICS v_n = ROW_COUNT; v_total := v_total + v_n;
    RAISE NOTICE '  % %', rpad('Standings', 26), v_n;

    -- --- Scoring configuration -----------------------------------------------
    DELETE FROM "ScoringScoreEntries" WHERE "ConfigId" = ANY(v_config_ids);
    GET DIAGNOSTICS v_n = ROW_COUNT; v_total := v_total + v_n;
    RAISE NOTICE '  % %', rpad('ScoringScoreEntries', 26), v_n;

    DELETE FROM "ScoringMultiplierRules" WHERE "ConfigId" = ANY(v_config_ids);
    GET DIAGNOSTICS v_n = ROW_COUNT; v_total := v_total + v_n;
    RAISE NOTICE '  % %', rpad('ScoringMultiplierRules', 26), v_n;

    DELETE FROM "ScoringClassicTeams" WHERE "ConfigId" = ANY(v_config_ids);
    GET DIAGNOSTICS v_n = ROW_COUNT; v_total := v_total + v_n;
    RAISE NOTICE '  % %', rpad('ScoringClassicTeams', 26), v_n;

    DELETE FROM "SeasonScoringConfigs" WHERE "Id" = ANY(v_config_ids);
    GET DIAGNOSTICS v_n = ROW_COUNT; v_total := v_total + v_n;
    RAISE NOTICE '  % %', rpad('SeasonScoringConfigs', 26), v_n;

    -- --- The season itself ---------------------------------------------------
    DELETE FROM "Rounds" WHERE "Id" = ANY(v_round_ids);
    GET DIAGNOSTICS v_n = ROW_COUNT; v_total := v_total + v_n;
    RAISE NOTICE '  % %', rpad('Rounds', 26), v_n;

    DELETE FROM "Seasons" WHERE "Id" = v_season_id;
    GET DIAGNOSTICS v_n = ROW_COUNT; v_total := v_total + v_n;
    RAISE NOTICE '  % %', rpad('Seasons', 26), v_n;

    RAISE NOTICE '';
    RAISE NOTICE '  TOTAL % rows', v_total;
    RAISE NOTICE '';

    IF v_dry_run THEN
        RAISE EXCEPTION
            'DRY RUN -- rolled back, nothing was deleted. The counts above are real. Set v_dry_run := false to commit.';
    END IF;

    RAISE NOTICE 'Season "%" deleted. Group "%" kept.', v_found_name, v_group_label;
END $$;
