-- ---------------------------------------------------------------------------
-- Wipe all application data, keeping ONLY the chosen super-admin user plus the
-- reference/seed data the app needs to stay usable (the club/national-team
-- catalogue and the group(s) the kept admin owns).
--
-- Portable plain SQL: runs in any client (DBeaver, psql, pgAdmin). No psql
-- meta-commands (\set / \echo) so it works in GUI tools too.
--
-- What survives:
--   * The admin user matched by email (set v_admin_email below).
--   * All rows in "Teams" (seeded club / national-team catalogue).
--   * Every "Groups" row OWNED by the kept admin, plus that admin's membership.
--
-- What is deleted: every other user, group, membership, season, round, match,
-- prediction, score, standing, absence, OCR import, audit log and refresh
-- token. The __EFMigrationsHistory table is left untouched.
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
    v_admin_email text := 'admin@palpitao.local';
    v_admin_id    uuid;
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

    -- --- Fixtures / structure ----------------------------------------------
    DELETE FROM "RoundMatches";
    DELETE FROM "Rounds";
    DELETE FROM "Seasons";

    -- --- Cross-cutting -----------------------------------------------------
    DELETE FROM "AuditLogs";
    DELETE FROM "RefreshTokens";

    -- --- Memberships: keep only the kept admin's ---------------------------
    -- Removing every non-admin membership clears the FK (Restrict) that would
    -- otherwise block deleting those users below. The admin's memberships in
    -- groups they do NOT own are cascade-deleted when those groups go next.
    DELETE FROM "GroupUsers" WHERE "UserId" <> v_admin_id;

    -- --- Groups: keep only the ones the kept admin owns --------------------
    DELETE FROM "Groups" WHERE "OwnerUserId" <> v_admin_id;

    -- --- Users: keep only the admin ----------------------------------------
    DELETE FROM "Users" WHERE "Id" <> v_admin_id;

    RAISE NOTICE 'Wipe complete. Kept admin % (id %).', v_admin_email, v_admin_id;
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
