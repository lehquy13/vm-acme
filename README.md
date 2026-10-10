# Acme Clean Architecture Starter

This .NET 10 starter uses DDD and Clean Architecture. The API runs with in-memory storage by default; PostgreSQL and SQL Server are optional.

Run `./init.ps1` on PowerShell or `bash init.sh` in Bash. The initializer prompts for a project name and replaces the `Acme` placeholder in project names, namespaces, configuration, and deployment files before restoring and building. This is a one-time in-place rename. You can pass a name directly with `./init.ps1 -Name Billing` or `bash init.sh Billing`. Press Enter to keep `Acme`.

After initialization, start the API with `dotnet run --project src/<Name>.WebApi/<Name>.WebApi.csproj`. The API exposes `/health` and the WorkItem endpoints under `/api/work-items`.

Database migrations are applied separately by `tools/<Name>.DbMigrator`; API startup never connects to a database unless a provider and connection string are configured.
