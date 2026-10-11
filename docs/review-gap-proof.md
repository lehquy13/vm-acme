# Review-gap proof - 2026-10-12

Verified implementation source: `bf8fc5fbcf5cedc4947b2505edc63f094dbe022e`.

Evidence is collected from clean detached source clones and clean initialized commits. Generated changes are built, tested and rendered before their local commit; the deployment verifier refuses dirty generated checkouts or mismatched source fingerprints. Reports record both pre-initialization and generated SHAs. Earlier working-tree reports are historical and do not establish this result.

## Scenario matrix

| Scenario | Status | Evidence |
|---|---|---|
| Actual GitHub template creation: automatic push bootstrap, two private repositories | PASS | `github-template.json`; server-side template `generate` endpoint, actual push runs and generated markers/commits. |
| Original template guard | PASS | Bootstrap job skipped on original numeric repository ID; original source remains unchanged by initialization. |
| workflow_dispatch fallback and subsequent push idempotence | PASS | Dispatch succeeded without a new commit; subsequent push bootstrap left HEAD unchanged. |
| GitHub database CI at the verified source SHA | PASS | `github-ci.json`; 87 tests, zero skips, all eight required real-database rows. |
| Shared secret-policy local tests | PASS | Vault success; outage/403/404/empty handling; Required refusal; incomplete fallback; atomic publication; permitted secret names; sanitized logs/telemetry; path and precedence checks. |
| Linux runner and provider bundles load explicit files from unrelated working directories | PASS | Actual GitHub CI logs and source-SHA artifact. |
| Windows runner and provider bundles | PASS | Clean `2e4d9139b1c8f5015068f9de17e3d794d285f577` checkout, 86 tests, zero skips; separate older platform check, not relabeled as the latest SHA. |
| vm-orders / PostgreSQL fresh populated V1-to-V2 and existing-V2 rerun | PASS | Final clean initialized checkout, 28 fresh gates / 27 rerun gates. |
| vm-orders / SQL Server fresh populated V1-to-V2 and existing-V2 rerun | PASS | Final clean initialized checkout, 28 fresh gates / 27 rerun gates. |
| vm-order-service / PostgreSQL fresh populated V1-to-V2 and existing-V2 rerun | PASS | Final clean initialized checkout, 28 fresh gates / 27 rerun gates. |
| vm-order-service / SQL Server fresh populated V1-to-V2 and existing-V2 rerun | PASS | Final clean initialized checkout, 28 fresh gates / 27 rerun gates. |
| Read-only Kubernetes secret files, sanitized fallback indicator, Required-mode refusal | PASS | Both providers, current clean generated commit; API and runner ready in file/fallback modes; Required fails the migration hook and blocks API creation. |
| Live company Key Vault federation, access grants and secret rotation | BLOCKED | No authorized company vault/identities/federated infrastructure supplied. Injected-reader and missing-identity checks do not constitute live-cloud proof. |
| Actual Argo reconciliation | NOT RUN | Helm hooks were executed; Argo manifests were rendered/server-validated only. |

Fresh scenarios create a new collision-checked database, apply V1, populate both authenticated Todos and the legacy row affected by the upgrade, then apply one pending V2 migration. Rerun scenarios start against the resulting V2 database and explicitly do not claim a fresh upgrade. Both scenario types test reruns, SQL failure injection, unchanged API rollout, transaction rollback and recovery.

The actual GitHub creation tests used template source commits `65e9318b224662100e69dbc43a2751764bca5445` and `2e4d9139b1c8f5015068f9de17e3d794d285f577`. Their generated commits and successful database CI are recorded separately. The final two-name Kubernetes proof and current source database CI use `bf8fc5f`; these distinct executions are not relabeled as one commit.

## Reproduction

Use the exact commands in [POC-QUICKSTART](../POC-QUICKSTART.md). For the captured result, first check out the source SHA above, then run `scripts/poc-proof.ps1` with a new workspace path. The existing `vm-acme-poc` infrastructure is reused; proof databases/namespaces are unique and the original PoC deployments are preserved.

Fingerprint algorithm: SHA256 over ordinal-sorted repository-relative path and SHA256 file-hash rows, excluding Git metadata, local PoC state, graph/editor state and generated build/test artifacts. All four V1/V2 API/migrator release references and registry digests are retained in each deployment report. Node-pulled image IDs are recorded independently.

GitHub template test repositories are private and retained for audit. Their initialization commits, workflow runs and markers are listed in the JSON evidence. No GitHub or cloud tokens, connection strings, signing keys, populated fallback files or raw secret-service payloads are included in the tracked reports.

## Failed attempts and corrections

Earlier attempts failed on initializer parent-path exclusions, subprocess working directories, locally ignored PoC state, metadata assignment after conversion to PSCustomObject, and SQL row-count text. Those attempts are not counted as PASS. Missing workload-identity construction and secret-file policy validation were tightened before the final run. Retained local workspaces/logs identify the failed or interrupted attempts separately.

## Captured reports

[Summary](evidence/clean-commit/proof-summary.json), [GitHub template flow](evidence/clean-commit/github-template.json), [GitHub database CI](evidence/clean-commit/github-ci.json). Each provider/name has separate fresh and rerun JSON reports in the same evidence directory.

## Generated commits and fingerprints

| Name | Clean generated commit | SHA256 build-input fingerprint |
|---|---|---|
| vm-orders | `48ceb57f89e5502835e173546ac932aa99e57ccb` | `c96a480f3538168c5850e93f37d40d0d744b96efd3fa4f2b501819fc95bc6f80` |
| vm-order-service | `e2db2b0e1a9f968cf2f950114e5389531649a42e` | `822709e30e3ca2ccc466190b0cedb489afd036f7fd0336fa752c1f9366388d7c` |


GitHub counts and executable bundle/runner outcomes: [sanitized CI summary](evidence/clean-commit/github-ci-summary.log).
