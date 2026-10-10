# Acme Clean Architecture Project Design

## Goal

Create a runnable .NET 10 starter solution named `Acme` with the requested Clean Architecture and DDD project layout. A developer can run the API and exercise a small use case without configuring or starting a database.

## Approved direction

Use a small `WorkItem` aggregate as the example domain. Domain owns its rules and value objects; Application owns use cases and persistence abstractions; Infrastructure supplies both an in-memory repository used by default and an optional EF Core repository. The Web API stays thin and delegates to Application use cases. Database migrations and a migrator are optional tooling and do not block the default run path.

## Project layout

```text
src/
  Acme.Domain/
  Acme.Application/
  Acme.Infrastructure/
  Acme.WebApi/
tools/
  Acme.DbMigrator/
migrations/
  Acme.Migrations.PostgreSql/
  Acme.Migrations.SqlServer/
tests/
  Acme.UnitTests/
  Acme.IntegrationTests/
deploy/
  helm/acme/
Dockerfile
compose.yaml
Acme.slnx
init.sh
init.ps1
```

## Architecture and behavior

- Target .NET 10 and retain the exact project names and directories above.
- Enforce inward project references: Domain has no project dependencies; Application references Domain; Infrastructure references Application and Domain; WebApi composes the application and infrastructure.
- Model `WorkItem` as an aggregate root with a strongly typed ID, a validated title value object, and behavior to mark the item complete. Persist and load aggregate roots through a `IWorkItemRepository` abstraction declared in Application.
- Provide Application use cases to create, list, and complete work items. No mediator dependency is required for this small template; endpoints call the use cases through DI.
- Register the in-memory repository by default. Provide an opt-in EF Core persistence path configured by provider and connection string. The API must start and serve requests with no database environment variables or services.
- Expose create/list/complete HTTP endpoints below `/api/work-items`, plus `/health`. Invalid input returns a client error; completing a missing ID returns 404.
- Keep `Acme.Migrations.PostgreSql` and `Acme.Migrations.SqlServer` separate and provider-specific. `Acme.DbMigrator` accepts a provider and connection string and applies the matching migrations. Migration execution is never part of the default API startup.
- Include focused domain unit tests and API integration tests using the in-memory implementation.
- Provide Docker Compose for a database-free API run and a Helm chart for deploying the API with provider selection and connection settings configurable.
- Provide `init.sh` and `init.ps1` scripts that create/restore/build the solution using the installed .NET SDK. Scripts must not install databases or require secrets.
- Preserve existing unrelated workspace files and avoid replacing the existing `vm-acme.slnx`; create the requested `Acme.slnx` alongside it.

## Acceptance criteria

1. `dotnet build Acme.slnx` succeeds from the repository root.
2. `dotnet run --project src/Acme.WebApi` starts with the default in-memory configuration and no external service.
3. Health and work-item create/list/complete flows are usable over HTTP, and complete on an unknown ID returns 404.
4. Unit and integration test projects build and their tests pass.
5. The solution contains both provider-specific migrations projects, the migrator, Docker files, Helm chart, and both initialization scripts at the requested paths.

## Out of scope

- Choosing or implementing a production-specific domain beyond the illustrative `WorkItem` aggregate.
- Running PostgreSQL or SQL Server as part of the default local workflow.
- Authentication, authorization, message brokers, observability stacks, or production secrets management.
