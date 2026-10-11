# Template hardening

User authorized autopilot execution, routine design decisions and Luna-only Cavecrew subagents. Preserve existing user changes in docs/notes.md.

## Deliverables

1. Initializers process the same file types, exclude generated/index data, and translate deployment tokens separately from .NET identifiers. Kubernetes slug uses lowercase and underscores become hyphens; limit project-derived slug to 40 characters. Uppercase test environment variables and snake-case metrics follow the initialized project name. Verify two independent names with both initializer paths where available.
2. Persistence provider is mandatory. Explicit InMemory is allowed only in Development or Testing. Shared settings have no provider; Development explicitly chooses InMemory. Company/Production fail startup for missing configuration. Enable bounded provider retries and a pooled context factory, preserving scoped repository contexts.
3. Generate local signing credentials through .NET user secrets; tracked runtime configuration has no signing key. Add explicit Azure Key Vault configuration using workload identity, fetching only named secrets separately for API connection/JWT and migrator connection. Vault mode fails closed; no fallback to stale Kubernetes secrets. PoC retains Kubernetes secret mode. Cloud authorization and live vault validation require external inputs.
4. Required database CI executes both engines and inspects TRX results for required tests, rejecting missing/skipped coverage. Local ordinary tests may still skip live database tests.
5. Replace placeholder HTTP observability with OpenTelemetry HTTP/runtime instrumentation and database command tracing. Preserve application metrics compatibility while documenting its boundary.
6. Record current source revision, dirty state, and image digests in new Kubernetes evidence. Rebuild before rerunning both providers. Correct stale auth documentation.

## Company boundaries

No approved FIPS image reference, certification scope, E28 bundle specification, vault URI, cloud identity or company cluster credentials exist in this checkout. Expose configurable build base images and document required evidence; do not label arbitrary images FIPS compliant. Keep the existing migration runner for PoC failure injection; add a provider-specific self-contained EF bundle delivery path separately if compatible with the defined secret bootstrap. Do not claim live company compliance without these inputs.

## Verification

Regression tests for provider/environment matrix, named-secret configuration and fail-closed behavior; Rider build; full solution tests; disposable dual-database suite; initialization smoke runs; Helm render/server dry-run; current PoC provider verification and digest reports. Report any unavailable external validation explicitly.
