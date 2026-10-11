# Local Kubernetes PoC

This delivery targets **Windows 11, Git Bash, PowerShell 5.1 and Docker Desktop with Linux containers/WSL 2**. Run from the repository root. Allow at least 4 CPUs, 8 GB for Docker/WSL and 20 GB disk; the bootstrap runs Harbor, PostgreSQL and SQL Server together. SQL Server Developer edition is for development/testing. Firmware virtualization, WSL feature installation, a pending reboot, Docker's first-launch agreement and a corporate network proxy may require a manual host step. No company infrastructure is used by default.

Minikube and Helm are downloaded to `%LOCALAPPDATA%/vm-acme-poc/bin` if missing. Docker Desktop is installed via winget if absent and started if stopped. Git Bash must already be available to invoke the `.sh` commands; equivalent `.ps1` entrypoints are provided. The scripts pin Minikube 1.39.0, Kubernetes 1.37.0, Helm 3.19.0 and Harbor chart 1.19.2.

```bash
./scripts/poc.sh --provider sqlserver
./scripts/poc.sh --provider postgres
```

These commands install prerequisites, bootstrap isolated infrastructure, build/push/pull separate V1/V2 API and migrator images, deploy V1, and run the verification gates. Bootstrap and application deployment are separate script modules. Every Kubernetes/Helm operation uses the explicit `vm-acme-poc` context. Application namespaces are `vm-acme-poc-sqlserver` and `vm-acme-poc-postgres`; infrastructure and Harbor have separate namespaces. PVCs survive reruns. On an existing V2 database, V1 targeting skips downgrade and verification records the existing migration history.

Expected final output:

```text
UP PASS provider=sqlserver ...
PASS V1 API image deployed
PASS upgrade backfill preserves existing V1 row
PASS repeat deployment applies zero completed migrations
PASS failed migration leaves API rollout unchanged
VERIFY PASS provider=sqlserver gates=... evidence=.../.poc/sqlserver/verify-...
```

Evidence is in `.poc/<provider>/up/` and `.poc/<provider>/verify-<timestamp>/`: image build/push/pull logs, migration counts/history, created row, backfill, emitted metrics, API logs, Pod image IDs, events and `report.json`. `.poc/<provider>/latest-report.txt` points to the newest passing report. Do not commit `.poc`; bootstrap outputs may include operational details. The committed execution status is recorded in `docs/kubernetes-verification.md`.

The verified deployment mechanism is **Helm pre-install/pre-upgrade migration Jobs**. Jobs are retained until the next attempt for logs. The verifier registers a PoC-only pending EF migration that creates a probe table and executes invalid SQL, then checks rollback, history and unchanged API rollout. The failure migration requires both PoC flags. The verifier now registers/logs in a real user and checks Todo description, ID and completion across restart/upgrade. V1 targets the Todo/account migration; V2 targets latest so it applies its pending deployment revision. That immutable revision still modifies historical WorkItems; separate legacy SQL fixtures verify its backfill/default. Current verification status and source fingerprints are recorded in docs/kubernetes-verification.md; local disposable PostgreSQL/SQL Server tests verify schema, relationships, timestamps, ownership and concurrency. Decimal, baseline-adoption, drift and SSDT timing comparisons remain outside these gates.

## Access

The publishing engine is the **isolated Minikube node's Docker daemon**, controlled by the Windows host via Minikube's authenticated Docker connection. It pushes to the actual `<minikube-node-IP>:30002` Harbor endpoint, which kubelet uses for pulls. Host loopback is never used as a Pod registry address. Host browser access is a separate explicit tunnel:

```powershell
$env:PATH="$env:LOCALAPPDATA\vm-acme-poc\bin;$env:PATH"
minikube -p vm-acme-poc kubectl -- -n vm-acme-poc-postgres port-forward svc/vm-acme 18080:8080
# http://127.0.0.1:18080/health, /ready, /version, /metrics, /api/todos
minikube -p vm-acme-poc kubectl -- -n vm-acme-poc-registry port-forward svc/harbor 18082:80
# Harbor browser: http://127.0.0.1:18082
```

Harbor username is `admin`; explicit public PoC passwords are defined in `Get-PocCredentials` in `scripts/poc-common.ps1` and the database bootstrap ConfigMap. Scripts never print them. API and migrator use separate database users, secrets and service accounts. Local Harbor intentionally uses HTTP restricted to this isolated PoC; node trust is configured explicitly. Company credentials are never loaded.

For an existing **explicitly authorized** Harbor, supply `POC_REGISTRY_ENDPOINT` (`host:port`), `POC_REGISTRY_PROJECT`, `POC_REGISTRY_USERNAME`, `POC_REGISTRY_PASSWORD`, and `POC_REGISTRY_AUTHORIZED=true` as environment inputs. HTTPS is default; optionally provide `POC_REGISTRY_CA` with a PEM CA path trusted by Windows and the node. The project must already exist. HTTP requires `POC_REGISTRY_HTTP=true`. Endpoint DNS and routing must work from the publishing engine and nodes; failed login/push/pull is a gate failure. Do not set these inputs to shared infrastructure without authorization. External credentials should be read from a secret store, not shell history.

## Upgrade, rerun, troubleshoot, cleanup

```bash
./scripts/poc-up.sh --provider postgres       # preserves DB/PVC; rebuilds release artifacts
./scripts/poc-verify.sh --provider postgres   # upgrades V1 to V2 and exercises retry/failure/recovery; verifier registers/logs in a real user
./scripts/poc-cleanup.sh --provider postgres  # removes app/Job, retains database and Harbor
./scripts/poc-cleanup.sh --provider postgres --reset # explicit destructive reset of this provider only
./scripts/poc-cleanup.sh --registry --reset   # explicit registry/image/PVC deletion
./scripts/poc-cleanup.sh --stop               # stops profile; preserves all data
```

For SQL Server substitute `sqlserver`. Resetting one provider leaves the other provider's database intact. Cleanup checks the explicit context and PoC ownership labels. Complete cluster deletion, including all local PVC data: `minikube delete -p vm-acme-poc` (explicit destructive action).

On failure, inspect `.poc/native-failure.txt`, provider evidence/events, and `minikube -p vm-acme-poc kubectl -- get pods -A`. `ImagePullBackOff` means endpoint/project/credentials/trust must be corrected; `Pending` usually means insufficient CPU/RAM or unavailable storage. `vm-acme-migrate` logs show only sanitized exception types and migration IDs; inspect database logs with the appropriate provider administrator when detailed errors are needed. Ready checks query the real database. Database waits, Helm Jobs and endpoint checks have bounded timeouts. Bootstrap downloads and image publishing require internet access to GitHub, Helm, Harbor chart, Docker Hub, Microsoft image registry and NuGet.
