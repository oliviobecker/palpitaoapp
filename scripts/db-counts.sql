-- Read-only row-count snapshot of the tenant-relevant tables. Safe to run any
-- time: the reset workflow (.github/workflows/reset-db.yml) runs it to record
-- the "before" state, and it is handy locally to eyeball what a wipe affects.
SELECT 'Users'            AS table_name, count(*) AS rows FROM "Users"
UNION ALL SELECT 'Groups',            count(*) FROM "Groups"
UNION ALL SELECT 'GroupUsers',        count(*) FROM "GroupUsers"
UNION ALL SELECT 'Teams',             count(*) FROM "Teams"
UNION ALL SELECT 'Seasons',           count(*) FROM "Seasons"
UNION ALL SELECT 'Rounds',            count(*) FROM "Rounds"
UNION ALL SELECT 'RoundMatches',      count(*) FROM "RoundMatches"
UNION ALL SELECT 'Predictions',       count(*) FROM "Predictions"
UNION ALL SELECT 'PredictionScores',  count(*) FROM "PredictionScores"
UNION ALL SELECT 'Standings',         count(*) FROM "Standings"
UNION ALL SELECT 'Absences',          count(*) FROM "Absences"
UNION ALL SELECT 'SeasonScoringConfigs', count(*) FROM "SeasonScoringConfigs"
UNION ALL SELECT 'RefreshTokens',     count(*) FROM "RefreshTokens"
UNION ALL SELECT 'AuditLogs',         count(*) FROM "AuditLogs"
ORDER BY table_name;
