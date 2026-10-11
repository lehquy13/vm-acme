# Kubernetes delivery implementation plan

**Goal:** Execute an isolated Minikube PoC for PostgreSQL and SQL Server; deliver a reusable, statically validated company dev profile.

**Authority:** The user's detailed delivery request is the specification and explicitly authorizes autonomous prerequisite installation and execution. Additional approval pauses in planning skills are superseded by that instruction. Existing workspace changes are retained; no shared infrastructure is targeted.

**Architecture:** One application chart, separate common/dev/PoC values. Infrastructure bootstrap precedes application Helm hooks. Separate service accounts, connection secrets and images for API and migrator. Helm pre-install/pre-upgrade Jobs gate rollout; optional Argo CD PreSync annotations are provided but not claimed as exercised. Harbor and both databases live in PoC-owned namespaces with PVCs. The isolated Minikube Docker daemon is the image build/publishing engine, controlled by the host; its registry endpoint is also reachable by node image pulls. Windows browser access uses explicit port forwarding.

**Tech stack:** .NET 10, EF Core provider migration assemblies, Minikube Docker driver, Helm 3, Harbor, PostgreSQL, SQL Server. PowerShell orchestration with executable Bash entry points on Windows Git Bash; documented host requirements.

## Tasks

- [x] Inspect host, install missing CLIs, start isolated Minikube. Capture errors if runtime/network is unavailable.
- [x] Package API and migrator as separate image targets. Migrator reads secrets from environment, supports forward-only target migrations, emits applied/pending history and sanitized failure diagnostics.
- [x] Add provider-specific V2 additive schema/backfill, compatible with V1 API. Add version/readiness/Prometheus metrics endpoints and structured request logs.
- [x] Implement shared chart: resources/replicas/ingress/TLS, separate identities and secrets, Helm/Argo migration ordering, dev input validation, PoC values without company dependencies.
- [x] Implement bounded, idempotent infrastructure/bootstrap, image publishing, application up and verification scripts. Preserve PVCs by default; destructive cleanup requires explicit reset and targets owned namespaces/profile only.
- [x] Run both providers: V1 CRUD, pod restart persistence, V2 with existing row/backfill, repeat migration history unchanged, actual SQL failure blocks new API image, recover, logs/metrics and node pulls. Store evidence without credentials.
- [x] Helm lint/render + Kubernetes server dry-run dev manifests with synthetic input names; document exact inputs and unexecuted company/Argo deployment.
- [x] Focused Luna review and fixes; deliver POC-QUICKSTART.md and DEV-DEPLOYMENT.md with actual verification status.

## Review focus

Registry hostname must be node-reachable; never equate host loopback with node loopback. Never include secrets in commands/log evidence. Existing V2 databases must not be downgraded during rerun. Hook prerequisites exist before hooks. Failed migrations leave API pod-template and migration history unchanged. All external waits have timeouts. Cleanup cannot target another context/profile/namespace.

## Execution ledger

Host: Windows 11 Home, .NET 10.0.204, Docker Desktop 27.5.1 + WSL 2. Docker engine initially stopped; started successfully. Missing Minikube/Helm installed per-user. No CodeGraph index exists, so skipped per repository instruction.

Prerequisites, image publishing and fresh V1 → V2 Kubernetes verification completed for both providers. Full .NET suite: 21/21 pass; solution build: zero warnings/errors. Dev: four lint/render/admission cases pass on Minikube only. Luna review found a pre-EF failure injection coverage gap; replaced with an explicitly gated pending EF migration and verified SQL Server schema rollback. Final convenience-command reruns pass for both providers: 26 gates each, including an actual pending EF migration failure and schema rollback, API rollout blocking and recovery. Existing rows and V2 schema preserved. See docs/kubernetes-verification.md for exact evidence paths. Windows native-process handling, Docker registry trust, EF 14-digit IDs and port-forward ownership issues were diagnosed and fixed; no data reset was performed.
