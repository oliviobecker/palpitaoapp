-- ---------------------------------------------------------------------------
-- Wipe application data, keeping ONLY a chosen super-admin user plus the
-- reference data the app needs to stay usable (the global "Teams" catalogue).
--
-- Two scopes, controlled by v_keep_owner_groups below:
--   * true  (default) -- also keep the group(s) OWNED by the kept admin (their
--                        seasons/rounds/predictions are still wiped; the empty
--                        group shell survives) plus that admin's membership.
--   * false           -- fresh slate: delete EVERY group and membership, so the
--                        kept admin logs in with no group and creates one anew.
--
-- Portable plain SQL: runs in any client (DBeaver, psql, pgAdmin) and via the
-- CI reset workflow (scripts/run-sql.cs). No psql meta-commands (\set / \echo)
-- so it works in GUI tools too.
--
-- What ALWAYS survives: the admin matched by v_admin_email, and all "Teams".
-- What is deleted: every other user, plus all seasons, rounds, matches,
-- predictions, scores, scoring config, standings, absences, OCR imports, audit
-- logs and refresh tokens (and, per the scope above, some/all groups). The
-- __EFMigrationsHistory table is left untouched.
--
-- The admin's refresh tokens are wiped too, so the admin must log in again.
--
-- SAFETY:
--   * The whole wipe is one atomic DO block: if the admin email is not found it
--     RAISES and the transaction rolls back -- nothing is deleted.
--   * >>> EDIT v_admin_email below to the super-admin you want to keep. <<<
--   * Run it as a SCRIPT (DBeaver: Alt+X / "Execute SQL Script"), not a single
--     statement, so the trailing count report runs too.
-- ---------------------------------------------------------------------------

DO $$
DECLARE
    -- >>> CHANGE THIS to the email of the super-admin you want to keep. <<<
    v_admin_email       text    := 'admin@palpitao.local';
    -- true: keep the groups the admin owns (emptied). false: delete ALL groups.
    v_keep_owner_groups boolean := true;
    v_admin_id          uuid;
BEGIN
    SELECT "Id" INTO v_admin_id FROM "Users" WHERE "Email" = v_admin_email;
    IF v_admin_id IS NULL THEN
        RAISE EXCEPTION 'No user found with email %, aborting wipe (nothing deleted).', v_admin_email;
    END IF;

    -- --- Children first (predictions / scoring / OCR) ----------------------
    DELETE FROM "OcrPredictionCandidates";
    DELETE FROM "OcrImportBatches";
    DELETE FROM "PredictionScores";
    DELETE FROM "Predictions";
    DELETE FROM "RoundParticipantResults";
    DELETE FROM "Standings";
    DELETE FROM "AbsenceOverrides";
    DELETE FROM "Absences";

    -- --- Per-season scoring config (children -> parent) --------------------
    -- These cascade from "Seasons" too, but delete them explicitly so the wipe
    -- stays exhaustive if a cascade is ever changed. SeasonScoringConfig is a
    -- tenant root (IGroupOwned) whose Restrict FK to "Groups" would otherwise
    -- block the group delete below if any row lingered.
    DELETE FROM "ScoringScoreEntries";
    DELETE FROM "ScoringMultiplierRules";
    DELETE FROM "ScoringClassicTeams";
    DELETE FROM "SeasonScoringConfigs";

    -- --- Fixtures / structure ----------------------------------------------
    DELETE FROM "RoundMatches";
    DELETE FROM "Rounds";
    DELETE FROM "Seasons";

    -- --- Cross-cutting -----------------------------------------------------
    DELETE FROM "AuditLogs";
    DELETE FROM "RefreshTokens";

    -- --- Memberships & groups ----------------------------------------------
    IF v_keep_owner_groups THEN
        -- Removing every non-admin membership clears the FK (Restrict) that
        -- would otherwise block deleting those users below. The admin's
        -- memberships in groups they do NOT own are cascade-deleted when those
        -- groups go next.
        DELETE FROM "GroupUsers" WHERE "UserId" <> v_admin_id;
        DELETE FROM "Groups"     WHERE "OwnerUserId" <> v_admin_id;
    ELSE
        -- Fresh slate: drop ALL memberships and ALL groups (the admin included).
        DELETE FROM "GroupUsers";
        DELETE FROM "Groups";
    END IF;

    -- --- Users: keep only the admin ----------------------------------------
    DELETE FROM "Users" WHERE "Id" <> v_admin_id;

    RAISE NOTICE 'Wipe complete. Kept admin % (id %), keep_owner_groups=%.', v_admin_email, v_admin_id, v_keep_owner_groups;
END $$;

-- --- Post-wipe sanity report (separate statement) --------------------------
SELECT 'Users'        AS table_name, count(*) AS rows FROM "Users"
UNION ALL SELECT 'Groups',        count(*) FROM "Groups"
UNION ALL SELECT 'GroupUsers',    count(*) FROM "GroupUsers"
UNION ALL SELECT 'Teams',         count(*) FROM "Teams"
UNION ALL SELECT 'Seasons',       count(*) FROM "Seasons"
UNION ALL SELECT 'Rounds',        count(*) FROM "Rounds"
UNION ALL SELECT 'Predictions',   count(*) FROM "Predictions"
UNION ALL SELECT 'Standings',     count(*) FROM "Standings"
UNION ALL SELECT 'RefreshTokens', count(*) FROM "RefreshTokens"
UNION ALL SELECT 'AuditLogs',     count(*) FROM "AuditLogs";
