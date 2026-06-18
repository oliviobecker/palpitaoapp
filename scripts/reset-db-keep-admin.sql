-- ---------------------------------------------------------------------------
-- Wipe all application data, keeping ONLY the seeded super-admin user plus the
-- reference/seed data the app needs to stay usable (the club/national-team
-- catalogue and the default group + the admin's membership in it).
--
-- What survives:
--   * The admin user (matched by email, default admin@palpitao.local).
--   * All rows in "Teams"            (seeded club / national-team catalogue).
--   * The default group "Groups" row (33333333-...-301).
--   * The admin's membership in the default group ("GroupUsers").
--
-- What is deleted: every other user, group, membership, season, round, match,
-- prediction, score, standing, absence, OCR import, audit log and refresh
-- token. The __EFMigrationsHistory table is left untouched.
--
-- The admin's refresh tokens are wiped too, so the admin must log in again.
--
-- Usage (the .ps1 runner passes these for you):
--   psql ... -v ON_ERROR_STOP=1 \
--            --set=admin_email=admin@palpitao.local \
--            --set=default_group_id=33333333-3333-3333-3333-333333333301 \
--            -f reset-db-keep-admin.sql
-- ---------------------------------------------------------------------------

\if :{?admin_email}
\else
  \set admin_email 'admin@palpitao.local'
\endif

\if :{?default_group_id}
\else
  \set default_group_id '33333333-3333-3333-3333-333333333301'
\endif

\echo Keeping admin user with email = :'admin_email'
\echo Keeping default group id      = :'default_group_id'

BEGIN;

-- Fail loudly if the admin we are supposed to keep does not exist, rather than
-- silently wiping the whole table.
DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM "Users" WHERE "Email" = :'admin_email') THEN
        RAISE EXCEPTION 'No user found with email %, aborting wipe.', :'admin_email';
    END IF;
END $$;

-- --- Children first (predictions / scoring / OCR) --------------------------
DELETE FROM "OcrPredictionCandidates";
DELETE FROM "OcrImportBatches";
DELETE FROM "PredictionScores";
DELETE FROM "Predictions";
DELETE FROM "RoundParticipantResults";
DELETE FROM "Standings";
DELETE FROM "AbsenceOverrides";
DELETE FROM "Absences";

-- --- Fixtures / structure --------------------------------------------------
DELETE FROM "RoundMatches";
DELETE FROM "Rounds";
DELETE FROM "Seasons";

-- --- Cross-cutting ---------------------------------------------------------
DELETE FROM "AuditLogs";
DELETE FROM "RefreshTokens";

-- --- Memberships: keep only the kept admin's -------------------------------
-- Removing every non-admin membership clears the FK (Restrict) that would
-- otherwise block deleting those users below. Admin memberships in *other*
-- groups are cascade-deleted when those groups go in the next step.
DELETE FROM "GroupUsers"
WHERE "UserId" <> (SELECT "Id" FROM "Users" WHERE "Email" = :'admin_email');

-- --- Groups: keep only the default group -----------------------------------
DELETE FROM "Groups"
WHERE "Id" <> :'default_group_id';

-- --- Users: keep only the admin --------------------------------------------
DELETE FROM "Users"
WHERE "Email" <> :'admin_email';

COMMIT;

-- --- Post-wipe sanity report -----------------------------------------------
\echo '--- Row counts after wipe ---'
SELECT 'Users'        AS table, count(*) FROM "Users"
UNION ALL SELECT 'Groups',        count(*) FROM "Groups"
UNION ALL SELECT 'GroupUsers',    count(*) FROM "GroupUsers"
UNION ALL SELECT 'Teams',         count(*) FROM "Teams"
UNION ALL SELECT 'Seasons',       count(*) FROM "Seasons"
UNION ALL SELECT 'Rounds',        count(*) FROM "Rounds"
UNION ALL SELECT 'Predictions',   count(*) FROM "Predictions"
UNION ALL SELECT 'Standings',     count(*) FROM "Standings"
UNION ALL SELECT 'RefreshTokens', count(*) FROM "RefreshTokens"
UNION ALL SELECT 'AuditLogs',     count(*) FROM "AuditLogs";
