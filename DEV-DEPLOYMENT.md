# Company development deployment

Use `deploy/helm/vm-acme`: shared `values.yaml`, `values-dev.yaml` and a company-owned overlay supplied by the operator. There are no company endpoints, identity IDs or credentials in this repository. The PoC infrastructure manifest and public test credentials must never be deployed here.

## Required inputs

Supply the authorized kube context/namespace, provider (`PostgreSql` or `SqlServer`), registry/project and immutable API/migrator release tags; image pull secret name; external database endpoints/database name; separate API and migrator connection secret names/keys; Key Vault URI and separate API/migrator connection secret names plus API JWT secret name (at least 32 UTF-8 bytes); issuer and audience; API and migration service account names and any federated identity annotations/Pod labels; ingress controller class/host, annotations and TLS secret; replica count and resource requests/limits. For Argo CD also supply its namespace/project, repository URL, Git revision, destination server and overlay path. Restrict deployment access to the target namespace.

The API reads JWT issuer/audience from environment configuration and loads its signing key from the named Key Vault secret before identity registration. Todo endpoints require a valid signed bearer token. Register/login endpoints issue tokens for the example accounts; adapt account policy to the organization. Generate local signing keys outside Git with scripts/setup-local.ps1 or Compose environment input. Structured JSON logs go to stdout for Elastic/Kibana ingestion. OpenTelemetry exports HTTP/runtime/database telemetry to an OTLP collector. The Prometheus-compatible `/metrics` endpoint retains sample process/MediatR counters for PoC compatibility.

Company connections are stored in distinct named Key Vault secrets, each readable only by its intended API or migrator identity. Kubernetes Secret/key wiring remains available for PoC AppSettings mode. API credentials should have only the required table DML rights; migration credentials need schema change privileges and migration history rights. PostgreSQL table/default grants must allow future tables for the API role. SQL Server API credentials must not be schema owner. Require TLS verification for external databases; the PoC `TrustServerCertificate=True` setting is not a dev default.

## Bootstrap and Helm deployment

Complete cluster storage/network/ingress/TLS, registry pull access, external database/schema/user grants, Key Vault secrets, workload identity federation and least-privilege vault access before application deployment. The prerequisite phase of the same chart manages only the two service accounts as a **separate release**, so pre-install Jobs have identities on the first application install:

```bash
helm upgrade --install vm-acme-identities deploy/helm/vm-acme \
  --kube-context "$DEV_CONTEXT" --namespace "$DEV_NAMESPACE" \
  -f deploy/helm/vm-acme/values-dev.yaml -f "$DEV_VALUES" \
  --set phase=prerequisites --wait --timeout 5m

helm lint deploy/helm/vm-acme \
  -f deploy/helm/vm-acme/values-dev.yaml -f "$DEV_VALUES"
helm template vm-acme deploy/helm/vm-acme --namespace "$DEV_NAMESPACE" \
  -f deploy/helm/vm-acme/values-dev.yaml -f "$DEV_VALUES" > dev-rendered.yaml
kubectl --context "$DEV_CONTEXT" apply --dry-run=server -f dev-rendered.yaml

helm upgrade --install vm-acme deploy/helm/vm-acme \
  --kube-context "$DEV_CONTEXT" --namespace "$DEV_NAMESPACE" \
  -f deploy/helm/vm-acme/values-dev.yaml -f "$DEV_VALUES" \
  --set migration.orchestration=helm --wait --timeout 12m
```

The namespace, referenced registry/TLS secrets and named Key Vault secrets must already exist. `DEV_VALUES` supplies actual environment values without committing credentials. Company API and provider bundle images are built using `docker build --target api` and `--target bundle-postgresql` or `--target bundle-sqlserver`, with `--build-arg RELEASE_VERSION=<release>`; CI publishes both once and promotes the same immutable artifacts. Configure migration timeout below the deployment timeout. One release should migrate a given database at a time.

## Identity and orchestration

Configure `application.serviceAccountAnnotations`, `migration.serviceAccountAnnotations`, separate `podLabels` and `podAnnotations` for the company's workload identity webhook. Both accounts disable automatic Kubernetes API token mounting; provider webhooks may inject projected identity tokens. Federated trust, service-account subjects/audiences and cloud roles are external prerequisites. Company values default to KeyVaultRequired mode: supply secrets.vaultUri, application.vaultConnectionSecret, application.vaultJwtSecret and migration.vaultConnectionSecret. WorkloadIdentityCredential fetches only named secrets, failing startup on unavailable or empty secrets; it does not implement PostgreSQL token authentication. If the company requires token-based DB authentication, validate the selected driver/token integration separately before dev rollout.

With `migration.orchestration=helm`, the dedicated Job is a `pre-install,pre-upgrade` hook. Its failure aborts Helm before applying the new Deployment. Existing API Pods continue running; databases are not rolled back automatically. Migrator loads its named Key Vault connection using its own workload identity and reports migration IDs/counts. Successful reruns do not reapply completed migrations. Failed Job logs are retained until the next attempt. Use reviewed compatible migrations; a Helm rollback cannot undo schema changes safely.

For Argo CD set `migration.orchestration=argocd`, substitute the required inputs in `deploy/argocd/application.yaml`, and submit to the authorized Argo instance. The migration Job uses **PreSync**, wave `-10`; API resources sync afterward at wave `0`. Bootstrap service accounts/secrets outside this Application first. Helm annotations are omitted in Argo mode. Use a full Application sync to run PreSync; selective resource sync can bypass hooks. Failed hooks must be investigated before retry. No automatic Argo sync policy is enabled without company policy inputs.

## Validation status

`scripts/validate-deployment.ps1` uses clearly synthetic `validation.invalid` names, runs Helm lint/render for both providers and both orchestration modes, and submits server-side dry-runs to **Minikube only**. It does not contact a company environment or exercise an Argo controller. See `docs/kubernetes-verification.md` for executed checks and evidence.

Actual company dev deployment requires explicitly authorized access and real environment inputs. Cloud federation, external database TLS/routing/permissions, company registry pulls, ingress certificates and Argo CD PreSync execution still require validation there. A Helm PoC does not establish that Argo CD was tested.

See [template deployment policy](docs/template-hardening.md) for required Key Vault federation, strict persistence settings, OpenTelemetry, CI enforcement, bundle artifacts and the pending approved FIPS/E28 inputs. Kubernetes Secret instructions apply to the isolated PoC AppSettings mode; company dev defaults to KeyVaultRequired and omits connection/JWT Secret injection.

Explicit fallback and read-only file mounting are described in [naming and secret policy](docs/naming-secret-policy.md).
