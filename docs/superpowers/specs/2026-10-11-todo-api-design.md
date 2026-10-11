# Todo API design

## Intent and decisions

Replace the public WorkItem example with the owner-scoped Todo API from GeorgeLeithead/CleanArchitecture/net8, adapted to this starter's .NET 10, DDD, MediatR and optional database providers. User requested autopilot execution; routine decisions and execution are delegated to the agent. Implement inline; Luna-only scouts/reviewers.

Port the behavior rather than replace the repository wholesale. A wholesale copy would discard deployment and provider support. Merely renaming WorkItems would omit ownership, accounts and authorization.

## API and data

Todos expose create, list, get-by-id, complete and delete under `/api/todos`. Fields: ID, authenticated owner UserId, description (required, max 255), priority (Normal=0, Low=1, Medium=2, High=3, Top=4), optional UTC due date, labels, creation/completion timestamps and completion flag. Client never chooses an owner; every lookup, update and delete is owner-scoped. Foreign-owned IDs return 404. Due dates before the current UTC date, invalid enum values, malformed IDs and missing descriptions return 400. Repeated completion returns 409.

Accounts expose anonymous `/api/users/register` and `/api/users/login`, plus authenticated `/api/users/{id}` guarded by `Permission:users:read` and self-only access. Registration stores normalized unique email, names and an ASP.NET Identity password hash. Password length is 12–128; login returns a signed expiring JWT. Invalid credentials return a generic 401; duplicate email returns 409. Use the existing JWT configuration, adding token lifetime. No permission claims supplied by clients are trusted. Server-side permission provider grants registered users `users:read`; replaceable interface supports project-specific grants. Unknown permission policies and unauthenticated users fail closed.

## Layers and errors

Domain owns Todo/User models and Result/Error primitives, with no framework dependencies. Application owns repository/auth abstractions, MediatR handlers, validators and use cases. Infrastructure provides EF/in-memory stores, hashing, token issuance, user context and permission authorization. WebApi owns FastEndpoints and HTTP mapping. Retain MediatR 12.5.0 and observability behaviors.

All new application requests return Result<T>, including validation failures. Errors map centrally to ProblemDetails with stable codes: validation 400, unauthorized 401, forbidden 403, missing 404, conflict 409. Unexpected exceptions use IExceptionHandler, log details server-side and return generic 500 with trace ID. Do not expose stack traces or exception messages. FastEndpoints binding errors use the same ProblemDetails format.

## Compatibility

Keep the existing WorkItems persistence model and migrations as legacy compatibility; remove its public routes. New Users/TodoItems tables are additive for both PostgreSQL and SQL Server. Existing data is neither deleted nor assigned arbitrary owners. Document the old example as retired. Operational health/readiness/version/metrics endpoints remain accessible.

## Verification

Test Result invariants, validation short-circuiting, completion conflicts, hash verification and cancellation. Integration tests cover real registration/login/JWT validation, CRUD, ownership isolation, permissions denied to anonymous and authenticated callers, sanitized exceptions, invalid payloads, and EF persistence. ArchitectureTests inspect layer dependencies and project references so unused forbidden references also fail. Provider snapshots and generated migration scripts must match the updated model. Build through Rider; run the whole solution test suite.
