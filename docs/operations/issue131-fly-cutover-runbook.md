# Issue #131 — Fly.io ingress cutover (PREPARED, NO-GO)

This is a future runbook, not permission to operate Production. Nothing in it
has been executed against Production. Use the already allocated public IPs;
never release or reallocate them. The original `fly.toml` is unchanged.
The planned maintenance window is 48 hours (2 days). The 15-minute technical
wait below applies only to stopping the old API; it is not the maintenance
duration. General users remain blocked while the designated Admin configures
the ERP, imports remaining students and validates records. Access is reopened
only with explicit owner authorization, even if 48 hours have elapsed.

## Prepared configurations and known evidence

- `fly.issue131.private.toml`: existing app and runtime settings, but no
  `[http_service]` or `[[services]]`; Platform enabled and the committed
  StudentLifecycle job disabled. It also requests StudentImportCleanup disabled,
  which works only if the release image contains the corresponding code. There
  is no startup maintenance requirement, because the PlatformOperator must
  first activate it through the private API.
- `fly.issue131.protected.toml`: original public 80/443 service plus
  `Maintenance__RequiredAtStartup__0` set to the owner-confirmed AC Power Gym
  GymId. Startup fails closed unless that tenant has active maintenance with
  the authorized Admin.
- One Production Machine was confirmed: `48ed314fe04118`, started in `gru`,
  shared-cpu-1x / 512 MB. Its effective configuration has one public service
  on 80/443, autostart enabled, autostop disabled, one check, restart on-failure,
  process group `app`, and Fly Platform v2 metadata. Private
  `fly proxy` to its 6PN address and `GET /health` returned 200 on 2026-11-13.
  This proves private reachability of the current image, not of the candidate.
- Gate, migration, and in-process cutover tests passed locally. The gate commit
  `94fbfd8` is included in the candidate image already built in the registry.
  No Production Platform/MFA or Admin state is established by this runbook.

The planned cutover uses a direct Machines API update of the **same Machine ID**,
not `fly deploy rolling`. The API requires the complete effective Machine config:
copy it from a fresh GET in protected operator memory, preserve every existing
field, and change only the fields explicitly required for each phase. The TOMLs
describe intended state; neither is a complete Machines API payload. Explicit
`config.services=[]` removes public Fly Proxy routing while retaining the app's
allocated IPs, subject to verification of the effective config and external
probes. These API steps have **not** been exercised on this Production Machine.

## Preconditions, before closing ingress

1. New authorization for the Production window; verified target GymId and
   designated active Admin UserId. Confirm the reviewed Production provisioner
   from commit `43307a3` is available, an authorized SQL credential has
   sufficient privileges for the separately reviewed migrations and the
   first-operator insert, and the owner can recover the Platform MFA encryption
   key and a separate Platform JWT key. Do not require or assume an existing
   Production PlatformOperator before migrations. Confirm a fresh, verified
   pre-cutover backup of the existing Supabase PostgreSQL at the cutover point.
   Preserve Users, Students, GymId, credentials, workouts and history; do not
   create a parallel database or restore over new writes. Never print secret
   values. If any prerequisite is absent, NO-GO.
2. Use the already built immutable candidate image; do not rebuild from this
   dirty workspace. Record and verify the current and candidate image references
   and digests. The candidate must contain the maintenance commit and migration.
   Confirm from the exact image build provenance that the
   `StudentImportCleanup:Enabled=false` switch is implemented and configured
   for both private and protected phases; the approved maintenance commit alone
   does not implement it. Check that no higher-precedence secret overrides the
   TOML value, without viewing secret values. If image provenance or planned
   configuration is uncertain, STOP before migration. Confirm the effective
   runtime setting after private start and before protected/public opening.
