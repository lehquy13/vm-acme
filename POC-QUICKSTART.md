# Short reproducible verification

## Clean source and two names

Reproduce the captured runtime result from a detached checkout of the verified source. Use a new checkout path and workspace for each run.

```powershell
git clone --no-hardlinks . ../vm-acme-verification
git -C ../vm-acme-verification checkout --detach bf8fc5fbcf5cedc4947b2505edc63f094dbe022e
Set-Location ../vm-acme-verification
git status --porcelain
git rev-parse HEAD
powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/poc-proof.ps1 -Workspace .poc/proof-workspaces-current
```

Prerequisite: the isolated `vm-acme-poc` Minikube/Harbor/PostgreSQL/SQL Server infrastructure is running. For first-time infrastructure setup, use the provisioning commands in [the deployment guide](DEV-DEPLOYMENT.md). The harness creates unique fresh databases and namespaces and preserves existing PoC deployments. It initializes `vm-orders` and `vm-order-service`, builds/tests/renders before committing generated changes, and verifies fresh populated V1-to-V2 upgrades, separate V2 reruns, SQL failure injection, blocked rollout and recovery on both providers. Reports include source/generated SHAs, fingerprints, exact build commands and image digests.

## Real database tests and executable secret-file checks

From the clean source checkout:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/build-bundles.ps1 -Runtime win-x64
powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/test-databases.ps1 -BundleRuntime win-x64 -EvidenceDirectory artifacts/database-proof
```

GitHub uses `linux-x64` and uploads TRX and a source-SHA report. Required database rows cannot be skipped. Runner and bundles load an explicit operator file from an unrelated working directory.

The delivery commit adds the following Kubernetes proof helper. Run it from the updated authoring repository against the generated checkout created above (adjust the absolute clone path):

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/verify-secret-files.ps1 -CloneRoot C:/path/vm-acme-verification/.poc/proof-workspaces-current/vm-orders -Provider postgres
powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/verify-secret-files.ps1 -CloneRoot C:/path/vm-acme-verification/.poc/proof-workspaces-current/vm-orders -Provider sqlserver
```

These checks use separate read-only API/migration files, verify AppSettings and fallback startup, sanitized logs and metrics, and confirm Required mode blocks migration/API rollout when workload identity is unavailable. They do not constitute live Key Vault access verification.

## Initialization fallback

Use separate fresh template copies:

```powershell
./init.ps1 vm-orders
./init.ps1 vm-order-service
```

Bash alternative: `bash ./init.sh vm-orders`. GitHub template creation automatically runs bootstrap; dispatch fallback: Actions > Initialize repository from template > Run workflow. The original repository's immutable ID is excluded, and a committed marker prevents repeat initialization.

See [proof report](docs/review-gap-proof.md), [naming/secret policy](docs/naming-secret-policy.md), and [bootstrap policy](docs/template-hardening.md). Live company vault federation/access verification requires authorized infrastructure.
