# Accounts and groups

Global accounts, per-group membership and approval, tokens, and how a group stays isolated from
every other one.

## Accounts, sign-up and approval

### Initial admin

The development admin (`admin@palpitao.local`, platform admin and GroupAdmin of the default group)
is created in Development only, at startup, in a database with no users — credentials and details
in the [development guide](../development.md#the-development-admin). Any other environment that
still has it with the published password logs an error at every startup until it is changed.

### Public sign-up with per-group approval

New participants **sign up themselves** on the public **`/register`** screen ("Don't have an
account yet? Sign up" link on the login screen) by **choosing which group to join**. Sign-up
**does not grant access automatically** — the group's admin must approve the membership:

1. The user picks an **active group** (active-groups list, see [Groups (multi-tenant)](#groups-multi-tenant)) and provides name, email,
   password and confirmation. Validations: group, name and email required, valid email, matching
   passwords and a strong password (≥ 8 characters with at least **one letter and one number**).
2. `POST /api/auth/register` creates (or reuses) the **global account** as `Role = Participant`,
   `Status = Approved`, **plus a `GroupUser` membership `PendingApproval`** in the chosen group.
   The password is stored with a **BCrypt hash**; **no token** is issued — only the success message.
3. The **group's** admin sees the requests in **/admin/registration-requests** and can **approve**
   or **reject** (optional reason) — only for their own group ([Groups (multi-tenant)](#groups-multi-tenant)).
4. Once the **membership** is approved, the user logs in and enters the group. While they have no
   approved group, login succeeds but lands on the "waiting for approval" message.

The admin can also create participants directly in **/admin/participants** (membership already
approved and active). Creating a group and switching between groups are covered in **[Groups (multi-tenant)](#groups-multi-tenant)**.

### Tokens (JWT access + refresh)

`POST /api/auth/login` returns an **access token** (JWT Bearer, `Jwt:ExpiresHours`, default **12h**)
and a **refresh token** (`Jwt:RefreshTokenDays`, default **30 days**), plus the user object.

- **Refresh:** `POST /api/auth/refresh` exchanges a valid refresh token for a **new access token and
  a rotated refresh token** (the old one is invalidated). The frontend's HTTP interceptor does this
  transparently on a `401`, retrying the original request **once**; a second `401` ends the session.
- **Logout:** `POST /api/auth/logout` **revokes** the refresh token (idempotent for unknown/expired
  ones). Refresh tokens are stored **hashed** (`RefreshToken` entity), never in plain text.
- Public sign-up / create-group return no token (the user logs in afterwards).

### User status (`UserStatus`)

`UserStatus` is the **account-level** login gate. With groups, approval moved to the **membership**
(`GroupUser.Status`, [Groups (multi-tenant)](#groups-multi-tenant)): public sign-up now creates an **`Approved`** account plus a
`PendingApproval` group membership — so `UserStatus.PendingApproval` is mostly legacy/pre-groups.

| Status | IsActive | Can log in? | Origin |
|---|---|---|---|
| `PendingApproval` | false | ❌ | legacy (pre-groups); new sign-ups are `Approved` at the account level |
| `Approved` | true | ✅ | public sign-up, create-group admin, or admin-created |
| `Rejected` | false | ❌ | rejected at the account level (with `RejectionReason`) |
| `Inactive` | false | ❌ | account deactivated |

**Authentication requires `Status = Approved` **and** `IsActive = true`** at the account level;
**access to a group** additionally requires an **`Approved` `GroupUser`** membership ([Groups (multi-tenant)](#groups-multi-tenant)). A blocked
attempt shows a friendly message and is recorded in the `AuditLog` as `LoginBlocked`. The
`RegistrationSubmitted`, `RegistrationApproved` and `RegistrationRejected` events are also audited.

### Messages in PT/EN

All messages (sign-up success and login blocks) are resolved by the backend according to the
`Accept-Language` header Angular sends (see [Languages (Portuguese / English)](../development.md#languages-portuguese--english)). Examples:

| Situation | Portuguese | English |
|---|---|---|
| Sign-up submitted | "Cadastro enviado com sucesso. Aguarde a aprovação do administrador para acessar o sistema." | "Registration submitted successfully. Please wait for admin approval before accessing the system." |
| Login pending | "Seu cadastro ainda está pendente de aprovação." | "Your registration is still pending approval." |
| Login rejected | "Seu cadastro foi rejeitado. Entre em contato com o administrador." | "Your registration was rejected. Please contact the administrator." |
| Login inactive | "Sua conta está inativa. Entre em contato com o administrador." | "Your account is inactive. Please contact the administrator." |

### Test the flow manually

1. Go to `/register`, sign up a user (e.g. `john@example.com` / `Pass1234`) → see the sign-up submitted message.
2. Try to log in as them at `/login` → login **blocked** with "still pending".
3. Logged in as admin, go to **/admin/registration-requests** → **approve** John.
4. Log in as John → now he **gets in** as a participant.
5. (Optional) Sign up another user and **reject** with a reason → login blocked with "was rejected".

## Groups (multi-tenant)

The system is **multi-group**: each **group** is an independent pool (e.g. _Palpitão England
2025/2026_, _World Cup_, _Friends Group_) with its own administrators, participants, rounds,
matches, predictions, standings, access requests, OCR imports and auditing. **Data never crosses
between groups.**

### What groups are

- **`Group`** is the tenant: `Name`, `Slug` (unique), `Description?`, `OwnerUserId`, `IsActive`.
- **`GroupUser`** is the user↔group link, with `Role` (`GroupAdmin`/`Participant`) and `Status`
  (`PendingApproval`/`Approved`/`Rejected`/`Inactive`). Unique per `(GroupId, UserId)`.
- **`User`** is the **global** identity (email/password); roles are **per group**. The one platform-wide
  role is the super-admin (`UserRole.Admin`), who acts as group admin in any existing group.

### Create a group

1. On the login screen, click **Create a group** (`/create-group`).
2. Enter the group name + the administrator's name/email/password.
3. The backend creates the `User`, the `Group` (slug generated from the name) and a `GroupUser`
   `GroupAdmin/Approved`. Log in and you land on the group's `/admin`.

### Request access to a group

1. In **Sign up** (`/register`), pick the **desired group** (list of active groups via
   `GET /public/groups`).
2. The global account (approved) and a `GroupUser` **`PendingApproval`** in the group are created.
3. On login, while there is no approved group, it shows _"wait for the group administrator's
   approval"_.

### Per-group approval

- The admin sees only the requests **of their group** in `/admin/registration-requests` and
  approves/rejects only those (operations by `groupUserId`). Everything is audited with `GroupId`.

### Login and switching groups

- After authenticating, the frontend calls `GET /auth/my-groups` (approved **and active**): **0** →
  the **`/pending`** screen (awaiting-approval, lists pending/rejected/**deactivated** memberships with
  re-check + logout); **1** → enter directly; **several** → `/select-group` screen.
- The current group is kept in `localStorage` and shown in the header, with a **Switch group** button.
- A member **deactivated** in a group (`GroupUser.IsActive=false`) is **blocked** from it: `my-groups`
  hides it and `CurrentGroupService` returns 403 `group.membershipInactive` (SuperAdmin bypasses).

### How the frontend sends the group / how the backend validates it

- The `group.interceptor` injects the **`X-Group-Id`** header on every authenticated call.
- The `CurrentGroupService` (backend) reads that header, **validates** that the user has an
  `Approved` **and active** `GroupUser` in the group and exposes `GroupId`/`Role`. The `[RequireGroupAdmin]` /
  `[RequireGroupParticipant]` filters protect the controllers. A missing/invalid header or no access
  ⇒ **HTTP 403**. The frontend is **never** trusted alone — every endpoint revalidates the group.

### `GroupId` propagation (modeling decision)

To isolate with lean migrations, the `GroupId` column exists only on the **tenant roots** —
`Season`, `Round` (denormalized from Season), `Standing`, `RoundParticipantResult`,
`SeasonScoringConfig`, `OcrParticipantAlias`, `AuditLog` and `GroupUser`. The first six implement
`IGroupOwned`, which is what puts them under the EF Core global query filter and the insert stamping;
`GroupUser` (membership lookups span groups) and `AuditLog` (entries outside a group stay global)
carry a `GroupId` without the filter. The per-round entities (`Prediction`, `PredictionScore`, `Absence`, `AbsenceOverride`,
`RoundMatch`, `Ocr*`) **derive the group from the parent** (`Round`/`Season`), and every query
validates that the round/season belongs to the current group. The **roster** of a round (who scores/
is absent) comes from the **group membership** (`GroupQueries.ActiveParticipants`), not the global
role. **`Team`** remains a **global** catalog of real clubs.

### Migrating existing data

The `AddGroupsAndTenancy` migration creates the tables, gives a default `GroupId` to the current
rows pointing at a seeded **default group** — _Palpitão England 2025/2026_
(`palpitao-england-2025-2026`) — and links the seeded admin as `GroupAdmin/Approved` of that group.

### Multi-group security rules

- A group's admin **never** sees/manages another group's data.
- A participant only accesses a group where `GroupUser.Status = Approved`; pending/rejected/inactive
  is blocked.
- The header's `GroupId` is always revalidated in the backend; relevant actions go to the `AuditLog`
  with `GroupId`; Sentry receives the `group_id` tag. An audit entry whose caller names no group
  takes the one `CurrentGroupService` already **validated** for the request (never the raw header),
  so it shows in that group's `GET /api/admin/audit`; outside a group request (login, background
  jobs) it stays global (`GroupId` null).

### Current limitations

- Per-group **`IsActive`** and **`IsEliminated`** live on **`GroupUser`**: elimination and per-bolão
  deactivation are scoped to the group (scoring/standings read the per-group flags). `User.IsActive`
  remains the **global account** login gate.
- Creating a group via the public screen requires a **new** email (an existing user creating another
  group is left for later). `AdminSentryController` (diagnostics) still uses the global role.

### Test the isolation manually

1. Create 2 groups with different admins (`/create-group`).
2. In each, create a round/matches. Confirm one admin does **not** see the other's rounds.
3. Force another group's `X-Group-Id` on an authenticated call (DevTools) → response **403**.
4. Sign up a participant (`/register`) in one group and confirm they only appear in **that** group's
   requests.
