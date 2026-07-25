-- ---------------------------------------------------------------------------
-- Post-scoring assertions for the rehearsal season.
--
-- Read-only. Every check either RAISEs NOTICE (report) or RAISEs EXCEPTION
-- (failure), so scripts/run-sql.cs echoes the report and exits non-zero the
-- moment something is incoherent.
--
-- Run it AFTER scripts/rehearsal/score-season.ps1. Before scoring, checks B and
-- onwards will fail by design -- the derived tables simply do not exist yet.
--
-- v_flavio_round is rewritten by .github/workflows/seed-rehearsal-staging.yml.
-- ---------------------------------------------------------------------------

DO $$
DECLARE
    v_flavio_round int := 18;

    v_season_id  uuid;
    v_group_id   uuid;
    v_season     text;
    r            record;
    v_n          bigint;
    v_bad        text;
BEGIN
    SELECT "Id", "GroupId", "Name" INTO v_season_id, v_group_id, v_season
    FROM "Seasons" WHERE "IsActive" ORDER BY "CreatedAt" DESC LIMIT 1;
    IF v_season_id IS NULL THEN
        RAISE EXCEPTION 'No active season found; nothing to verify.';
    END IF;
    RAISE NOTICE 'Verifying season "%" (%)', v_season, v_season_id;

    -- --- A. Shape ----------------------------------------------------------
    -- Standings must have one row per PARTICIPANT. The super-admin holds a
    -- GroupAdmin membership and GroupQueries.ApprovedMemberships filters
    -- Role='Participant', so the admin being absent here is correct, not a bug.
    SELECT count(*) INTO v_n FROM "GroupUsers"
     WHERE "GroupId" = v_group_id AND "Role" = 'Participant' AND "Status" = 'Approved';
    RAISE NOTICE 'A. participants=%  rounds=%  matches=%  predictions=%  standings=%',
        v_n,
        (SELECT count(*) FROM "Rounds" WHERE "SeasonId" = v_season_id),
        (SELECT count(*) FROM "RoundMatches" m JOIN "Rounds" ro ON ro."Id" = m."RoundId" WHERE ro."SeasonId" = v_season_id),
        (SELECT count(*) FROM "Predictions" p JOIN "Rounds" ro ON ro."Id" = p."RoundId" WHERE ro."SeasonId" = v_season_id),
        (SELECT count(*) FROM "Standings" WHERE "SeasonId" = v_season_id);

    IF (SELECT count(*) FROM "Standings" WHERE "SeasonId" = v_season_id) <> v_n THEN
        RAISE EXCEPTION 'A. Standings rows (%) != approved participants (%).',
            (SELECT count(*) FROM "Standings" WHERE "SeasonId" = v_season_id), v_n;
    END IF;

    SELECT count(*) INTO v_n FROM "RoundMatches" m
      JOIN "Rounds" ro ON ro."Id" = m."RoundId"
     WHERE ro."SeasonId" = v_season_id AND (m."HomeScore" IS NULL OR m."AwayScore" IS NULL)
       AND ro."Status" <> 'Draft';
    IF v_n > 0 THEN
        RAISE EXCEPTION 'A. % match(es) on non-Draft rounds have no result; those rounds cannot be scored.', v_n;
    END IF;

    -- --- B. No round claims Scored without results -------------------------
    -- Catches "the seeder wrote Status=Scored but the scorer never ran, or died
    -- halfway" -- the single most likely way this whole exercise goes wrong.
    SELECT string_agg('R' || ro."Number", ', ' ORDER BY ro."Number") INTO v_bad
    FROM "Rounds" ro
    WHERE ro."SeasonId" = v_season_id AND ro."Status" = 'Scored'
      AND NOT EXISTS (SELECT 1 FROM "RoundParticipantResults" rr WHERE rr."RoundId" = ro."Id");
    IF v_bad IS NOT NULL THEN
        RAISE EXCEPTION 'B. Rounds marked Scored but with no participant results: %. Run score-season.ps1.', v_bad;
    END IF;
    RAISE NOTICE 'B. every Scored round has participant results.';

    -- --- C. Per-round coherence (report) -----------------------------------
    FOR r IN
        SELECT ro."Number",
               count(DISTINCT m."Id")                            AS matches,
               count(DISTINCT rr."UserId")                       AS results,
               count(DISTINCT rr."UserId") FILTER (WHERE rr."WasAbsent")        AS absent,
               count(DISTINCT rr."UserId") FILTER (WHERE rr."FlavioRuleApplied") AS flavio
          FROM "Rounds" ro
          LEFT JOIN "RoundMatches" m             ON m."RoundId"  = ro."Id"
          LEFT JOIN "RoundParticipantResults" rr ON rr."RoundId" = ro."Id"
         WHERE ro."SeasonId" = v_season_id AND ro."Status" = 'Scored'
         GROUP BY ro."Number"
        HAVING count(DISTINCT rr."UserId") FILTER (WHERE rr."WasAbsent") > 0
            OR count(DISTINCT rr."UserId") FILTER (WHERE rr."FlavioRuleApplied") > 0
         ORDER BY ro."Number"
    LOOP
        RAISE NOTICE 'C. R%: % matches, % results, % absent, % flavio',
            r."Number", r.matches, r.results, r.absent, r.flavio;
    END LOOP;

    -- --- D. Standings recomputed independently -----------------------------
    SELECT string_agg(format('%s (stored %s / recomputed %s)', u."Name", s."TotalPoints", a.total), '; ') INTO v_bad
    FROM (SELECT rr."UserId",
                 SUM(rr."FinalPoints") - SUM(rr."PenaltyPoints") AS total,
                 SUM(CASE WHEN rr."WasAbsent" THEN 1 ELSE 0 END) AS absences
            FROM "RoundParticipantResults" rr
           WHERE rr."SeasonId" = v_season_id
           GROUP BY rr."UserId") a
    JOIN "Standings" s ON s."UserId" = a."UserId" AND s."SeasonId" = v_season_id
    JOIN "Users" u     ON u."Id" = s."UserId"
    WHERE s."TotalPoints" <> a.total OR s."AbsenceCount" <> a.absences;
    IF v_bad IS NOT NULL THEN
        RAISE EXCEPTION 'D. Standings disagree with the round results: %', v_bad;
    END IF;
    RAISE NOTICE 'D. standings match a from-scratch recomputation of the round results.';

    -- --- E. Absence ladder (AbsenceService.PenaltyFor: 3rd/4th -20, 5th out) -
    SELECT string_agg(format('%s #%s -> %s pts', u."Name", ab."AbsenceNumber", ab."PenaltyPoints"), '; ') INTO v_bad
    FROM "Absences" ab
    JOIN "Rounds" ro ON ro."Id" = ab."RoundId"
    JOIN "Users" u   ON u."Id"  = ab."UserId"
    WHERE ro."SeasonId" = v_season_id
      AND ab."PenaltyPoints" <> CASE WHEN ab."AbsenceNumber" IN (3, 4) THEN 20 ELSE 0 END;
    IF v_bad IS NOT NULL THEN
        RAISE EXCEPTION 'E. Absence penalties off the 3rd/4th=-20 ladder: %', v_bad;
    END IF;

    FOR r IN
        SELECT u."Name", count(*) AS absences, sum(ab."PenaltyPoints") AS penalty,
               bool_or(gu."IsEliminated") AS eliminated
          FROM "Absences" ab
          JOIN "Rounds" ro     ON ro."Id" = ab."RoundId"
          JOIN "Users" u       ON u."Id"  = ab."UserId"
          JOIN "GroupUsers" gu ON gu."UserId" = ab."UserId" AND gu."GroupId" = v_group_id
         WHERE ro."SeasonId" = v_season_id
         GROUP BY u."Name" ORDER BY count(*) DESC, u."Name"
    LOOP
        RAISE NOTICE 'E. % - % absence(s), % penalty pts%',
            rpad(r."Name", 18), r.absences, r.penalty,
            CASE WHEN r.eliminated THEN ', ELIMINATED' ELSE '' END;
    END LOOP;

    SELECT string_agg(u."Name", ', ') INTO v_bad
    FROM "GroupUsers" gu
    JOIN "Users" u ON u."Id" = gu."UserId"
    WHERE gu."GroupId" = v_group_id
      AND gu."IsEliminated" <> EXISTS (
            SELECT 1 FROM "Absences" ab JOIN "Rounds" ro ON ro."Id" = ab."RoundId"
             WHERE ab."UserId" = gu."UserId" AND ro."SeasonId" = v_season_id AND ab."AbsenceNumber" >= 5);
    IF v_bad IS NOT NULL THEN
        RAISE EXCEPTION 'E. IsEliminated does not match "has a 5th absence" for: %', v_bad;
    END IF;

    -- --- F. Elimination actually stopped participation ----------------------
    -- ActiveParticipants excludes eliminated members, so an eliminated user must
    -- have no result AFTER the round that eliminated them.
    SELECT string_agg(format('%s eliminated at R%s but has results up to R%s', u."Name", e.elim_round, e.last_round), '; ')
      INTO v_bad
    FROM (
        SELECT ab."UserId",
               MIN(ro."Number") FILTER (WHERE ab."AbsenceNumber" >= 5) AS elim_round,
               (SELECT MAX(r2."Number") FROM "RoundParticipantResults" rr
                  JOIN "Rounds" r2 ON r2."Id" = rr."RoundId"
                 WHERE rr."UserId" = ab."UserId" AND r2."SeasonId" = v_season_id) AS last_round
          FROM "Absences" ab JOIN "Rounds" ro ON ro."Id" = ab."RoundId"
         WHERE ro."SeasonId" = v_season_id
         GROUP BY ab."UserId"
        HAVING MIN(ro."Number") FILTER (WHERE ab."AbsenceNumber" >= 5) IS NOT NULL) e
    JOIN "Users" u ON u."Id" = e."UserId"
    WHERE e.last_round > e.elim_round;
    IF v_bad IS NOT NULL THEN
        RAISE EXCEPTION 'F. Eliminated participants kept being scored: %', v_bad;
    END IF;
    RAISE NOTICE 'F. eliminated participants stop being scored at their elimination round.';

    -- --- G. Flavio landed exactly once, on the intended round ---------------
    SELECT string_agg(format('R%s %s %s->%s', ro."Number", u."Name", rr."GrossPoints", rr."FinalPoints"), '; ' ORDER BY ro."Number")
      INTO v_bad
    FROM "RoundParticipantResults" rr
    JOIN "Rounds" ro ON ro."Id" = rr."RoundId"
    JOIN "Users" u   ON u."Id"  = rr."UserId"
    WHERE rr."SeasonId" = v_season_id AND rr."FlavioRuleApplied";
    IF v_bad IS NULL THEN
        RAISE EXCEPTION 'G. The Flavio rule never fired. Expected it on round %.', v_flavio_round;
    END IF;
    RAISE NOTICE 'G. Flavio penalties: %', v_bad;

    SELECT string_agg(DISTINCT 'R' || ro."Number", ', ') INTO v_bad
    FROM "RoundParticipantResults" rr
    JOIN "Rounds" ro ON ro."Id" = rr."RoundId"
    WHERE rr."SeasonId" = v_season_id AND rr."FlavioRuleApplied" AND ro."Number" <> v_flavio_round;
    IF v_bad IS NOT NULL THEN
        RAISE EXCEPTION 'G. Flavio fired outside the intended round %: %', v_flavio_round, v_bad;
    END IF;

    SELECT string_agg(format('%s %s->%s', u."Name", rr."GrossPoints", rr."FinalPoints"), '; ') INTO v_bad
    FROM "RoundParticipantResults" rr
    JOIN "Users" u ON u."Id" = rr."UserId"
    WHERE rr."SeasonId" = v_season_id AND rr."FlavioRuleApplied"
      AND rr."FinalPoints" <> rr."GrossPoints" / 2;
    IF v_bad IS NOT NULL THEN
        RAISE EXCEPTION 'G. Flavio penalty is not floor(gross/2) for: %', v_bad;
    END IF;

    -- --- H. ...and the DEADLINE is what triggered it ------------------------
    -- Transcribes FlavioRuleService.ComputeSpecialDeadline. This is the direct
    -- check on the historical-timestamp trap: seeding PublishedAt = now() against
    -- historical kickoffs would make EVERY round >= 16 late.
    SELECT string_agg('R' || x."Number", ', ' ORDER BY x."Number") INTO v_bad
    FROM (
        SELECT ro."Number",
               (MAX(p."SubmittedAt") > LEAST(
                    ro."PublishedAt" + CASE WHEN ro."FirstMatchStartsAt" - ro."PublishedAt" < interval '24 hours'
                                            THEN interval '12 hours' ELSE interval '24 hours' END,
                    ro."FirstMatchStartsAt")) AS late
          FROM "Rounds" ro JOIN "Predictions" p ON p."RoundId" = ro."Id"
         WHERE ro."SeasonId" = v_season_id AND ro."Number" >= 16 AND ro."Status" = 'Scored'
         GROUP BY ro."Number", ro."PublishedAt", ro."FirstMatchStartsAt") x
    WHERE x.late <> (x."Number" = v_flavio_round);
    IF v_bad IS NOT NULL THEN
        RAISE EXCEPTION 'H. Late-submission flag is wrong on: % (expected late only on R%).', v_bad, v_flavio_round;
    END IF;
    RAISE NOTICE 'H. submissions are late on R% only, as intended.', v_flavio_round;

    -- --- I. The penalised user really led going in --------------------------
    -- Fails if the rounds were scored out of order, or via
    -- POST /seasons/{id}/recalculate (which reads a stale Standings snapshot).
    SELECT string_agg(u."Name", ', ') INTO v_bad
    FROM "Users" u
    WHERE u."Id" IN (
        SELECT rr."UserId" FROM "RoundParticipantResults" rr JOIN "Rounds" ro ON ro."Id" = rr."RoundId"
         WHERE rr."SeasonId" = v_season_id AND rr."FlavioRuleApplied" AND ro."Number" = v_flavio_round)
      AND u."Id" NOT IN (
        SELECT b."UserId" FROM (
            SELECT rr."UserId", SUM(rr."FinalPoints") - SUM(rr."PenaltyPoints") AS pts
              FROM "RoundParticipantResults" rr JOIN "Rounds" ro ON ro."Id" = rr."RoundId"
             WHERE rr."SeasonId" = v_season_id AND ro."Number" < v_flavio_round
             GROUP BY rr."UserId") b
         WHERE b.pts = (SELECT MAX(pts) FROM (
            SELECT SUM(rr."FinalPoints") - SUM(rr."PenaltyPoints") AS pts
              FROM "RoundParticipantResults" rr JOIN "Rounds" ro ON ro."Id" = rr."RoundId"
             WHERE rr."SeasonId" = v_season_id AND ro."Number" < v_flavio_round
             GROUP BY rr."UserId") c));
    IF v_bad IS NOT NULL THEN
        RAISE EXCEPTION 'I. Flavio penalised % but they were not leading before R% -- rounds were scored out of order, or via recalculate.', v_bad, v_flavio_round;
    END IF;
    RAISE NOTICE 'I. the penalised participant was the leader before R%.', v_flavio_round;

    -- --- Final table --------------------------------------------------------
    FOR r IN
        SELECT s."Position", u."Name", s."TotalPoints", s."PlayedRounds",
               s."AbsenceCount", s."PenaltyPoints", s."ExactCount", gu."IsEliminated"
          FROM "Standings" s
          JOIN "Users" u       ON u."Id" = s."UserId"
          JOIN "GroupUsers" gu ON gu."UserId" = s."UserId" AND gu."GroupId" = v_group_id
         WHERE s."SeasonId" = v_season_id
         ORDER BY s."Position"
    LOOP
        RAISE NOTICE '  %  %  % pts  (% played, % abs, -% pen, % exact)%',
            lpad(r."Position"::text, 2), rpad(r."Name", 18), lpad(r."TotalPoints"::text, 4),
            r."PlayedRounds", r."AbsenceCount", r."PenaltyPoints", r."ExactCount",
            CASE WHEN r."IsEliminated" THEN '  ELIMINATED' ELSE '' END;
    END LOOP;

    RAISE NOTICE 'All assertions passed.';
END $$;
