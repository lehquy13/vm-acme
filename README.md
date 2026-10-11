# vm-acme Clean Architecture Starter

This .NET 10 starter uses DDD, Clean Architecture, MediatR, Result objects and FastEndpoints. Its owner-scoped Todo example is adapted from [GeorgeLeithead/CleanArchitecture/net8](https://github.com/GeorgeLeithead/CleanArchitecture/tree/main/net8). The API runs with in-memory storage by default; PostgreSQL and SQL Server are optional.

Run `./init.ps1` on PowerShell or `bash init.sh` in Bash. The initializer prompts for the brand segment (`acme` by default) and replaces it consistently before restoring/building. `./init.ps1 -Name Billing` or `bash init.sh Billing` produces `vm-billing.slnx`, `VmBilling.*` namespaces/projects, and `deploy/helm/vm-billing`. This is a one-time rename. Press Enter to keep `vm-acme`.

Start the API with `dotnet run --project src/VmAcme.WebApi`. Register at `POST /api/users/register`, log in at `POST /api/users/login`, then send the returned `accessToken` as a Bearer token to `/api/todos`. The [HTTP example](src/VmAcme.WebApi/VmAcme.WebApi.http) covers the full flow. Todos support create, list, get, complete and delete, scoped to the authenticated owner. `GET /api/users/{id}` requires the server-resolved `users:read` permission and returns your own profile. See [design, errors and authorization](docs/reference-todo.md).

JWT settings: `Authentication:Jwt:Issuer`, `Audience`, `SigningKey`, `ExpirationMinutes` (default 60, range 1–1440). Development uses a local-only key; deployments supply a secret. Tokens validate signature, algorithm, issuer, audience, lifetime and registered GUID subject. Passwords use ASP.NET Identity hashing. In-memory accounts and Todos reset when the process restarts.

`/health`, `/ready`, `/version` and `/metrics` remain anonymous. Structured JSON logs go to stdout for Kibana. Metrics include MediatR request counts, durations and Result/exception failures; replace the placeholder adapter with project internal metrics packages.

Run `dotnet test vm-acme.slnx` for unit, API, migration-model and architecture tests. Live database tests are opt-in: `./scripts/test-databases.ps1` starts disposable PostgreSQL/SQL Server containers, runs the whole suite and removes the containers. Docker must be running. Architecture tests enforce layer dependencies, framework boundaries, FastEndpoints adapters and Result contracts.

Database migrations are applied separately by `tools/VmAcme.DbMigrator`. Additive migrations create `Users` and `TodoItems` for both providers. Historical `WorkItems` data and mappings remain for compatibility; `/api/work-items` is retired and data is not automatically assigned to users. Kubernetes setup: [POC-QUICKSTART.md](POC-QUICKSTART.md). Company development profile: [DEV-DEPLOYMENT.md](DEV-DEPLOYMENT.md).
