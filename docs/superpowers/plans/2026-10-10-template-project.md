# VmAcme Project Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build the approved runnable .NET 10 `VmAcme` solution with DDD/Clean Architecture, optional database providers, and the requested tooling and deployment files.

**Architecture:** `VmAcme.Domain` owns the WorkItem aggregate and value objects. Application owns use cases and the aggregate repository contract; Infrastructure provides in-memory-by-default and optional EF Core persistence; WebApi is a thin composition root. PostgreSQL and SQL Server migrations are isolated in their own assemblies and are invoked only by the migrator.

**Tech Stack:** .NET 10, ASP.NET Core Minimal APIs, EF Core 10, PostgreSQL and SQL Server EF providers, xUnit, WebApplicationFactory, Docker, Helm.

**Spec:** `docs/superpowers/specs/2026-10-10-template-clean-architecture-design.md`

## Global Constraints

- Target .NET 10 and keep the exact project names and directories in the spec.
- Domain has no project or infrastructure dependency; Application references Domain; Infrastructure references Application and Domain; WebApi composes the layers.
- In-memory persistence is the default and requires no database environment variables or service.
- WorkItem aggregate rules remain in Domain; endpoints only bind HTTP and call Application use cases.
- Database migrations never run during API startup.
- Preserve `vm-acme.slnx`, `.gitignore`, and `.idea/`; create `VmAcme.slnx` alongside them.

## Review Focus

- Empty or whitespace title: reject with client error; pin in domain unit test and API integration test.
- Overlong title (>200 characters): reject; pin in domain unit test.
- Complete a missing ID: return 404; pin in API integration test.
- Complete an already completed item: preserve aggregate invariants and return a client error; pin in domain unit test and API integration test.
- Start without provider or connection settings: API uses in-memory storage and is healthy; pin in integration test and run smoke check.

---

### Task 1: Solution projects and dependency graph

**Files:**
- Create: `Directory.Build.props`, `VmAcme.slnx`
- Create: all 9 `.csproj` files under `src/`, `tools/`, `migrations/`, and `tests/` named in the spec
- Create: starter `Program.cs` files for WebApi and DbMigrator

**Interfaces:**
- Produces the project references and solution paths consumed by every following task.

- [x] Create projects targeting `net10.0`; set nullable and implicit usings consistently in `Directory.Build.props`.
- [x] Add the exact project references described by the spec and EF Core 10 provider package references to Infrastructure/migration projects; xUnit test package references come from project templates.
- [x] Add every project to `VmAcme.slnx`; verify `dotnet sln VmAcme.slnx list` reports all 9 projects.
- [x] Run `dotnet build VmAcme.slnx` and resolve restore or reference errors before adding behavior.

### Task 2: WorkItem domain model and unit tests

**Files:**
- Create: `src/VmAcme.Domain/Common/Entity.cs`, `AggregateRoot.cs`
- Create: `src/VmAcme.Domain/WorkItems/WorkItemId.cs`, `WorkItemTitle.cs`, `WorkItemStatus.cs`, `WorkItem.cs`
- Create: `tests/VmAcme.UnitTests/WorkItems/WorkItemTests.cs`

**Interfaces:**
- Produces `WorkItem.Create(WorkItemTitle title)`, `bool WorkItem.Complete()`, `WorkItem.Id`, `WorkItem.Title`, and `WorkItem.Status`.

- [x] Write unit tests for valid creation, whitespace and >200-character title rejection, first completion, and rejection of repeated completion.
- [x] Run the focused unit tests and confirm the intended failures.
- [x] Implement a strongly typed `WorkItemId`, validated immutable `WorkItemTitle`, and aggregate methods enforcing those rules.
- [x] Run the focused unit tests and confirm they pass.

### Task 3: Application contracts and use cases

**Files:**
- Create: `src/VmAcme.Application/WorkItems/IWorkItemRepository.cs`
- Create: `src/VmAcme.Application/WorkItems/WorkItemDto.cs`
- Create: `src/VmAcme.Application/WorkItems/CreateWorkItem.cs`, `ListWorkItems.cs`, `CompleteWorkItem.cs`
- Create: `src/VmAcme.Application/DependencyInjection.cs`

**Interfaces:**
- `IWorkItemRepository`: `Task AddAsync(WorkItem, CancellationToken)`, `Task<WorkItem?> GetAsync(WorkItemId, CancellationToken)`, `Task<IReadOnlyList<WorkItem>> ListAsync(CancellationToken)`, `Task UpdateAsync(WorkItem, CancellationToken)`.
- `CreateWorkItem.HandleAsync(string title, CancellationToken) -> Task<WorkItemDto>`; invalid title throws `ArgumentException`.
- `ListWorkItems.HandleAsync(CancellationToken) -> Task<IReadOnlyList<WorkItemDto>>`.
- `CompleteWorkItem.HandleAsync(Guid id, CancellationToken) -> Task<WorkItemDto?>`; returns null for missing ID and throws `InvalidOperationException` when already complete.

- [x] Add use-case tests through a small fake repository for create, list, invalid title, complete, repeated complete, and missing ID.
- [x] Implement the repository contract and exact use case signatures above without referencing ASP.NET Core or EF Core from Application.
- [x] Run unit tests and `dotnet build VmAcme.slnx`; verify Domain and Application remain framework-independent.

