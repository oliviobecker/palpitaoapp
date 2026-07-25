-- Read-only row-count snapshot of the tenant-relevant tables, emitted via RAISE
-- NOTICE (so scripts/run-sql.cs echoes it). Safe to run any time: the reset
-- workflow (.github/workflows/reset-db.yml) runs it for the before/after report,
-- and it is handy locally to eyeball what a wipe affects.
--
-- Schema-version tolerant: a table absent from this environment's schema
-- (migrations not yet applied) is reported as "(absent)" instead of erroring.
DO $$
DECLARE
    t text;
    n bigint;
    v_tables text[] := ARRAY[
        'Users', 'Groups', 'GroupUsers', 'Teams',
        'Seasons', 'Rounds', 'RoundMatches',
        'Predictions', 'PredictionScores', 'Standings', 'Absences',
        'SeasonScoringConfigs', 'RefreshTokens', 'AuditLogs'
    ];
BEGIN
    FOREACH t IN ARRAY v_tables LOOP
        IF to_regclass(format('%I', t)) IS NOT NULL THEN
            EXECUTE format('SELECT count(*) FROM %I', t) INTO n;
            RAISE NOTICE '  % %', rpad(t, 22), n;
        ELSE
            RAISE NOTICE '  % (absent)', rpad(t, 22);
        END IF;
    END LOOP;
END $$;
