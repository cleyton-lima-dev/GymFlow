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
- One Production Machine was reported: `48ed314fe04118` in `gru`. Private
  `fly proxy` to its 6PN address and `GET /health` returned 200 on 2026-11-13.
  This proves private reachability of the current image, not of a replacement.
- Gate, migration, and in-process cutover tests passed locally. The gate commit
  `94fbfd8` is not published. No Production Platform/MFA or Admin state is
  established by this runbook.

The Fly docs say that an app without services has no public Fly Proxy route,
and Fly staff advise removing the service section and redeploying. This is
configuration intent, **not proof** that the installed flyctl will reconcile
the already persisted Machine's `config.services` to `[]` or that a replacement
will lack services. The first Production change is blocked until an authorized
rehearsal on an already existing non-Production app demonstrates those exact
effects with the installed flyctl version. Inspect the effective Machine config
after every deployment, not just the TOML or app-level service listing.

## Preconditions, before closing ingress

1. New authorization for the Production window; verified target GymId and
   designated active Admin UserId. Confirm Production PlatformOperator, MFA,
   Platform JWT/MFA configuration, database privileges and a reviewed backup.
   Never print secret values. If any are absent, NO-GO.
2. Prepare an immutable image built from an isolated, reviewed source tree;
   never build from this dirty workspace. Record the current and candidate image
   digests. The candidate must contain the maintenance commit and migration.
   Confirm in that exact release image that `StudentImportCleanup:Enabled=false`
   is implemented and effective in both private and protected phases; the
   approved maintenance commit alone does not implement this switch. Check
   that no higher-precedence secret overrides the TOML value, without viewing
   secret values. Otherwise NO-GO before migration.
3. Review both TOML files against the effective, **sanitized** Fly Machine
   config. Preserve any required settings not represented locally. The private
   config must yield zero public service entries. Confirm the existing process
   group, release-command setting, and health-check behavior; these files define
   no `[processes]` or release command, and private service checks disappear
   with the public service. `--skip-release-command` prevents a configured
   release command, while Fly's default smoke checks may still run. Resolve
   any difference first.
4. Schedule a full operational pause with Abrahão and obtain his confirmation
   that no administrative or financial operation is in progress before closing
   ingress. The owner accepts that a request or job still in progress may be
   interrupted despite the pause and wait below; do not treat the available
   metrics as proof that every operation completed. Keep a verified backup and
   a way to reconcile any interrupted operation before reopening access.
5. Confirm the single-Machine topology again immediately before the window.
   If it changes, enumerate every Machine and revise the commands; no wildcard
   action based on the historical ID.

## Future sequence — commands are examples to execute only when authorized

1. Announce the maintenance window. Cordon the confirmed old Machine:

   `fly machine cordon 48ed314fe04118 --app gymflow-api-prod`

   Confirm that public probes from outside no longer reach the API and prevent
   application writes through the private 6PN path during the pause. Cordon
   prevents new proxy routing to that Machine; it is **not** persistent ingress
   policy for replacements. If public ingress remains open, NO-GO.

2. Programmed pause: after public ingress is confirmed closed, wait at least
   15 minutes with no new private writes. Reconfirm with Abrahão that no
   administrative or financial operation is pending. Observe per-Machine
   concurrency, TCP connections and database transactions as supporting
   signals. An idle TCP connection is not an active request; zero connections
   does not prove zero requests; zero transactions does not prove that an HTTP
   operation or job has finished. If an active request, relevant transaction or
   job is known to persist, or Abrahão cannot confirm the pause, remain closed
   and abort before migration. The residual risk of unobserved work remains
   accepted by the owner; do not describe this wait as complete drainage.

3. Convert the *existing image* to a private config before migration. The
   command form is documented, but the current image digest and an approved
   private-config reconciliation are prerequisites:

   `fly deploy . --app gymflow-api-prod --config fly.issue131.private.toml --image <CURRENT_IMAGE_DIGEST> --strategy rolling --ha=false --update-only --no-public-ips --skip-release-command`

   Do not use `bluegreen` or `canary`; these can add Machines. `--ha=false`
   suppresses a spare, `--update-only` only prevents creation for new process
   groups, and `--no-public-ips` only prevents new IP allocation. Rolling may
   replace the Machine; none of these flags guarantees zero transient Machines
   or zero incremental cost. Do not proceed if the authorized rehearsal cannot
   establish acceptable Machine count and cost behavior. The flags also do not
   prove the reconciled Machine has no services. Verify every resulting
   Machine's effective `config.services` is `[]`, that the app service
   list has no public mapping, that neither public IP routes to the API, and
   that private `/health` still works. IP allocations must remain unchanged.
   Any unexpected public service is an immediate STOP/NO-GO; do not migrate.

   After confirming private-only routing, stop the old API Machine and verify
   it stays stopped before migration:

   `fly machine stop <CURRENT_PRIVATE_MACHINE_ID> --app gymflow-api-prod --timeout 120`

   Request the normal Machine shutdown and confirm it has fully stopped before
   migration. `--timeout 120` is a CLI wait, not a 120-second application grace
   period; the current `fly.toml` sets no `kill_timeout` (Fly default: 5 seconds).
   Confirm through approved read-only database views that no relevant
   transaction or session from the old API remains. If shutdown fails, the
   Machine restarts, or relevant transactions/sessions cannot be ruled out,
   remain closed and abort before migration. Use the current ID returned by
   the private deployment; it may differ from the historical ID. Stopping can
   interrupt an unobserved request or background job; reconcile its outcome
   before reopening access. Affected operations may include more than workout
   history.

