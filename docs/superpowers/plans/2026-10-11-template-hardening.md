# Template Hardening Implementation Plan

> For agentic workers: use executing-plans inline with Luna-only focused Cavecrew helpers. User selected autopilot.

**Goal:** Resolve the six review findings and provide honest current deployment evidence.
**Architecture:** Separate naming, persistence policy, secret bootstrap, CI and deployment evidence. Existing provider migrations and PoC failure injection remain supported.
**Spec:** ../specs/2026-10-11-template-hardening-design.md

## Global constraints

- Preserve docs/notes.md user changes. No invented company inputs or compliance claims.
- Luna only for subagents; CodeGraph before indexed source reads; Rider for builds.
- Generate local credentials outside Git; company secrets use separate identities.

## Tasks

- [x] Initializers: init.sh/init.ps1 parity, naming validation and contextual token replacement; prove Orders and Order_Service generated paths.
- [x] Persistence: DependencyInjection.cs takes environment name, requires provider, restricts InMemory; update host/factory/settings. Test missing/invalid providers and environment restrictions before implementation; use pooled factory with bounded retries for relational providers.
- [x] Secrets: shared named-secret bootstrap in Infrastructure, API/migrator integration and Helm mode configuration; local user-secret setup. Verify no configured credentials leak and vault mode fails closed.
- [x] CI: test-databases.ps1 emits/validates TRX; databases workflow runs both providers and rejects skipped tests. Run disposable engines locally.
- [x] Observability/build: OpenTelemetry host instrumentation, configurable approved base image inputs and provider EF bundle packaging. Verify build and document certification boundaries.
- [x] Evidence: rebuild PoC images, report source revision/dirty state and both image IDs; execute both providers, update quickstart/dev/report docs.
- [x] Final Luna diff review and focused corrective checks.

## Review focus

Underscore/long names; Production InMemory; Key Vault missing URI/identity/secret and stale fallback; missing/skipped database rows; evidence referencing images built from another source state.

## Ledger

Autopilot design and plan decisions recorded before implementation. Company FIPS/E28 specifics are absent; external compliance validation remains conditional.

Verified: Rider builds passed; focused secret/persistence tests 9 passed; telemetry tests 2 passed. Disposable full suite 69 tests passed (11 unit, 53 integration, 5 architecture), zero skips; strict TRX check confirmed all 8 required provider rows. Windows self-contained bundles applied to fresh PostgreSQL/SQL Server; Linux bundles built and PostgreSQL launched on runtime-deps without installed .NET.
Ruling: public default migrator base must be ASP.NET because Infrastructure references its shared framework; original runtime-only base failed the Kubernetes hook. Approved FIPS base images remain external inputs.
Ruling: preserve sample /metrics endpoint for PoC compatibility; standard HTTP/runtime/database telemetry uses OTLP. SQL query/connection tags are stripped before export.
Ruling: Kubernetes fingerprint rejection after build-input changes is expected; rebuild all artifacts before accepting reports.

Verified: PostgreSQL and SQL Server current Kubernetes commands PASS 27 gates each; identical product fingerprint303486d26aedc35cca109c091310dcfb5d66f2be5b7a5e43c8c1a8d9c838d0c6 and recorded API/migrator digests. Both report schemaUpgradeExecuted=false on retained V2 data. Tracked reports saved under docs/evidence/template-hardening.
Final: Ruling: reviewer requested a runtime Key Vault wrapper for bundles. Declined: Linux runtime bundle with KeyVault mode and invalid vault URI exited with SecretBootstrap's URI validation error, proving factory bootstrap executes at runtime. Cost if wrong: company bundle cannot obtain its connection; live company access remains an external validation requirement.

Verified: Orders/Bash and Order_Service/PowerShell full-copy restore/build and actual isolated deployment PASS; generated deployment helpers PASS; local development signing key generated in user secrets outside Git. Smoke namespaces/images retained for review.

Verified: both self-contained provider Docker images built/published; nonroot read-only runs succeeded against real existing PoC databases, applying zero migrations. Reports and image digests saved under docs/evidence/template-hardening.
Integration: preserve dev workspace changes for user review; no push/merge. User docs/notes.md staged/unstaged edits preserved.