### Task 4: Infrastructure persistence and registration

**Files:**
- Create: `src/VmAcme.Infrastructure/Persistence/InMemoryWorkItemRepository.cs`
- Create: `src/VmAcme.Infrastructure/Persistence/VmAcmeDbContext.cs`
- Create: `src/VmAcme.Infrastructure/Persistence/EfWorkItemRepository.cs`
- Create: `src/VmAcme.Infrastructure/Persistence/Configurations/WorkItemConfiguration.cs`
- Create: `src/VmAcme.Infrastructure/DependencyInjection.cs`

**Interfaces:**
- Consumes `IWorkItemRepository` and the WorkItem aggregate from Tasks 2–3.
- Produces `AddInfrastructure(IServiceCollection, IConfiguration)` that registers in-memory by default and EF persistence when an explicit provider is configured.

- [x] Implement the in-memory repository with thread-safe storage and cancellation-aware async methods.
- [x] Map WorkItem and its strongly typed ID/title into EF Core; implement EF-backed aggregate persistence.
- [x] Select PostgreSQL or SQL Server only when explicitly configured with a connection string; throw a clear startup error for an unknown/incomplete explicit provider.
- [x] Build the solution and verify the default dependency registration does not resolve a DbContext or connect to a database.

### Task 5: Web API endpoints and integration tests

**Files:**
- Create: `src/VmAcme.WebApi/Program.cs`, `Endpoints/WorkItemEndpoints.cs`, `appsettings.json`
- Create: `tests/VmAcme.IntegrationTests/VmAcmeWebApplicationFactory.cs`, `WorkItemEndpointTests.cs`

**Interfaces:**
- HTTP: `POST /api/work-items` with `{ "title": "..." }`; `GET /api/work-items`; `POST /api/work-items/{id:guid}/complete`; `GET /health`.
- Produces standard JSON DTOs, 201 on create, 200 on list/complete, 400 on invalid input, and 404 for missing IDs.

- [x] Write integration tests for health, create/list, complete, invalid title, missing ID, repeated completion, and no-database startup.
- [x] Run those tests and confirm expected failures before implementation.
- [x] Implement thin endpoint mappings and wire Application/Infrastructure through DI.
- [x] Run the integration tests, then start the API and smoke-check health plus create/list/complete over HTTP.

### Task 6: Provider migrations and database migrator

**Files:**
- Create provider-specific initial migration and model snapshot under `migrations/VmAcme.Migrations.PostgreSql/`
- Create provider-specific initial migration and model snapshot under `migrations/VmAcme.Migrations.SqlServer/`
- Create: `tools/VmAcme.DbMigrator/Program.cs`

**Interfaces:**
- CLI: `dotnet run --project tools/VmAcme.DbMigrator -- --provider PostgreSql --connection "..."` or `--provider SqlServer --connection "..."`.
- Consumes provider-specific migration assemblies and the shared `VmAcmeDbContext`.

- [x] Implement initial schema migration for the WorkItems table in each provider project.
- [x] Configure each migration assembly to use its corresponding provider and reference the shared Infrastructure DbContext.
- [x] Implement argument validation and `Database.MigrateAsync`; fail clearly when provider or connection is missing/unknown.
- [x] Build all migration and migrator projects; verify no migration is invoked by WebApi startup.

### Task 7: Container, Compose, Helm, and init scripts

**Files:**
- Create: `Dockerfile`, `compose.yaml`
- Create: `deploy/helm/vm-acme/Chart.yaml`, `values.yaml`, and templates for Deployment and Service
- Create: `init.sh`, `init.ps1`

**Interfaces:**
- Compose runs the API without database services and leaves in-memory persistence as default.
- Helm values configure image, service, replica count, persistence provider, and connection string via environment/secret reference.
- Init scripts restore and build `VmAcme.slnx` with nonzero exit on failure.

- [x] Implement a multi-stage .NET 10 Docker build and database-free Compose service with a health check.
- [x] Implement a minimal Helm chart with configurable image, ports, replica count, and optional persistence environment.
- [x] Implement Bash and PowerShell init scripts that restore and build from repository root.
- [x] Validate both scripts by running them successfully; Docker engine is not running and Helm is not installed, so their optional render/build checks cannot run here.

### Task 8: End-to-end verification and handoff

**Files:**
- Modify only files required to address verification failures.

- [x] Run `dotnet restore VmAcme.slnx` and `dotnet build VmAcme.slnx`.
- [x] Run `dotnet test VmAcme.slnx` and confirm unit and integration suites pass.
- [x] Run the API with default settings and verify `/health` and the create/list/complete flow over HTTP.
- [x] Review `git status` and confirm pre-existing staged files and `.idea/` were not modified.

## Self-review

- Spec coverage: project layout and references (Task 1); DDD aggregate (Task 2); use cases (Task 3); fake and optional EF persistence (Task 4); HTTP behavior and default-run acceptance (Task 5); both migrations and migrator (Task 6); container, Helm, and scripts (Task 7); build/test/runtime acceptance (Task 8).
- Interface consistency: WebApi uses the Task 3 use cases; Infrastructure implements the Task 3 repository contract; migrator and migration projects target the Task 4 DbContext.
- Review Focus cases are assigned to Tasks 2, 3, and 5.
- Docker and Helm validation is conditional on tools being installed; solution build, tests, and local API smoke check are required.
