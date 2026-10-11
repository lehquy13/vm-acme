# Naming and startup secret policy

| Convention | Policy |
|---|---|
| Repository | Remove one leading `vm-`; validate letter-led segments and length before mutation. |
| .NET | PascalCase identifier, e.g. `vm-order-service` ? `VmOrderService`. |
| Deployment | DNS slug, e.g. `vm-order-service`; never duplicate `vm-`. |
| Environment / metrics | `VM_ORDER_SERVICE` / `vm_order_service`. |
| AppSettings | No vault calls. Validate the complete settings snapshot. |
| KeyVaultRequired | Company dev default. Complete vault set required; failures abort startup. |
| KeyVaultWithFallback | Explicit opt-in. Complete valid vault set wins; otherwise use a complete valid application settings set. |
| Required secrets | API: database connection (unless explicit local InMemory) plus signing key =32 UTF-8 bytes. Runner/bundles: database connection only; never request JWT. |
| Precedence | Tracked appsettings ? explicit secrets file ? environment ? complete vault snapshot. Vault partial results are never published. Runner `--connection` applies only in AppSettings; prefer shared configuration for every path. |
| File paths | Absolute `Secrets__FallbackFile` is recommended. Relative paths resolve against absolute `Secrets__BaseDirectory`, defaulting to `AppContext.BaseDirectory`, never the current working directory. |
| Identities | Separate API/migrator service accounts, client IDs, vault connection secrets, and fallback Kubernetes Secrets. |
| Loading / rotation | Startup-only snapshot. Restart API pods and rerun runner/bundles after rotation; no live reload. |
| Bounds / diagnostics | Whole vault phase =30 seconds, =3 SDK retries, request network timeout 5 seconds. Sanitized warning; `secrets.fallback.activations` meter and `vm_acme_secret_fallback_active` API indicator. No values or service error payloads logged. |

## Operator fallback file

Provision `appsettings.secrets.json` outside Git and images. The name and `appsettings.*.secrets.json` are ignored by Git and Docker. Do not populate a tracked example.

The API file contains `ConnectionStrings:DefaultConnection` and `Authentication:Jwt:SigningKey`. The migration file contains only `ConnectionStrings:DefaultConnection`. Set `Secrets__FallbackFile` to an absolute path. For shared relative file names set the same absolute `Secrets__BaseDirectory` on all three paths.

API settings use the normal ASP.NET configuration (including local user secrets). Runner and provider EF factories use optional `appsettings.json` beside the executable and environment configuration, then the shared policy. Environment variables override the explicit file. The file is loaded only when settings are selected; an unavailable fallback file does not prevent a successful vault snapshot.

Kubernetes: create separate operator-owned Secrets from each file, outside the chart:

```powershell
kubectl create secret generic orders-api-fallback --from-file=appsettings.secrets.json=/operator/api/appsettings.secrets.json -n orders
kubectl create secret generic orders-migration-fallback --from-file=appsettings.secrets.json=/operator/migration/appsettings.secrets.json -n orders
```

Set `secrets.mode=KeyVaultWithFallback`, `application.fallbackSecretName=orders-api-fallback`, and `migration.fallbackSecretName=orders-migration-fallback`, alongside the required vault/identity settings. Helm mounts each file read-only at `/etc/vm-acme/secrets/appsettings.secrets.json`; it never embeds file contents. AppSettings may also use those mounts explicitly. Required mode does not mount fallback files.

Run provider bundles without `--connection` so their EF factory applies this policy; EF's own explicit connection override bypasses the factory's selected connection. Company launch commands must omit that override.

Live workload federation, vault access, and actual company secret names require authorized company infrastructure and are separate from injected-reader local tests.
