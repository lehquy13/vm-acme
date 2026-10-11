# vm-acme Clean Architecture Starter

This .NET 10 starter uses DDD and Clean Architecture. The API runs with in-memory storage by default; PostgreSQL and SQL Server are optional.

Run `./init.ps1` on PowerShell or `bash init.sh` in Bash. The initializer prompts for the brand segment (`acme` by default) and replaces it consistently before restoring/building. `./init.ps1 -Name Billing` or `bash init.sh Billing` produces `vm-billing.slnx`, `VmBilling.*` namespaces/projects, and `deploy/helm/vm-billing`. This is a one-time rename. Press Enter to keep `vm-acme`.

Start the default API with `dotnet run --project src/VmAcme.WebApi`. It exposes `/health`, `/ready`, `/version`, `/metrics` and JWT-protected `/api/work-items`. The API validates JWTs using `Authentication:Jwt:Issuer`, `Authentication:Jwt:Audience`, and `Authentication:Jwt:SigningKey`; local development uses a local-only key, while deployments must provide a secret. This starter does not issue tokens. Structured JSON logs go to stdout for Kibana ingestion; `/metrics` provides placeholder Prometheus metrics for Grafana scraping and should be replaced with project internal metrics packages.

Database migrations are applied separately by `tools/VmAcme.DbMigrator`. Kubernetes setup and verification: [POC-QUICKSTART.md](POC-QUICKSTART.md). Company development profile: [DEV-DEPLOYMENT.md](DEV-DEPLOYMENT.md).
