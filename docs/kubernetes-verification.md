# Historical Kubernetes verification

Superseded by [clean-commit review-gap proof](review-gap-proof.md), verified from source bf8fc5fbcf5cedc4947b2505edc63f094dbe022e. The following working-tree results are retained only as historical evidence.

# Kubernetes verification — 11 October 2026


## Current Todo/authentication hardening verification

Both provider commands were rebuilt and rerun against the current working tree on 11 October 2026:

- PostgreSQL: PASS, 27 gates, `.poc/postgres/verify-20261011-224613/report.json`; tracked copy [postgres.json](evidence/template-hardening/postgres.json).
- SQL Server: PASS, 27 gates, `.poc/sqlserver/verify-20261011-224909/report.json`; tracked copy [sqlserver.json](evidence/template-hardening/sqlserver.json).
- Build baseline SHA: `c0290c28b470bcfe9cd035bd405a3cec445b3589`; product source was dirty. These reports verify the hardening working tree, not the unchanged baseline commit.
- Exact build-input SHA256 fingerprint: `303486d26aedc35cca109c091310dcfb5d66f2be5b7a5e43c8c1a8d9c838d0c6` for both providers. The verifier rejects mismatched source fingerprints.
- API digest: `sha256:eb0864aea20ede9d45729f2506eb5974b52755827c2f251c4131355e617432e0`.
- Migrator digest: `sha256:59e6ed18646db0609152dfd6f3dd00619a677e4525fae2f63ba009f53d38167a`.

The current verifier registers/logs in real users and exercises owner-scoped Todos, persistence across restart/upgrade, completion, invalid input, idempotent migration reruns, pending SQL failure/rollback, unchanged rollout and separate API/migrator identities. Both retained existing V2 databases (`schemaUpgradeExecuted: false`); no new fresh schema-upgrade claim is made. Fresh database schema and both self-contained Windows bundles were separately exercised by disposable dual-engine tests.

Rider build: PASS. Latest disposable database suite: 69 passed, zero failed/skipped (11 unit, 53 integration, 5 architecture); all eight required live/staged provider rows confirmed by TRX. Missing/skipped TRX coverage was separately rejected by the guard probe. Helm lint/render/server dry-run: PASS for both providers and both orchestration modes, including Key Vault identity inputs. Linux self-contained bundles built for both providers and launched in runtime-deps containers; PostgreSQL also launched as nonroot with a read-only filesystem and writable /tmp. A runtime KeyVault-mode invalid-URI probe failed with the SecretBootstrap validation message, proving the bundle invokes the provider factory's runtime secret bootstrap.

The original runtime-only migrator image failed its Kubernetes hook because Infrastructure requires the ASP.NET shared framework; the default migrator base was corrected before these passing runs. A source-fingerprint rejection during hardening correctly forced rebuilding changed inputs.

Live company Key Vault federation/access, approved FIPS images/certification, exact E28 acceptance, branch-protection required-check settings, external TLS/registry/ingress and real Argo execution remain external validation requirements. See [deployment policy](template-hardening.md).

## Historical WorkItem verification

The sections below describe earlier runs and do not establish current Todo/authentication coverage.

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

## Initialized-name deployment verification

Full copies of this repository were initialized through Bash with `Orders` and PowerShell with `Order_Service`. Both actual restore/build runs passed with zero warnings/errors. Both renamed API/migrator images were built and published to the isolated Harbor registry and deployed into `vm-acme-poc-namecheck-orders` and `vm-acme-poc-namecheck-order-service` in the existing Minikube profile. These smoke checks used the existing PoC PostgreSQL database and credentials, not company infrastructure.

Both migration Jobs completed idempotently, APIs became Ready, and registration/login/authenticated Todo access passed. The generated `poc-common.ps1` deployment/migration-log helpers were then executed against the same isolated profile; renamed chart/image/Deployment/Job references all worked. Reports: [Orders](evidence/template-hardening/vm-orders.json), [Order_Service](evidence/template-hardening/vm-order-service.json). Long slugs and trailing underscores were rejected; names containing Acme/acme also passed isolated replacement checks.

These two extra smoke namespaces and images are retained for inspection. To remove only these smoke resources after review, use explicit context commands `kubectl --context vm-acme-poc delete namespace vm-acme-poc-namecheck-orders vm-acme-poc-namecheck-order-service`. Original provider/infrastructure namespaces and data are separate.
## Provider bundle image verification

Both Docker targets `bundle-postgresql` and `bundle-sqlserver` built and published successfully. Both images launched as nonroot with a read-only filesystem and writable /tmp, then connected to their existing real PoC databases and confirmed the migration history was already up to date. No schema changes were applied in these image checks. Reports: [PostgreSQL bundle](evidence/template-hardening/bundle-postgresql.json), [SQL Server bundle](evidence/template-hardening/bundle-sqlserver.json).

- PostgreSQL bundle digest: `sha256:68460c76b36bafbc9cd59b20da92bfe69e84a43b9665aa1ebd6d67a3899cb694`.
- SQL Server bundle digest: `sha256:cd263d8a0edcdadbdd02a95cb9975c3b65df4dfd5e88dd280d397228e5bf4192`.

These use public runtime-deps images for local verification; they do not establish FIPS compliance. Their real database runs use PoC Environment-mode secrets. Key Vault runtime bootstrap is covered by the fail-closed bundle probe and application configuration tests; live cloud secret access still requires company inputs.