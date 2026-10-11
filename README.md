# vm-acme Clean Architecture Starter

This .NET 10 starter uses DDD and Clean Architecture. The API runs with in-memory storage by default; PostgreSQL and SQL Server are optional.

Run `./init.ps1` on PowerShell or `bash init.sh` in Bash. The initializer prompts for the brand segment (`acme` by default) and replaces it consistently before restoring/building. `./init.ps1 -Name Billing` or `bash init.sh Billing` produces `vm-billing.slnx`, `VmBilling.*` namespaces/projects, and `deploy/helm/vm-billing`. This is a one-time rename. Press Enter to keep `vm-acme`.

Start the default API with `dotnet run --project src/VmAcme.WebApi`. It exposes `/health`, `/ready`, `/version`, `/metrics` and `/api/work-items`.

Database migrations are applied separately by `tools/VmAcme.DbMigrator`. Kubernetes setup and verification: [POC-QUICKSTART.md](POC-QUICKSTART.md). Company development profile: [DEV-DEPLOYMENT.md](DEV-DEPLOYMENT.md).