3. Obtain a fresh, private GET of the effective Machine config and `instance_id`
   immediately before each update. Preserve `env`, `init`, `guest`, `metadata`,
   `restart`, image, process settings and every other effective field, including
   mounts/files if present. The observed config has 10 env entries, including
   `FLY_PROCESS_GROUP` and `PRIMARY_REGION`; the TOMLs alone omit effective
   fields. Do not print or persist the raw config, lease nonce or secret values.
   Use fresh `instance_id` as `current_version`, never the historical value.
   Resolve any missing field, image reference, check or precedence difference
   before changing the Machine. Service-linked checks cease with `services=[]`;
   use a private `/health` probe after explicit start.
4. Schedule a full operational pause with Abrahão and obtain his confirmation
   that no administrative or financial operation is in progress before closing
   ingress. The owner accepts that a request or job still in progress may be
   interrupted despite the pause and wait below; do not treat the available
   metrics as proof that every operation completed. Keep a verified backup and
   a way to reconcile any interrupted operation before reopening access.
5. Confirm the single-Machine topology again immediately before the window.
   If it changes, enumerate every Machine and revise the commands; no wildcard
   action based on the historical ID.

## Future sequence — planned Machines API operations, not yet proven in Production

1. Announce the maintenance window. Confirm the current Machine ID and acquire
   an exclusive Machine lease **before** cordon. Keep its nonce private, track
   expiry, and renew it before the remaining lifetime becomes too short for
   the next operation. The lease holder must supply `fly-machine-lease-nonce`
   on applicable Machine API operations, including update and release; use
   `current_version` from a fresh GET for each update. A failed/expired lease,
   stale version or rejected operation is STOP/NO-GO; never retry a mutation
   blindly. The lease may prevent proxy autostart while active, but propagation
   is not instantaneous and neither lease nor cordon proves isolation alone.
   Cordon the confirmed old Machine through the authorized API operation;
   the historical CLI form below identifies the target only:

   `fly machine cordon 48ed314fe04118 --app gymflow-api-prod`

   Confirm from outside that probes on the actual public hostname and allocated
   IPv4/IPv6 no longer reach the API. Observe Machine events for proxy starts
   and prevent application writes through private 6PN during the pause. Do not
   assume an immediate proxy effect from `cordoned=true`. If public ingress
   remains open, the lease lapses, or a proxy start occurs, STOP/NO-GO.

2. Programmed pause: after public ingress is confirmed closed, wait at least
   15 minutes with no new private writes, maintaining the lease throughout.
   Reconfirm with Abrahão that no
   administrative or financial operation is pending. Observe per-Machine
   concurrency, TCP connections and database transactions as supporting
   signals. An idle TCP connection is not an active request; zero connections
   does not prove zero requests; zero transactions does not prove that an HTTP
   operation or job has finished. If an active request, relevant transaction or
   job is known to persist, or Abrahão cannot confirm the pause, remain closed
   and abort before migration. A fresh verified backup must already exist.
   The residual risk of unobserved work remains accepted by the owner; do not
   describe this wait as complete drainage or as the 48-hour maintenance window.

3. With the lease valid and public probes failing, stop the old API Machine
   through the Machines API, supplying the nonce when required. Wait for the
   exact instance to report `stopped`; inspect subsequent events for proxy
   autostart. `fly machine stop --timeout 120`, if used in a future procedure,
   means seconds before SIGKILL; `--wait-timeout` is the separate CLI wait for
   the stopped state. Neither the stop response nor `cordoned=true` proves
   isolation. Stopping may interrupt an unseen HTTP request or job; reconcile
   any uncertain operation before reopening.

   While still stopped, GET the same Machine again. Preserve the **complete**
   effective config and current image, set only `config.services=[]`, and
   update the same ID with `skip_launch=true`, fresh `current_version` and the
   valid lease nonce. Do not use the private TOML as an API payload. GET again:
   require the same logical Machine ID, `stopped`, expected old image,
   `services=[]`, and all unrelated config fields unchanged. Confirm no other
   Machine appeared, both allocated public IPs remain assigned to the app,
   external public probes cannot reach the API, and no proxy start event or
   relevant database transaction/session or write remains. Any failed check,
   unexpected restart, lease expiry or ambiguous update response is STOP/NO-GO
   **before migration**. Do not repeat update on an uncertain result.

