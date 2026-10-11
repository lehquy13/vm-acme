# Short reproducible verification

## Clean source and two names

Run these commands from the original template repository after committing implementation changes. The proof harness refuses a dirty checkout, creates clean initialized commits for `vm-orders` and `vm-order-service`, and preserves the existing PoC.

```powershell
git status --porcelain
git rev-parse HEAD
powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/poc-proof.ps1 -Workspace .poc/proof-workspaces-current
```

Prerequisite: the existing isolated `vm-acme-poc` Minikube/Harbor/PostgreSQL/SQL Server infrastructure is running. The harness creates unique fresh databases and app namespaces; it runs fresh populated V1-to-V2 upgrades, then separate V2 reruns, failure injection, blocked rollout and recovery for both providers and names. Reports include source and generated commit SHAs, input fingerprints and image digests.

## Real database tests and executable secret-file checks

From a clean detached clone of the recorded source SHA:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/build-bundles.ps1 -Runtime win-x64
powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/test-databases.ps1 -BundleRuntime win-x64 -EvidenceDirectory artifacts/database-proof
```

GitHub runs the same commands with `linux-x64` and uploads TRX and a source-SHA report. Required live database rows cannot be skipped. Runner and bundles resolve an explicit operator file from an unrelated working directory.

## Initialization fallback

```powershell
./init.ps1 vm-orders
./init.ps1 vm-order-service
```

Use separate fresh template copies for those commands. Bash fallback: `bash ./init.sh vm-orders`. GitHub template creation uses the automatic bootstrap workflow, with Actions â†’ Initialize repository from template â†’ Run workflow as its dispatch fallback. The immutable original repository ID is excluded.

See [naming/secret policy](docs/naming-secret-policy.md), [bootstrap policy](docs/template-hardening.md), and the current proof report for actual scenario statuses. Live company vault federation requires authorized infrastructure.