4. With ingress still closed and the old API stopped, execute the
   separately reviewed migration against the verified Production database.
   Do not put migrations in a Fly release command. Confirm schema and existing
   data integrity before starting the new API.

5. Deploy the candidate immutable image with the same private config:

   `fly deploy . --app gymflow-api-prod --config fly.issue131.private.toml --image <CANDIDATE_IMAGE_DIGEST> --strategy rolling --ha=false --update-only --no-public-ips --skip-release-command`

   First inspect every resulting Machine's image and `config.services`
   **before** starting a stopped Machine. If it remained stopped after deploy,
   start only the verified private Machine using the documented `fly machine
   start` operation. A stopped Machine is not assumed to restart merely
   because a deploy completed. Then repeat the private-reachability checks.
   Open a tunnel bound only to local loopback:

   `fly proxy 18080:8080 --app gymflow-api-prod --select`

   Through that private Fly 6PN tunnel only, perform the official
   PlatformOperator login with MFA and PUT `/api/platform/gyms/{gymId}/maintenance`
   with the verified
   `AllowedAdminUserId`. Confirm active state and audit. Do not put passwords,
   MFA codes, JWTs, or invitation links in command lines, files, or logs.
   Confirm that the candidate image actually honors
   `StudentImportCleanup:Enabled=false` and that the job remains inactive;
   a TOML value alone is insufficient. If this cannot be confirmed, keep
   ingress closed and do not proceed to protected/public operation.

6. Before the protected deploy, confirm the owner-approved AC Power Gym GymId
   already in the TOML matches the active maintenance row and designated Admin
   UserId. Do not infer tenant identity from the timezone key. If the GymId,
   maintenance state or Admin does not match, NO-GO. Deploy with the same
   immutable candidate image only after this confirmation:

   `fly deploy . --app gymflow-api-prod --config fly.issue131.protected.toml --image <CANDIDATE_IMAGE_DIGEST> --strategy rolling --ha=false --update-only --no-public-ips --skip-release-command`

   `Maintenance:RequiredAtStartup` must be effective
   **before** `app.Run()`; if maintenance/Admin validation fails, startup must
   fail. Confirm every resulting Machine has the intended service and startup
   guard, then verify public Student rejection, designated Admin access, and
   unaffected tenants. `/health` alone does not certify these conditions.

7. Final release requires explicit owner approval; 48 hours do not trigger
   automatic reopening. While maintenance is still
   active, close ingress again and deploy the private config with the same
   candidate image, removing `RequiredAtStartup` safely. Confirm private-only
   state, deactivate maintenance through the official audited endpoint, then
   deploy the reviewed normal public config with the approved immutable image
   and independently reviewed settings. Confirm HTTP/tenant behavior and
   unchanged IP allocations. Never deactivate maintenance while a public
   Machine still requires it at startup: a restart would fail closed.

## Abort and rollback boundary

- Before migration: keep ingress closed; restore the prior image/config only
  after confirming the old schema is intact and public routing is deliberate.
- After migration and especially after financial writes: keep the private or
  protected maintenance state. Do not automatically run migration `Down` or
  restore an old database snapshot over new records. Repair forward or use a
  separately approved recovery plan.
- Any replacement with public `config.services` before maintenance is active,
  loss of 6PN access, unresolved active work, missing Platform/MFA state, or
  mismatch of image/GymId/IPs, unexpected Machine creation/cost, or an effective
  StudentImportCleanup job during the protected phase is NO-GO. Preserve closure
  and diagnose; never uncordon merely to recover convenience.

## One remaining material validation

In a separately authorized window, rehearse a no-service deploy using the
**installed flyctl version** on an already existing non-Production Fly app.
Verify both an updated Machine and any replacement have `config.services=[]`,
public routing is absent, private 6PN access survives, and the prior public
service can be restored without changing IP allocations. Record peak and final
Machine counts and any incremental cost. If no suitable
existing app is available, the Production cutover remains NO-GO.

## Sources

- Fly app config: https://fly.io/docs/reference/configuration/
- Fly app services and 6PN: https://fly.io/docs/networking/app-services/
- Fly deploy CLI: https://fly.io/docs/flyctl/deploy/
- Fly Machine stop CLI: https://fly.io/docs/flyctl/machine-stop/
- Fly proxy CLI: https://docs.fly.io/flyctl/cmd/fly_proxy
- Fly staff on removing services and redeploying:
  https://community.fly.io/t/how-can-i-remove-existing-app-from-public-network/11393
- Fly staff on reading effective Machine config:
  https://community.fly.io/t/secure-read-only-access-to-raw-fly-machine-configuration-without-exposing-credentials/29058
