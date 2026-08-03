-- ---------------------------------------------------------------------------
-- Delete ONE group and everything under it (a "cascade" the database will not
-- do for you: five of the FKs pointing at "Groups" are Restrict, not Cascade,
-- so a plain DELETE FROM "Groups" fails with a foreign-key violation).
--
-- HOW TO USE (DBeaver: Alt+X / "Execute SQL Script"):
--   1. Run it as-is. v_dry_run = true, so it performs every DELETE, prints the
--      real row counts, and then ROLLS BACK -- nothing is lost. This is not a
--      simulation: it runs the actual statements, so if the delete order were
--      wrong you would see the FK violation here rather than half-way through
--      the real run.
--   2. Read the NOTICE output. If the numbers look like the group you meant,
--      set v_dry_run := false and run it again to commit.
--
-- WHAT IS DELETED: the group row, its memberships, seasons, rounds, matches,
-- predictions, scores, standings, absences, scoring config, OCR imports and
-- audit logs.
--
-- WHAT SURVIVES: the "Users" themselves (a person may belong to other groups --
-- only their membership in THIS group goes), the global "Teams" catalogue, and
-- refresh tokens. Every other group is untouched.
--
-- SAFETY:
--   * One atomic DO block. Any RAISE EXCEPTION rolls the whole thing back.
--   * Aborts if the name matches zero groups, or more than one (names are NOT
--     unique in this schema -- only "Slug" is; see v_group_slug below).
--   * Refuses to delete the seeded default group.
--
-- Ids are captured into arrays BEFORE the parents are deleted -- recomputing
-- them later would return empty sets and silently leave orphans behind.
-- ---------------------------------------------------------------------------

DO $$
DECLARE
    -- >>> The group to delete. Match by name... <<<
    v_group_name text    := 'TESTE - Palpitão England 26/27';
    -- ...or, if two groups share that name, put the unique slug here instead
    -- and the name is ignored. Find it with:
    --   SELECT "Id", "Name", "Slug" FROM "Groups" ORDER BY "Name";
    v_group_slug text    := NULL;

    -- >>> false = actually delete. true = count and roll back. <<<
    v_dry_run    boolean := true;

    v_group_id   uuid;
    v_found_name text;
    v_found_slug text;
    v_matches    bigint;
    v_list       text;

    v_season_ids uuid[];
    v_round_ids  uuid[];
    v_config_ids uuid[];
    v_batch_ids  uuid[];

    v_name_norm  text;
    v_n          bigint;
    v_total      bigint := 0;
