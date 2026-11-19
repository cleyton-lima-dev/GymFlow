# Issue #131 — Production cutover execution log (sanitized)

> Last updated: 2026-11-19 (Brazil time).
> This repository is **public**. This log intentionally omits production project IDs, Machine IDs, account/user IDs, IP addresses, credentials, tokens, TOTP information, backup paths, and personal data.
> This is an operational chronology, **not** authorization to perform any Production action. The live state must be checked before the next mutation. The owner must approve each new operational stage.

## Scope and operational constraints

- Production API: Fly.io. Production PostgreSQL: Supabase. One real gym with existing users and history.
- Owner and gym administrator approved a **48-hour maintenance window**, beginning 2026-11-18. The 15-minute drain interval described in the original runbook was explicitly waived by the owner, with residual risk documented. Forty-eight hours is **not** a deadline that automatically reopens access.
- Preserve existing Users, Students, workouts, execution history, passwords, and tenant boundaries.
- No unreviewed migrations, schema resets, automatic retries of ambiguous mutations, new paid infrastructure without approval, or unsupervised public release.
- The Fly.io application currently has a temporary second Machine due to host capacity constraints; reduce to one when the candidate has passed sufficient acceptance checks.
- Production deployments are manual; Git pushes do not by themselves deploy the API.

## Confirmed execution milestones — 2026-11-18

1. **Pre-cutover backup:** encrypted Production backup created and independently validated (archive integrity, decryptability, pg_restore index, identity and migration baseline). Owner confirmed no intervening writes to warrant another backup. The existing original recovery copy was preserved.
2. **Maintenance isolation:** the then-current API Machine was cordoned, stopped, and updated to have no public services, without releasing application IP allocations. External probes failed to reach the API. Exclusive management lease was acquired and renewed during applicable operations.
3. **Platform secrets:** independent encryption and JWT keys were staged in Fly.io. Their values are not in this repository. Subsequent candidate startup confirmed presence/format; end-to-end MFA login later confirmed encryption-key continuity.
4. **Database migration:** exactly 26 EF Core migrations applied to existing Production PostgreSQL, moving the baseline from **13 to 39**; final migration `20261113003407_AddGymMaintenanceStateAndAudit`. The original offline SQL file contained a UTF-8 BOM and initially failed before the first statement; a byte-identical copy excluding the BOM completed successfully with `psql` exit code 0. The final migration history was verified against the expected 39 IDs.
5. **Legacy-data verification:** 70 Users, 67 Students, 67 Workouts and 422 WorkoutExecutions remained. Existing student identity and tenant associations passed checks, with no orphaned records. The intended identity backfill affected 67 rows. New schema objects, indexes and foreign keys were audited; two automatically truncated identifiers had no collisions.
6. **First PlatformOperator:** created with the official guarded Production provisioner, including password and TOTP confirmation. No previous Production operator existed. Subsequent private login succeeded with HTTP 200 and MFA.
7. **Fly host capacity conflict:** attempted to replace the stopped original Machine using the reviewed candidate image and `skip_launch=true`. Fly returned HTTP 409; Machine events identified `cpu_overcommit_exceeded`, with replacement reverted and old Machine left stopped. The exact reason a launch capacity gate was involved despite `skip_launch=true` was not established. Do **not** retry this replacement blindly or start the old image on the migrated database.
8. **Controlled temporary second Machine:** owner approved one candidate Machine, same application/region/resource size, created stopped and private without public service registration. Candidate image used the previously approved immutable digest; no extra volume or public IP was created. Candidate was started privately, returned `GET /health` HTTP 200 via Fly 6PN and retained `services=[]` and `cordoned=true`. Background services `StudentLifecycle` and `StudentImportCleanup` were confirmed disabled.
9. **Fly credential incident:** a tool diagnostic exposed an existing local CLI authentication bundle. The exposed bundle was selectively revoked, its invalidation independently confirmed, and access restored with a fresh app-scoped short-lived credential stored outside the repository. **No token values were committed**.
10. **Runtime DB privileges:** login initially failed with SQLSTATE 42501 because the app role lacked access to the new Platform tables. An audited transaction added narrowly scoped privileges across 24 newly used runtime tables (61 privilege entries confirmed). No ownership, database CREATE, GRANT OPTION, or superuser rights were granted. Subsequent private Platform MFA login succeeded.
11. **Supabase Data API exposure concern:** inspection found broad grants to Supabase API roles without RLS. Owner verified in the Supabase dashboard that the Data API is **disabled**. Do not enable the Data API without first addressing the grants/RLS exposure; this is a separate hardening task, not part of the immediate Fly ingress cutover.
12. **Tenant maintenance:** through the private Platform endpoint, activated maintenance for the intended existing gym and its designated active Admin. Confirmed `active=true`, version 1, matching Admin, and exactly one activation audit event. No public access was reopened.

