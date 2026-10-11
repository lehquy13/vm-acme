# Research / PoC: Versioned Database Deployment and PostgreSQL Readiness

## 1. Problem

The current application uses SQL Server Database Projects (SSDT / `.sqlproj`) to manage schema changes.

The reported workflow compares schemas, generates a SQL change script, and applies it to production. The team experiences build overhead and difficulty tracing which incremental changes have been applied to each environment.

The goal is to determine whether a migration-based approach improves release traceability, deployment reliability, and PostgreSQL readiness enough to justify switching.

A runnable CRUD demo alone is insufficient. The PoC must address the existing deployment pain points.

## 2. Pain Points and Research Questions

| Pain point / concern | Research question | Required evidence |
|---|---|---|
| SQL project build overhead | Does the proposed approach reduce build and deployment preparation time? | Measure the current workflow against the PoC using comparable changes. |
| Unclear change history per environment | Can we identify which changes were applied and which are pending? | Demonstrate migration history and its relationship to application releases. |
| Manual schema comparison and script preparation | Can deployment use a reviewed, versioned artifact produced by CI? | Deploy the same release artifact to separate environments without regenerating changes from the development database. |
| Risk when upgrading databases containing data | Can representative schema changes preserve existing data? | Upgrade a populated database and validate values, constraints, and relationships. |
| Different starting versions across environments | Can the deployment apply only the required changes? | Upgrade databases from different known migration versions to the same target version. |
| Failed deployments and retries | What happens when a migration fails, and how is recovery performed? | Inject a failure, inspect schema/history state, and demonstrate a documented recovery procedure. |
| SQL Server dependency | Which application and schema changes are required for PostgreSQL? | Execute equivalent business scenarios on both providers and document incompatibilities. |
| Uncontrolled schema drift | What happens when the database is modified outside the deployment process? | Introduce a manual schema change and document detection and handling. |

These concerns must be validated against the actual workflow. SSDT/DACPAC can already support automated CI/CD and schema comparison; manual deployment is not an inherent limitation of the tool.

## 3. Three Solutions

| Solution | Advantages | Disadvantages |
|---|---|---|
| **1. EF Core + EF Core Migrations** | Shared application model; generated migrations; explicit migration history; convenient integration with .NET development. | Requires migration review, baseline adoption, separate provider migrations, and additional handling for SQL objects and schema drift. |
| **2. Dapper + DbUp** | Explicit control over queries, schema changes, and data transformations; suitable for SQL-heavy applications. | More handwritten SQL; provider-specific queries/scripts; greater maintenance effort for CRUD and entity updates. |
| **3. EF Core + DbUp** | EF Core for application persistence with explicit SQL scripts for database deployment. | EF mappings and SQL schema must remain synchronized; provider-specific scripts still require maintenance. |

**Selected candidate for the initial PoC: Solution 1 — EF Core + EF Core Migrations.**

This is a hypothesis to validate, not a commitment to replace the existing deployment process.

## 4. PoC Scope

Build a runnable Git template containing:

- .NET 10 Web API with Clean Architecture.
- Project initialization with consistent solution, namespace, Docker, and Helm naming.
- PostgreSQL as the default database and SQL Server as an alternative.
- Separate migrations and model snapshots for each provider.
- A dedicated migration runner.
- Docker Compose for local execution.
- Dockerfile and Helm chart for deployment.
- A sample feature with real database reads and writes.
- Integration tests against both database providers.

## 5. Required Demonstrations

### A. Initialize and Run

Initialize the template with a new project name and start it using one documented command.

Verify:
- The generated solution builds.
- The database becomes ready.
- Migrations complete before the API starts.
- Create/read endpoints work against a real database.
- Data survives an API restart.
- Generated names contain no template placeholders.

### B. Upgrade a Populated Database

Deploy version 1 and insert representative data.

Upgrade through changes such as:
- Adding a required column using a staged backfill.
- Renaming a column while preserving values.
- Adding a unique constraint after handling conflicting data.

Verify the resulting data and schema. Compare the implementation and review effort with the current SSDT workflow.

### C. Adopt an Existing Database

Demonstrate a baseline procedure for a representative existing SQL Server schema.

Verify:
- Existing tables and data are preserved.
- The baseline is recorded only after schema compatibility is verified.
- Subsequent migrations apply successfully.
- A fresh database can still be created from the migration chain.

### D. Deploy, Fail, and Recover

Produce a versioned migration artifact and execute it through a dedicated deployment step.

Verify:
- Migration failure prevents the new API rollout.
- Re-running a successful deployment does not reapply completed migrations.
- Failure behavior and recovery are documented.
- Representative upgrade steps remain compatible with the previous API version during rolling deployment.

### E. Validate PostgreSQL Readiness

Run equivalent business scenarios on SQL Server and PostgreSQL.

Verify representative behavior involving:
- Decimal precision.
- Timestamps and UTC handling.
- Generated identifiers.
- Constraints and relationships.
- Case sensitivity and string comparison.

Document provider-specific code, migrations, and unsupported assumptions.

This demonstrates provider compatibility. It does not demonstrate production data transfer between SQL Server and PostgreSQL.

## 6. Acceptance Criteria

- The template runs end to end; successful initialization alone does not count.
- Both database providers pass the agreed integration scenarios.
- Upgrading a populated database preserves the expected data.
- Existing-database baseline adoption is demonstrated.
- Deployment history can be traced to release artifacts.
- Failure and recovery behavior is demonstrated.
- Build/deployment effort is measured against the current workflow.
- Schema drift limitations are explicitly documented.
- Helm deployment is validated on a test Kubernetes environment.

## 7. Decision Criteria

Recommend adoption only if the evidence shows meaningful improvements in the team's actual pain points without unacceptable maintenance or operational costs.

The recommendation must include:
- Measured benefits.
- Remaining limitations.
- Adoption effort and baseline strategy.
- Ownership of migrations and SQL objects.
- PostgreSQL compatibility gaps.
- A staged rollout proposal.

Retaining and improving SSDT/DACPAC remains a valid outcome if the PoC does not demonstrate sufficient benefit.

## 8. Non-Goals

- Automatic conversion of all T-SQL, stored procedures, or views.
- Production data migration or cutover to PostgreSQL.
- Automatic detection or repair of all schema drift.
- Guaranteed rollback of destructive changes.
- Replacing the production deployment process before the PoC is evaluated.