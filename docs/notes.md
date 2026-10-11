| Convention area | What to include |
|---|---|
| **Naming** | Projects/namespaces, images, Helm releases/resources, labels and configuration keys |
| **Data access** | EF Core by default; Dapper for tuned reads; `<Query>.sqlserver.sql` / `<Query>.postgres.sql`; prohibited legacy APIs |
| **Schema ownership** | One migration-owning context per DB; domain contexts do not independently manage schema |
| **Migrations** | Approved naming, immutable historical SQL, provider-specific assemblies, expand/contract and forward-fix policy |
| **Portability** | UTC handling, explicit decimal precision, comparison semantics, deterministic ordering and keyset paging |
| **Reliability** | Cancellation tokens, defined timeouts, transaction boundaries and safe retry rules |
| **Observability** | Structured logs, correlation IDs, agreed metrics/tags and sensitive-data redaction |
| **Deployment/security** | Separate API/migrator identities, verified TLS, resource limits, health probes and approved image/version policy |