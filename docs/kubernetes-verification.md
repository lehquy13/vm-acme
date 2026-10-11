# Kubernetes verification — 11 October 2026

## Environment and mechanism

Executed on Windows 11 Home with Docker Desktop 27.5.1 / WSL 2, .NET SDK 10.0.204, Minikube 1.39.0 (Docker driver/runtime), Kubernetes 1.37.0 and Helm 3.19.0. Missing Minikube and Helm were installed per user and Docker Desktop was started. The isolated profile is `vm-acme-poc` (4 CPUs, 7168 MB).

Harbor chart 1.19.2 / Harbor 2.15.2 runs in `vm-acme-poc-registry`. PostgreSQL 17.6 and SQL Server 2022 CU21 run with PVCs in `vm-acme-poc-infra`. Both databases and their separate API/migration users were provisioned automatically. Only explicit public PoC credentials are used.

The Windows host controls the isolated node Docker daemon for image builds and publishing. Harbor's actual node endpoint `192.168.49.2:30002` is used for push, pull and Kubernetes image references. Pod image IDs prove registry digest resolution; browser access uses a separate port-forward. The scripts discover this address rather than hardcoding it.

Application deployment uses **Helm pre-install/pre-upgrade migration Jobs**, after infrastructure, Secrets and the separate identity release are ready. **Argo CD was not installed or exercised.** Its PreSync annotations were rendered and admission checked only.

## Executed checks

- Fresh PostgreSQL V1 → V2: PASS, 26 gates, `.poc/postgres/verify-20261011-160214/report.json`.
- Fresh SQL Server V1 → V2: PASS, 26 gates, `.poc/sqlserver/verify-20261011-160659/report.json`.
- Both fresh runs created and read persisted sample data, restarted API Pods, applied exactly one pending V2 migration, checked backfill and defaults, completed a work item, rejected invalid input, inspected request logs and Prometheus metrics, resolved published API/migrator image digests, and repeated deployment with zero migrations applied.
- The initial failure probe ran invalid SQL before EF migration application. It verified Job/rollout failure but was replaced with a stronger pending EF migration probe. Final rerun evidence below is authoritative for migration rollback.
- Dev manifests: PASS for both providers × Helm/Argo annotation modes, Helm lint/render plus Minikube server-side dry-run. Evidence: `.poc/dev-validation/*.yaml` and `*.dry-run.txt`. Synthetic `validation.invalid` inputs only.
- .NET tests: PASS, 12 unit + 9 integration. Migrator build: PASS with zero warnings/errors.

## Final rerun evidence

SQL Server final convenience-command rerun: PASS, 26 gates, `.poc/sqlserver/verify-20261011-161105/report.json`. Its failure log records `MIGRATIONS_BEFORE=2 PENDING=1` followed by `SqlException`, and the verifier confirms schema rollback. PostgreSQL final convenience-command rerun: PASS, 26 gates, `.poc/postgres/verify-20261011-161320/report.json`; its pending migration fails with `PostgresException` and the probe table is rolled back. Both original fresh-upgrade completed rows remain present (`.poc/<provider>/original-row-preserved.txt`). Both final APIs are Ready on V2 and their successful migration Jobs are retained. Latest passing reports are referenced by `.poc/<provider>/latest-report.txt`. A rerun preserves the V2 database and explicitly reports `schemaUpgradeExecuted: false`; it does not claim another fresh schema upgrade.

The PoC-only EF migration creates a probe table, then issues invalid SQL in the same EF migration transaction. Verification checks a failed Job/database exception, unchanged API template and generation, unchanged migration history, absence of the rolled-back probe table, and successful recovery to V2. It is registered only under both explicit PoC flags; normal provider assemblies do not contain this migration.

## Delivery boundaries

Company dev deployment was **not executed**: authorized company context, registry, database, identity, ingress/TLS and Secret inputs have not been supplied. Workload identity annotations are configurable; actual cloud federation/token authentication is not established by this PoC. No shared infrastructure was modified.

Metrics evidence is the application's emitted Prometheus endpoint, not an installed monitoring stack. The sample upgrade is additive deployment metadata; arbitrary production migration safety is not implied. Data and the running PoC are retained. See [POC quickstart](../POC-QUICKSTART.md) for access and explicit cleanup, and [dev deployment](../DEV-DEPLOYMENT.md) for required company inputs.
