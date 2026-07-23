-- ---------------------------------------------------------------------------
-- Create a group and make an existing user its GroupAdmin.
--
-- Why this exists: after a full reset with drop_all_groups=true, the kept
-- super-admin has no group and the app has no in-app "create group" for an
-- already-logged-in user (the public /create-group flow makes a NEW account).
-- This seeds the first group for the kept admin so they can log straight in.
--
-- Enums are stored as strings ("GroupAdmin"/"Approved"); timestamps are
-- timestamptz; GroupUsers.IsActive/IsEliminated default true/false but are set
-- explicitly here. Mirrors exactly what AuthService.CreateGroup writes.
--
-- Idempotent & safe (one atomic DO block):
--   * Aborts (rollback) if the admin email is not found.
--   * Does nothing if that admin already has ANY group membership.
--   * Aborts if the slug is already taken.
--
-- Edit v_admin_email / v_group_name / v_slug for a different group.
-- ---------------------------------------------------------------------------

DO $$
DECLARE
    v_admin_email text := 'admin@palpitao.local';
    v_group_name  text := 'Palpitao England 2026/2027';
    v_slug        text := 'palpitao-england-2026-2027';
    v_admin_id    uuid;
    v_group_id    uuid;
    v_now         timestamptz := now();
BEGIN
    SELECT "Id" INTO v_admin_id FROM "Users" WHERE "Email" = v_admin_email;
    IF v_admin_id IS NULL THEN
        RAISE EXCEPTION 'Admin % not found; aborting (no group created).', v_admin_email;
    END IF;

    IF EXISTS (SELECT 1 FROM "GroupUsers" WHERE "UserId" = v_admin_id) THEN
        RAISE NOTICE 'Admin % already has a group membership; nothing created.', v_admin_email;
        RETURN;
    END IF;
    IF EXISTS (SELECT 1 FROM "Groups" WHERE "Slug" = v_slug) THEN
        RAISE EXCEPTION 'A group with slug "%" already exists; aborting.', v_slug;
    END IF;

    v_group_id := gen_random_uuid();

    INSERT INTO "Groups"
        ("Id", "Name", "Slug", "Description", "CreatedByUserId", "OwnerUserId", "IsActive", "CreatedAt", "UpdatedAt")
    VALUES
        (v_group_id, v_group_name, v_slug, NULL, v_admin_id, v_admin_id, true, v_now, v_now);

    INSERT INTO "GroupUsers"
        ("Id", "GroupId", "UserId", "Role", "Status", "IsActive", "IsEliminated", "ApprovedAt", "CreatedAt", "UpdatedAt")
    VALUES
        (gen_random_uuid(), v_group_id, v_admin_id, 'GroupAdmin', 'Approved', true, false, v_now, v_now, v_now);

    RAISE NOTICE 'Created group "%" (id %, slug %) with % as GroupAdmin.', v_group_name, v_group_id, v_slug, v_admin_email;
END $$;
