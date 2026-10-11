# Company development deployment

Use `deploy/helm/vm-acme`: shared `values.yaml`, `values-dev.yaml` and a company-owned overlay supplied by the operator. There are no company endpoints, identity IDs or credentials in this repository. The PoC infrastructure manifest and public test credentials must never be deployed here.

## Required inputs

Supply the authorized kube context/namespace, provider (`PostgreSql` or `SqlServer`), registry/project and immutable API/migrator release tags; image pull secret name; external database endpoints/database name; separate API and migrator connection secret names/keys; API and migration service account names and any federated identity annotations/Pod labels; ingress controller class/host, annotations and TLS secret; replica count and resource requests/limits. For Argo CD also supply its namespace/project, repository URL, Git revision, destination server and overlay path. Restrict deployment access to the target namespace.

Connection secrets use `connection` by default. Provision them using the company secret manager or externally synchronized Kubernetes Secrets. API credentials should have only the required table DML rights; migration credentials need schema change privileges and migration history rights. PostgreSQL table/default grants must allow future tables for the API role. SQL Server API credentials must not be schema owner. Require TLS verification for external databases; the PoC `TrustServerCertificate=True` setting is not a dev default.

## Bootstrap and Helm deployment

Complete cluster storage/network/ingress/TLS, registry pull access, external database/schema/user grants, connection Secrets and identity federation before application deployment. The prerequisite phase of the same chart manages only the two service accounts as a **separate release**, so pre-install Jobs have identities on the first application install:

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

The namespace and all referenced secrets must already exist. `DEV_VALUES` supplies actual environment values without committing credentials. API and migrator images are built using `docker build --target api` and `--target migrator`, with `--build-arg RELEASE_VERSION=<release>`; CI publishes both once and promotes the same immutable artifacts. Configure migration timeout below the deployment timeout. One release should migrate a given database at a time.

## Identity and orchestration

Configure `application.serviceAccountAnnotations`, `migration.serviceAccountAnnotations`, separate `podLabels` and `podAnnotations` for the company's workload identity webhook. Both accounts disable automatic Kubernetes API token mounting; provider webhooks may inject projected identity tokens. Federated trust, service-account subjects/audiences and cloud roles are external prerequisites. Connection Secrets remain the current application's database access mechanism; annotating an account alone does not implement PostgreSQL token authentication or secret-store retrieval. If the company requires token-based DB authentication, validate the selected driver/token integration separately before dev rollout.

With `migration.orchestration=helm`, the dedicated Job is a `pre-install,pre-upgrade` hook. Its failure aborts Helm before applying the new Deployment. Existing API Pods continue running; databases are not rolled back automatically. Migrator reads its secret from environment and reports migration IDs/counts. Successful reruns do not reapply completed migrations. Failed Job logs are retained until the next attempt. Use reviewed compatible migrations; a Helm rollback cannot undo schema changes safely.

For Argo CD set `migration.orchestration=argocd`, substitute the required inputs in `deploy/argocd/application.yaml`, and submit to the authorized Argo instance. The migration Job uses **PreSync**, wave `-10`; API resources sync afterward at wave `0`. Bootstrap service accounts/secrets outside this Application first. Helm annotations are omitted in Argo mode. Use a full Application sync to run PreSync; selective resource sync can bypass hooks. Failed hooks must be investigated before retry. No automatic Argo sync policy is enabled without company policy inputs.

## Validation status

`scripts/validate-deployment.ps1` uses clearly synthetic `validation.invalid` names, runs Helm lint/render for both providers and both orchestration modes, and submits server-side dry-runs to **Minikube only**. It does not contact a company environment or exercise an Argo controller. See `docs/kubernetes-verification.md` for executed checks and evidence.

Actual company dev deployment requires explicitly authorized access and real environment inputs. Cloud federation, external database TLS/routing/permissions, company registry pulls, ingress certificates and Argo CD PreSync execution still require validation there. A Helm PoC does not establish that Argo CD was tested.