BEGIN
    -- Names are typed in the UI and copied by eye, so the comparison folds what
    -- looks identical on screen but never compares equal: case, padding,
    -- repeated spaces, and en/em dashes vs a plain hyphen.
    v_name_norm := lower(btrim(regexp_replace(translate(v_group_name, '–—' || chr(160), '-- '), '\s+', ' ', 'g')));

    -- --- Resolve the group, refusing anything ambiguous ---------------------
    -- The whole group list is built up front so a failure to match can show it:
    -- the stored name rarely reads exactly the way it looks in the UI (season
    -- spelled out, a different dash, a stray space), and guessing costs a round
    -- trip each time.
    SELECT string_agg(format('%s   (slug=%s)', "Name", "Slug"), E'\n    ' ORDER BY "Name")
    INTO v_list FROM "Groups";

    IF v_group_slug IS NOT NULL THEN
        SELECT "Id", "Name", "Slug" INTO v_group_id, v_found_name, v_found_slug
        FROM "Groups" WHERE "Slug" = v_group_slug;
        IF v_group_id IS NULL THEN
            RAISE EXCEPTION E'No group with Slug = "%".\n\n  Groups that exist:\n    %\n\nNothing deleted.',
                v_group_slug, v_list;
        END IF;
    ELSE
        -- Trimmed and case-insensitive: enough to survive a copy-paste, while
        -- still refusing to guess between two genuinely different names.
        SELECT count(*) INTO v_matches
        FROM "Groups"
        WHERE lower(btrim(regexp_replace(translate("Name", '–—' || chr(160), '-- '), '\s+', ' ', 'g'))) = v_name_norm;

        IF v_matches = 0 THEN
            RAISE EXCEPTION
                E'No group matched "%".\n\n  Groups that exist:\n    %\n\nCopy an exact Name above into v_group_name, or put its slug in v_group_slug. Nothing deleted.',
                v_group_name, v_list;
        END IF;
        IF v_matches > 1 THEN
            RAISE EXCEPTION
                E'% groups share the name "%". Set v_group_slug to the one you mean.\n\n  Groups that exist:\n    %\n\nNothing deleted.',
                v_matches, v_group_name, v_list;
        END IF;

        SELECT "Id", "Name", "Slug" INTO v_group_id, v_found_name, v_found_slug
        FROM "Groups"
        WHERE lower(btrim(regexp_replace(translate("Name", '–—' || chr(160), '-- '), '\s+', ' ', 'g'))) = v_name_norm;
    END IF;

    IF v_group_id = '33333333-3333-3333-3333-333333333301'::uuid THEN
        RAISE EXCEPTION 'Refusing to delete the seeded default group (%). Nothing deleted.', v_group_id;
    END IF;

    RAISE NOTICE '=== Group ===';
    RAISE NOTICE '  Name  %', v_found_name;
    RAISE NOTICE '  Slug  %', v_found_slug;
    RAISE NOTICE '  Id    %', v_group_id;

    -- --- Capture the id graph BEFORE deleting anything -----------------------
    -- Rounds and configs are matched on GroupId OR their season, because the
    -- GroupId columns carry a DB default (the seeded group): a row inserted
    -- without an explicit group would otherwise be missed here.
    SELECT coalesce(array_agg("Id"), '{}') INTO v_season_ids
    FROM "Seasons" WHERE "GroupId" = v_group_id;

    SELECT coalesce(array_agg("Id"), '{}') INTO v_round_ids
    FROM "Rounds" WHERE "GroupId" = v_group_id OR "SeasonId" = ANY(v_season_ids);

    SELECT coalesce(array_agg("Id"), '{}') INTO v_config_ids
    FROM "SeasonScoringConfigs" WHERE "GroupId" = v_group_id OR "SeasonId" = ANY(v_season_ids);

    SELECT coalesce(array_agg("Id"), '{}') INTO v_batch_ids
    FROM "OcrImportBatches" WHERE "RoundId" = ANY(v_round_ids);

    RAISE NOTICE '  seasons % / rounds % / scoring configs % / ocr batches %',
        cardinality(v_season_ids), cardinality(v_round_ids),
        cardinality(v_config_ids), cardinality(v_batch_ids);
    RAISE NOTICE '';
    RAISE NOTICE '=== Rows deleted ===';

    -- --- OCR imports ---------------------------------------------------------
    -- Candidates first: their RoundId/UserId/RoundMatchId are plain columns with
    -- no FK, so nothing else would ever clean them up.
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
    -- PredictionScores MUST precede RoundMatches: PredictionScores.RoundMatchId
    -- is Restrict (while its RoundId is Cascade -- easy trap).
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
    WHERE "GroupId" = v_group_id
       OR "SeasonId" = ANY(v_season_ids)
       OR "RoundId"  = ANY(v_round_ids);
    GET DIAGNOSTICS v_n = ROW_COUNT; v_total := v_total + v_n;
    RAISE NOTICE '  % %', rpad('RoundParticipantResults', 26), v_n;

    DELETE FROM "Standings"
    WHERE "GroupId" = v_group_id OR "SeasonId" = ANY(v_season_ids);
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

    -- --- The tenant roots ----------------------------------------------------
    DELETE FROM "Rounds" WHERE "Id" = ANY(v_round_ids);
    GET DIAGNOSTICS v_n = ROW_COUNT; v_total := v_total + v_n;
    RAISE NOTICE '  % %', rpad('Rounds', 26), v_n;

    DELETE FROM "Seasons" WHERE "Id" = ANY(v_season_ids);
    GET DIAGNOSTICS v_n = ROW_COUNT; v_total := v_total + v_n;
    RAISE NOTICE '  % %', rpad('Seasons', 26), v_n;

    -- AuditLogs."GroupId" has NO foreign key, so nothing would ever clean these
    -- up; skipping this leaves rows pointing at a group that no longer exists.
    DELETE FROM "AuditLogs" WHERE "GroupId" = v_group_id;
    GET DIAGNOSTICS v_n = ROW_COUNT; v_total := v_total + v_n;
    RAISE NOTICE '  % %', rpad('AuditLogs', 26), v_n;

    -- Memberships only -- the "Users" rows stay, they may belong to other groups.
    DELETE FROM "GroupUsers" WHERE "GroupId" = v_group_id;
    GET DIAGNOSTICS v_n = ROW_COUNT; v_total := v_total + v_n;
    RAISE NOTICE '  % %', rpad('GroupUsers', 26), v_n;

    DELETE FROM "Groups" WHERE "Id" = v_group_id;
    GET DIAGNOSTICS v_n = ROW_COUNT; v_total := v_total + v_n;
    RAISE NOTICE '  % %', rpad('Groups', 26), v_n;

    RAISE NOTICE '';
    RAISE NOTICE '  TOTAL % rows', v_total;
    RAISE NOTICE '';

    IF v_dry_run THEN
        RAISE EXCEPTION
            'DRY RUN -- rolled back, nothing was deleted. The counts above are real. Set v_dry_run := false to commit.';
    END IF;

    RAISE NOTICE 'Group "%" deleted.', v_found_name;
END $$;
