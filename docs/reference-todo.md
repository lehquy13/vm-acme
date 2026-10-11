# Todo example conventions

Adapted from [GeorgeLeithead/CleanArchitecture/net8](https://github.com/GeorgeLeithead/CleanArchitecture/tree/main/net8), commit `8e7babe01e49289ef62528392aea4fd665a88170`: Todo/account behavior, Result objects, current-user abstraction and dynamic permission policies. Retains this starter's .NET 10, deployment tooling, database providers and MediatR 12.5.0.

## Request flow and errors

FastEndpoints binds DTOs and dispatches application requests through MediatR. Logging/metrics wrap validation and handlers. All application requests return Result<T>. Validation returns field errors without executing handlers; expected failures are values. Endpoint adapters share ResultHttpMapper. Unexpected exceptions propagate to GlobalExceptionHandler, which logs server-side and returns a generic 500 with trace ID.

Error types map to validation 400, unauthorized 401, forbidden 403, missing 404 and conflict 409. Binding/application failures return application/problem+json with stable codes and trace IDs. Authentication challenges and authorization failures use standard ASP.NET 401/403 responses. Metrics count Result failures and thrown exceptions.

## Todos

Routes: POST/GET `/api/todos`; GET/DELETE `/api/todos/{id}`; POST `/api/todos/{id}/complete`. All require a registered authenticated user. The owner is the validated JWT subject and cannot be chosen in the payload. Repositories filter by ID and owner; foreign-owned IDs return 404. Completion is one-way and records a UTC timestamp; repeated/concurrent completion returns 409.

Descriptions are required, max 255. Priority: Normal=0, Low=1, Medium=2, High=3, Top=4. Due date is optional and cannot precede the current UTC date. Up to 20 nonempty labels of max 64 characters. Timestamps are UTC.

## Authentication and permissions

Anonymous registration/login use normalized unique email and ASP.NET Identity's salted, versioned PBKDF2 password hashing. Database uniqueness handles racing registration. Password hashes never appear in responses. HS256 JWTs include sub, email and jti with configured issuer/audience/expiration. Validation checks signature, algorithm, issuer, audience, expiry and registered GUID subject; MapInboundClaims=false preserves sub.

Application depends on IUserContext, IPasswordHasher, ITokenProvider, IUserRepository and IPermissionProvider; framework implementations live in Infrastructure. PermissionPolicyProvider builds `Permission:<permission>` policies with an explicit authenticated-user requirement. The handler only succeeds for authenticated GUID subjects with server-resolved grants. Unknown permissions fail closed. The example provider grants registered users `users:read`; replace it with persisted project roles/grants. GET `/api/users/{id}` also enforces self-only access. Clients cannot assign permissions.

The upstream permission handler succeeds for unauthenticated principals; this adaptation explicitly requires authentication. Refresh tokens, email verification, password reset and external identity integration are outside this example.

## Persistence and verification

Additive PostgreSQL/SQL Server migrations create Users/TodoItems and preserve historical WorkItems. Retained WorkItem models/repositories exist for database compatibility; their public routes and application requests are removed. New account/Todo data is removed by rolling back the new migration.

Run `dotnet test vm-acme.slnx` for local gates. Run `scripts/test-databases.ps1` for disposable live databases, migration/persistence round trips, ownership, uniqueness and completion races. Without database settings, live cases skip; snapshots and generated SQL still run. Live tests apply migrations and create records, so use disposable test databases only.

Architecture checks inspect compiled references and project XML, including unused dependencies. They enforce layer/framework boundaries, Result-returning requests, FastEndpoints and no repository/DbContext injection into endpoint constructors.

## Reference attribution

Upstream publishes the MIT notice below. Its placeholder copyright line is preserved verbatim. This notice applies to reference-derived portions, not a new license declaration for the entire starter.

```text
MIT License

Copyright (c) [year] [fullname]

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
```