## Status observed — 2026-11-19

- Production database: **39/39** expected EF migrations, validated.
- Production PlatformOperator: exists; private password + TOTP login: **HTTP 200**.
- Maintenance for the target gym: **active**, designated Admin only, version 1.
- Candidate API Machine: **started**, `cordoned=true`, `services=[]`; private `/health`: **200**.
- Original API Machine: **stopped**, `cordoned=true`, `services=[]`. It remains a temporary cost/resource liability until safely deleted.
- Public ingress: still intentionally closed.
- Target Admin private real login: **200**; `GET /api/auth/me`: **200**.
- Target Admin `GET /api/plans`: **403** (not a failure of login or maintenance). Its cause is **confirmed**: the tenant-specific `GymFeatures:<target-gym-id>:ErpEnabled` value is absent from the candidate's effective `config.env`; no same-name secret overrides it. `ConfigurationGymErpPolicy` defaults the tenant to ERP disabled, and `ErpEnabledFilter` returns 403.
- Student maintenance-denial behavior in real Production: **not yet tested**; the interactive verification script stopped before the student login. Professor / different Admin checks also remain pending where legitimate credentials are unavailable. Never treat HTTP 401 as evidence of maintenance denial.
- Latest observed runtime access checks did not mutate any data. Management leases expire and must be newly acquired/renewed before future protected mutations.
- The old and candidate Machines must not be confused. Operator must obtain their current IDs from a fresh authorized read, not infer them from this public log.

## Next authorized work and remaining gates (as of 2026-11-19)

1. **Tenant ERP feature flag:** owner explicitly authorized a controlled stop/update/private restart of **only the candidate Machine**, preserving its complete config, approved image, `services=[]`, `cordoned=true`, and all other gates. Add only `GymFeatures__<approved-gym-id>__ErpEnabled=true`. Use fresh Machine GET, lease, current version and `skip_launch=true`. Do not replay on HTTP 409 or ambiguous results; observe Fly's previously encountered `cpu_overcommit_exceeded` risk. Confirm private `/health` after restart.
2. **Production authorization check:** using legitimately held credentials only through a private 6PN tunnel, confirm designated Admin `GET /api/plans` returns 200 after the ERP flag is active, and verify the actual student denial as HTTP 503 / `GYM_MAINTENANCE`. Test other identities if genuinely available. Do not create synthetic Production users, reveal JWTs, or replay unnecessary tests.
3. **Protected public Admin phase:** prior to adding any public Fly services, apply and validate the fail-closed `Maintenance:RequiredAtStartup` gate for the approved tenant, prove that only the specified Admin can work during maintenance, and verify student/professor denial. The protected TOML is a *reference*, not a full Machines API payload. Public opening requires its own owner GO and a controlled state transition.
4. **Remove temporary old Machine as soon as candidate is sufficiently validated** and removal is expressly authorized. Retain the application and allocated public IPs; do not leave two Machines permanently.
5. **ERP operational rollout:** designated gym Admin configures plans, memberships, charges, and any approved student import/dedup within the maintenance window. No generic student release until explicit owner GO, even after 48 hours.
6. **Platform web dashboard:** separate UX/homologation work. Today the ERP entitlement is an immutable startup configuration (`GymFeatures`), not a dynamic control in the visual Platform. Do not introduce a new runtime entitlement system during this Production cutover.
7. **Later security hardening:** before enabling Supabase Data API, review/restrict grants to `anon` and `authenticated` and implement appropriate RLS/API exposure policy. Plan default privileges for future DDL separately.

## Non-negotiable operator rules

- **One action at a time.** Stop on uncertainty, avoid repeated tests/backups/migrations or blind API retries.
- Never put passwords, TOTP secrets, API keys, JWTs, Fly tokens, or private backup file paths in this public repository.
- A lease is a temporary concurrency-control mechanism; it does not itself keep a Machine running or services closed. Validate live state and lease again when resuming.
- No public access, new database writes, app-store release, or deletion of the old Machine merely because this log says a prerequisite passed.
- Keep this log updated as subsequent stages complete, with only sanitized operational facts. The confidential operational identifiers remain outside the public log.