4. Only after the old API is stopped, `services=[]` is confirmed and public
   probes fail, configure `Platform__MfaEncryptionKey` and
   `Platform__Jwt__Key` through a separately reviewed, controlled secret
   procedure. Do not use a command that deploys, starts or restarts the old
   API. Keep both values out of commands, logs, Git and reports. The MFA key
   supplied later to the provisioner must be exactly the one effective in the
   candidate API; the Platform JWT key must be different. If secret staging
   can alter the Machine, require a fresh GET, event review and external probes
   before continuing. Any Machine change, restart, public route, lease failure
   or ambiguous result is STOP/NO-GO before migration until separately
   reviewed. Reconfirm the same stopped Machine, `services=[]`, unchanged IPs
   and no relevant writes before proceeding.

5. With ingress still closed, `services=[]`, the old API stopped and no
   relevant write/transaction remaining, execute the
   separately reviewed migration against the verified Production database.
   Do not put migrations in a Fly release command. Confirm schema and existing
   data integrity before starting the new API. Keep the lease valid until the
   private configuration is verified; if that cannot be assured, pause before
   migration.

6. After the migrations and data integrity are verified, run the published
   Production provisioner in its explicit Production mode, with its fixed
   Project Ref and required interactive human confirmations. Supply the
   authorized SQL credential and the same owner-held MFA encryption key staged
   for the candidate API; do not expose values in command lines, files or logs.
   The provisioner must reject an unexpected database or migration baseline
   and an existing operator. Confirm exactly one active PlatformOperator after
   its committed insert, without assuming one existed beforehand. If
   provisioning, identity, key continuity or the one-active-operator check is
   uncertain, keep ingress closed and STOP/NO-GO before starting the candidate
   or activating maintenance.

7. GET the stopped Machine again and, through the Machines API, update the
   **same ID** with the already built candidate image and reviewed private
   environment gates: `Platform__Enabled=true`,
   `StudentLifecycle__Enabled=false` and
   `StudentImportCleanup__Enabled=false`. Preserve the complete effective
   config and every unrelated field, retain `services=[]`, and use
   `skip_launch=true`, fresh `current_version` and valid lease nonce. GET again
   to confirm `stopped`, candidate image digest, `services=[]`, the reviewed
   private gate values, unchanged unrelated fields, no new Machine and
   unchanged app IP allocations. Only then start that Machine
   explicitly using the authorized API operation and nonce if required. Do
   not release the lease until the no-service state and private path are
   verified; coordinate release before a future public service relies on
   proxy autostart. A direct update has not been proven on this Production
   Machine and does not itself guarantee zero transient resource cost.

   Repeat private-reachability checks. While the Machine is stopped for the
   migration, private access is unavailable. After its explicit start, open
   a tunnel bound only to local loopback:

   `fly proxy 18080:8080 --app gymflow-api-prod --select`

   Confirm the candidate started privately with `Platform__MfaEncryptionKey`
   and `Platform__Jwt__Key` effective, distinct and without a
   higher-precedence override. Use the same owner-held MFA key supplied to the
   provisioner; the private MFA login below is the end-to-end check of key
   continuity. If the keys are missing, mismatched or uncertain, keep ingress
   closed and STOP/NO-GO.

8. Through that private Fly 6PN tunnel only, perform the official
   PlatformOperator login with MFA. Only after it succeeds, PUT
   `/api/platform/gyms/{gymId}/maintenance` with the verified
   `AllowedAdminUserId`. Confirm active state, designated active Admin and
   audit. Confirm the gate's private behavior for Student, Professor and
   unauthorized Admin access before public exposure. Do not put passwords,
   MFA codes, JWTs, encryption keys or invitation links in command lines,
   files or logs. Confirm that the candidate image actually honors
   `StudentImportCleanup:Enabled=false` and that the job remains inactive;
   a TOML value alone is insufficient. If any check fails, keep ingress closed
   and do not proceed to protected/public operation.

