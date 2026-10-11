# Template deployment policy

## Names

Initializers accept a repository slug made of letter or digit segments separated by hyphens or underscores, up to 40 characters after removing one leading `vm-`. Hyphens join PascalCase .NET segments and underscores remain in .NET identifiers. Kubernetes deployment names use lowercase hyphenated slugs with one `vm-` prefix; environment and metric names use underscore separators. Example: `vm-order-service` produces .NET `VmOrderService`, deployment `vm-order-service`, test variables `VM_ORDER_SERVICE_TEST_*` and metric prefix `vm_order_service`. `Order_Service` produces .NET `VmOrder_Service` with the same deployment/environment/metric forms. Both local wrappers preserve their PowerShell/Bash entrypoints and exclude Git, IDE, generated output, local PoC state and CodeGraph index data. `scripts/test-template-naming.ps1` and `.sh` check matching naming cases and invalid inputs.

The template bootstrap workflow is guarded by the source repository's immutable GitHub repository ID. It runs the existing initializer, then dotnet test and Helm lint, and commits only after all succeed. `.template-initialized` makes a completed bootstrap idempotent. The workflow listens for pushes and `workflow_dispatch`; GitHub documents the push and manual triggers but not a dedicated template-repository-created event, so automatic execution must be confirmed in an actual generated repository. If the first push does not start it, enable Actions and dispatch it manually from the default branch.

## Secret access

Local development: run `scripts/setup-local.ps1`; the randomly generated JWT key is saved outside the repository in .NET user secrets. For Compose supply `VM_ACME_LOCAL_JWT_KEY` independently. Disposable database tests generate test passwords at runtime. No tracked development configuration contains signing keys.

Company deployment: `values-dev.yaml` requires `secrets.mode=KeyVaultRequired`. Supply `secrets.vaultUri`, `application.vaultConnectionSecret`, `application.vaultJwtSecret` and a distinct `migration.vaultConnectionSecret`. These are names, never secret values. API/migrator use their separate Kubernetes service accounts with Azure workload identity federation and pod label `azure.workload.identity/use: "true"`. Set `azure.workload.identity/client-id` annotations to the approved distinct identity IDs. The webhook injects the projected token and Azure environment variables; automatic Kubernetes API token mounting remains disabled.

Use `WorkloadIdentityCredential` and Key Vault `get` access only to the exact names each identity needs. No vault listing or developer credential fallback is used. Startup reads have a 30-second deadline and bounded transport retries. In Required mode, missing settings, failed access or empty values abort startup. Explicit KeyVaultWithFallback mode selects a complete validated settings snapshot instead; see [naming and secret policy](naming-secret-policy.md). Migrator requests only its own connection secret. Secrets are snapshots per process; restart Pods/Jobs after rotation. Authorize network access and least-privilege vault/DB grants separately. Live federation, vault access, TLS and rotation behavior need validation in the company environment.

## Persistence and telemetry

Missing provider fails registration. Production/Staging cannot select InMemory. Development/Testing must explicitly select it. Relational API contexts use a pooled factory, scoped repositories and up to three provider transient retries with a five-second maximum delay; callers must use the EF execution strategy around any future explicit transaction.

OpenTelemetry exports ASP.NET Core traces/metrics, Npgsql/SqlClient command traces and `VmAcme.Database` command duration histograms to OTLP. Metrics tags contain only provider, operation and error type; they never contain SQL or credentials. Configure the collector through standard `OTEL_EXPORTER_OTLP_*` settings. The sample `/metrics` endpoint is retained for PoC compatibility and has separate process/MediatR counters. Collector delivery and dashboards require external validation.

## CI and migration artifacts

The `Database integration tests / database-tests` workflow runs disposable PostgreSQL and SQL Server and validates all LiveDatabaseTests/StagedMigrationTests TRX rows. Missing, skipped or failed required rows fail the job. Configure this job as a required status in repository branch protection; that setting is external to the checkout.

`scripts/build-bundles.ps1 -Runtime linux-x64` builds separate self-contained EF 10.0.12 bundles under ignored `artifacts/bundles/`. Runtime credentials are supplied through configuration, with the same workload-identity secret bootstrap in the provider factories. Do not put connections on command lines or bake them into images. Company dev sets migration.artifact=Bundle. Build its provider image with docker build --target bundle-postgresql or --target bundle-sqlserver; both contain a self-contained Linux EF bundle and retrieve their named Key Vault connection at runtime. Bundle mode rejects runner target/failure settings. The existing Helm PoC runner remains necessary for its target/count/failure injection protocol. Exact E28 packaging/compliance acceptance still needs the company contract.

Docker supports `SDK_BASE`, `API_BASE`, `MIGRATOR_BASE` and `BUNDLE_BASE` build arguments for approved images. Defaults remain public .NET images for local PoC. A build argument does not establish FIPS certification: the approved immutable references, runtime/OS compatibility, crypto configuration and required certification evidence have not been supplied.

## Evidence

PoC configs/report JSON record source commit, product dirty state and SHA256 fingerprint of build inputs. The verifier rejects images whose recorded source fingerprint differs from current source. Reports record API/migrator image IDs and gate evidence. Earlier WorkItem results do not validate the Todo/authentication release; current results must be recorded separately.

Bundle Jobs set DOTNET_BUNDLE_EXTRACT_BASE_DIR=/tmp/.net so self-contained extraction works with nonroot execution and a read-only root filesystem; /tmp is mounted writable.