9. Before restoring public service, confirm the owner-approved AC Power Gym GymId
   already in the TOML matches the active maintenance row and designated Admin
   UserId. Do not infer tenant identity from the timezone key. The protected
   TOML is a reference, not a complete API payload. On the same Machine,
   prepare the full effective config with the reviewed 80/443 service and
   `Maintenance__RequiredAtStartup__0` effective **before** any public
   routing is restored. Retain the verified private Platform, StudentLifecycle
   and StudentImportCleanup settings and effective Platform secrets. The direct
   update may restart the Machine by default:
   use an explicitly reviewed stopped/update/start sequence, with fresh
   version and lease handling, and prove the startup gate through private 6PN
   before enabling public routing. If maintenance/Admin validation fails,
   startup must fail closed. Verify the resulting service, gate, IPs, public
   Student/Professor/unauthorized Admin rejection, designated Admin access,
   and unaffected tenants; `/health` alone is insufficient. Any mismatch is
   NO-GO for protected opening. Only in this protected public phase can the
   designated AC Power Gym Admin use the ERP normally during the planned
   48-hour window; `services=[]` provides only private operator access.

10. Final general release requires explicit owner approval; 48 hours do not
   trigger automatic reopening. While maintenance is still active, close
   ingress again and verify no services before removing `RequiredAtStartup`.
   Deactivate maintenance only through the private official audited endpoint,
   then restore the reviewed normal public service through a separate,
   protected Machines API transition. Confirm HTTP/tenant behavior and
   unchanged IP allocations. Never deactivate maintenance while a public
   Machine still requires it at startup: a restart would fail closed.

## Abort and rollback boundary

- Before migration: if lease, cordon, stop, update or secret staging has an
  uncertain outcome, keep ingress closed and inspect Machine state, events and
  effective config.
  Do not blindly retry mutations or release/expire the lease while a stopped
  Machine still advertises public services. Restore the prior image/config only
  after confirming the old schema is intact and public routing is deliberate.
- After migration, a failed provisioner, missing or mismatched Platform key,
  failed private MFA login or unverified maintenance gate keeps ingress closed.
  Especially after financial writes, keep the private or protected maintenance
  state. Do not automatically run migration `Down` or restore an old database
  snapshot over new records. Repair forward or use a separately approved
  recovery plan.
- Any public response before intended reopening, proxy start after stop,
  failed/expired lease, stale `current_version`, `config.services` mismatch,
  relevant new write, loss of 6PN after candidate start, unresolved active work,
  missing Platform/MFA state, image/GymId/IP mismatch, unexpected Machine or
  cost, or an effective StudentImportCleanup job during the protected phase is
  NO-GO. Preserve closure and diagnose; never uncordon merely for convenience.

## Remaining Production-only validation

No authorized Production transition has established that the lease blocks proxy
autostart promptly, that cordon propagation completes before stop, or that the
same-ID update with `skip_launch=true` leaves `services=[]` without transient
public exposure, Machine replacement/cost or IP changes. These are mandatory
observations during a separately authorized window, with STOP before migration
on any failure. The later protected reopening also needs a proven ordering of
startup guard and public service registration; do not infer safety from the
TOML or a passing `/health`. Until authorization and these checkpoints pass,
the cutover remains NO-GO.

## Sources

- Fly app config: https://fly.io/docs/reference/configuration/
- Fly app services and 6PN: https://fly.io/docs/networking/app-services/
- Fly deploy CLI: https://fly.io/docs/flyctl/deploy/
- Fly Machine stop CLI: https://fly.io/docs/flyctl/machine-stop/
- Fly Machines API update, lease, cordon: https://fly.io/docs/machines/api/machines-resource/
- Fly proxy CLI: https://docs.fly.io/flyctl/cmd/fly_proxy
- Fly report on delayed cordon/autostart propagation:
  https://community.fly.io/t/stopped-machine-with-autostart-false-and-cordoned-is-started-by-fly-proxy/28720
- Fly staff on active Machine lease and proxy autostart:
  https://community.fly.io/t/how-to-honour-restart-attempt-limit/22774/8
