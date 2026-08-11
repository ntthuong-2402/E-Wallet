# Friday Project Map

> Admin/Identity delta (2026-08-10): this map predates enforced permissions, lockout, durable security audit, refresh reuse/concurrency protection, bootstrap admin, role removal, pagination, relational integrity, and 11 automated Admin security tests. Use `.codex/state/ADMIN_REVIEW.md` for current Admin status.

Evidence status used throughout: **CONFIRMED**, **PARTIALLY_CONFIRMED**, **NOT_IMPLEMENTED**, **UNKNOWN**.

## 1. Repository Summary

Friday is currently a single-process ASP.NET Core API composed from Admin and Sample module projects plus shared BuildingBlocks. The executable is `Friday.API`; the module projects are class libraries, not independently deployable services. The code combines module/layer project boundaries with feature-oriented command/query files and minimal API endpoint groups. The best evidence-based description is a **hybrid modular monolith with layered module projects and vertical-slice-style application features**.

- **CONFIRMED** deployable host: `src/API/Friday.API`.
- **CONFIRMED** functional module: Admin (registration, login, refresh/logout, users, roles, rights).
- **CONFIRMED** demonstration module: Sample (in-memory todo API).
- **CONFIRMED** shared relational persistence: one `FridayDbContext` loads Admin and shared localization mappings.
- **NOT_IMPLEMENTED** banking/payment business modules.
- **NOT_IMPLEMENTED** independently deployed module services.

Evidence:
- File: `src/API/Friday.API/Program.cs`
- Method: top-level startup; `AddAdmin*`, `AddSample*`, `MapAuthModule`, `MapAdminModule`, `MapSampleModule`
- File: `src/BuildingBlocks/Friday.BuildingBlocks.Infrastructure/Persistence/FridayDbContext.cs`
- Class: `FridayDbContext`
- Method: `OnModelCreating`

## 2. Technology Stack

| Item | Status | Current implementation |
|---|---|---|
| .NET target | CONFIRMED | `net10.0` for all projects via `src/Directory.Build.props`. |
| ASP.NET Core | CONFIRMED | ASP.NET Core 10 shared framework / `Microsoft.NET.Sdk.Web`; package set is 10.0.x. |
| Language settings | CONFIRMED | Nullable reference types and implicit usings enabled. |
| API style | CONFIRMED | Minimal APIs and route groups; no MVC controllers found. |
| CQRS/mediator | CONFIRMED | LinKit.Core 2.2.3 (`IMediator`, `ICommand`, `IQuery`, handlers and behavior attributes). |
| ORM | CONFIRMED | EF Core 10.0.x. |
| Primary configured DB | CONFIRMED | PostgreSQL/Npgsql; Docker uses PostgreSQL 16. |
| Other DB capability | PARTIALLY_CONFIRMED | SQL Server, MySQL and Oracle providers/configuration are compiled in, but current EF migration is PostgreSQL-specific. In-memory fallback is active when `FridayDb` is absent. |
| SQL alternative | CONFIRMED | linq2db factory is registered; no business consumer was found. |
| Migrations | CONFIRMED | EF Core schema migrations plus FluentMigrator data migrations. |
| Cache | CONFIRMED | In-memory or Redis selected by configuration. |
| Authentication | CONFIRMED | ASP.NET Core JWT bearer with symmetric HMAC-SHA256 tokens. |
| Password hashing | CONFIRMED | ASP.NET Core Identity `PasswordHasher<CredentialUser>`. |
| Logging | CONFIRMED | Serilog structured request/application logging. |
| Observability | CONFIRMED | OpenTelemetry ASP.NET Core, HTTP and runtime instrumentation; OTLP export; Jaeger in Compose. |
| API documentation | CONFIRMED | Swashbuckle/OpenAPI, exposed only in Development. |
| Message broker | NOT_IMPLEMENTED | No RabbitMQ/MassTransit client, topology, outbox, or inbox code/package found. |

Package versions are centralized in `src/Directory.Packages.props`.

## 3. Solution Structure

Both `Friday.slnx` and legacy `src/src.sln` enumerate ten projects:

- `Friday.API`
- `Friday.BuildingBlocks.Domain`
- `Friday.BuildingBlocks.Application`
- `Friday.BuildingBlocks.Infrastructure`
- `Friday.Modules.Admin.Domain`
- `Friday.Modules.Admin.Application`
- `Friday.Modules.Admin.Infrastructure`
- `Friday.Modules.Sample.Domain`
- `Friday.Modules.Sample.Application`
- `Friday.Modules.Sample.Infrastructure`

There are no test projects in either solution and no test source/project files were found outside ignored build folders.

## 4. Project Dependency Map

Actual `ProjectReference` direction:

```text
Friday.API
  -> BuildingBlocks.Application
  -> BuildingBlocks.Infrastructure
  -> Admin.Application
  -> Admin.Infrastructure
  -> Sample.Application
  -> Sample.Infrastructure

BuildingBlocks.Application -> BuildingBlocks.Domain
BuildingBlocks.Infrastructure -> BuildingBlocks.Application + BuildingBlocks.Domain

Admin.Domain -> BuildingBlocks.Domain
Admin.Application -> Admin.Domain + BuildingBlocks.Application
Admin.Infrastructure -> Admin.Application + Admin.Domain + BuildingBlocks.Infrastructure

Sample.Domain -> BuildingBlocks.Domain
Sample.Application -> Sample.Domain + BuildingBlocks.Application
Sample.Infrastructure -> Sample.Application + Sample.Domain + BuildingBlocks.Infrastructure
```

- **CONFIRMED** no Domain-to-Infrastructure project reference.
- **CONFIRMED** no Admin-to-Sample or Sample-to-Admin project reference.
- **INFO / coupling:** `FridayDbContext` in BuildingBlocks.Infrastructure hard-codes module infrastructure assembly names, so shared infrastructure knows installed modules.
- **INFO / coupling:** API directly references both Application and Infrastructure projects, acting as composition root.
- **INFO / shared data boundary:** Admin and localization entities share one `FridayDbContext`, connection, migration set, and unit of work; database ownership is not isolated per module.

Evidence:
- Files: all `*.csproj`
- File: `src/BuildingBlocks/Friday.BuildingBlocks.Infrastructure/Persistence/FridayDbContext.cs`
- Class: `FridayDbContext`

## 5. Current Architecture

Classification: **CONFIRMED hybrid modular monolith**.

Why:

- One process and API host compose all modules.
- Admin and Sample each have Domain/Application/Infrastructure projects with inward dependency direction.
- API endpoints and application handlers are organized by feature.
- A shared DbContext and UnitOfWork cross module boundaries.
- Infrastructure module discovery is a hard-coded shared list rather than autonomous module bootstrap.

It has Clean-Architecture-like dependency direction and DDD-like entities/domain events, but calling the entire system strict Clean Architecture or strict DDD would overstate the evidence. It is not a microservice architecture.

Architecture inventory:

| Concern | Status | Evidence summary |
|---|---|---|
| Startup | CONFIRMED | Top-level `Program.cs`, built-in DI, extension-method registrations. |
| Module boundaries | PARTIALLY_CONFIRMED | Separate projects/namespaces, but shared host/DbContext/UoW. |
| Endpoint organization | CONFIRMED | Minimal API groups under `/api/auth`, `/api/admin`, `/api/sample`. |
| Controllers | NOT_IMPLEMENTED | No controllers or `AddControllers`/`MapControllers` found. |
| CQRS | CONFIRMED | LinKit commands, queries, handlers, mediator, command transaction behavior. |
| Validation pipeline | NOT_IMPLEMENTED | No FluentValidation or dedicated validation behavior; validation is inline/domain exceptions/model binding. |
| Result pattern | NOT_IMPLEMENTED | No generic domain/application `Result<T>` abstraction found. API uses `ApiResponse`/`ApiResults`; failures use exceptions. |
| Domain events | CONFIRMED | In-process LinKit notifications dispatched during unit-of-work commit. |
| Integration events | PARTIALLY_CONFIRMED | Contract/handler types exist; no durable broker or outbox. |

## 6. BuildingBlocks

### Friday.BuildingBlocks.Domain

Responsibility: generic domain primitives.

| Abstraction | Location | Purpose / implementation / consumers |
|---|---|---|
| `Entity` | `Domain/Entities/Entity.cs` | Integer identity, UTC created/updated fields, in-memory domain event collection; inherited by Admin aggregates and Sample todo. Generic. |
| `AggregateRoot` | `Domain/Entities/AggregateRoot.cs` | Marker base over `Entity`; inherited by User, Role, Right, TodoItem. Generic. |
| `IDomainEvent` | `Domain/Abstractions/IDomainEvent.cs` | Extends LinKit `INotification` and requires `OccurredOnUtc`; implemented by module event records. Generic but coupled to LinKit. |
| `ValueObject` | `Domain/Primitives/ValueObject.cs` | Value-object equality primitive. No confirmed business consumer found. Generic. |

No repository, authentication, current-user, clock, money, or transaction abstractions exist in Domain BuildingBlocks.

### Friday.BuildingBlocks.Application

Responsibility: generic application ports, errors, CQRS behavior, localization and event contracts.

| Abstraction | Location | Purpose / implementation / consumers |
|---|---|---|
| `IUnitOfWork` | `Application/Abstractions/IUnitOfWork.cs` | Begin/commit/rollback; implemented by `EfUnitOfWork` and `InMemoryUnitOfWork`; consumed by `TransactionBehavior`. |
| `TransactionBehavior<TRequest,TResponse>` | `Application/Behaviors/TransactionBehavior.cs` | LinKit behavior for every `ICommand<TResponse>`; wraps handler in UoW transaction and commit/rollback. |
| `IDomainEventDispatcher` | `Application/Abstractions/IDomainEventDispatcher.cs` | Dispatches tracked entity events; implemented by infrastructure `DomainEventDispatcher`. |
| `ICacheService` | `Application/Abstractions/ICacheService.cs` | JSON cache port; Memory/Redis implementations. No feature consumer confirmed. |
| `IErrorLocalizationStore` | `Application/Localization/IErrorLocalizationStore.cs` | Error-message lookup port; EF implementation. |
| `IDataSeeder` | `Application/Seeding/IDataSeeder.cs` | Seed abstraction; consumers not confirmed. |
| `IIntegrationEvent` | `Application/IntegrationEvents/IIntegrationEvent.cs` | In-process notification-shaped integration event contract. |
| `UserCreatedIntegrationEvent` | `Application/IntegrationEvents/UserCreatedIntegrationEvent.cs` | Business-specific user event located in shared BuildingBlocks; consumed by Sample handler. This is not purely generic infrastructure. |
| `FridayException` | `Application/Exceptions/FridayException.cs` | Error code, message and HTTP status exception mapped by API middleware. |
| `ErrorCodes` | `Application/Errors/ErrorCodes.cs` | Common and Admin error-code constants; Admin-specific knowledge exists in shared Application. |

`ICommand`, `ICommandHandler`, `IQuery`, `IQueryHandler`, `IMediator`, pipeline contracts, and notification contracts come from external `LinKit.Core.Cqrs`; Friday does not define its own wrappers.

No current-user or clock/time application abstraction was found.

### Friday.BuildingBlocks.Infrastructure

Responsibility: shared EF persistence/UoW, database-provider selection, EF and FluentMigrator execution, localization storage, cache implementations, linq2db factory, domain-event dispatch, and root-service-provider access.

Important implementations:

- `FridayDbContext` — shared EF model; loads BuildingBlocks, Admin, and Sample configuration assemblies.
- `EfUnitOfWork` — `SaveChangesAsync`, dispatches tracked domain events, commits/rolls back relational transaction.
- `DomainEventDispatcher` — sequential in-process mediator publication.
- `RelationalDbContextConfigurer` — PostgreSQL/SQL Server/MySQL/Oracle selection or in-memory fallback.
- `DatabaseMigrationStartup.ApplyEfThenDataMigrationsAsync` — optional startup EF migration followed by FluentMigrator data migration.
- `MemoryCacheService` / `RedisCacheService` — `ICacheService` implementations.
- `EfErrorLocalizationStore` — database-backed localized errors.

## 7. Existing Modules

### Admin

- Responsibilities: authentication lifecycle, user profiles/passwords/status, roles, rights, assignments and sessions.
- Application: feature files containing request records and handlers; DTOs; JWT interface/settings; notification handlers.
- Domain: `User`, `Role`, `Right` aggregate roots; `UserPassword`, `UserSession`, join entities; repository ports; domain events.
- Infrastructure: EF repositories/configurations, password hasher registration, JWT issuer.
- API exposure: Auth endpoints under `/api/auth`; authenticated Admin endpoints under `/api/admin`.
- Database ownership: logical `admin` schema, but physically owned by shared `FridayDbContext` and shared migration project.
- BuildingBlocks dependencies: Domain -> BB Domain; Application -> BB Application; Infrastructure -> BB Infrastructure.
- Other-module dependencies: none.

### Sample

- Responsibilities: demonstration todo create/list and notification handling.
- Application: create command, list query, DTO and in-process event handlers.
- Domain: `TodoItem`, repository port and domain event.
- Infrastructure: singleton static-list `InMemoryTodoItemRepository`.
- API exposure: public `/api/sample/todos` POST/GET.
- Database ownership: none; its EF configuration assembly is loadable but no entity configuration/migration table exists.
- BuildingBlocks dependencies: same three-layer direction as Admin.
- Other-module dependencies: no project reference; Sample Application consumes shared `UserCreatedIntegrationEvent`.

## 8. API Architecture

- **CONFIRMED** minimal APIs grouped by static extension methods.
- `AuthEndpoints.MapAuthModule`: public register/login/refresh/logout routes.
- `AdminEndpoints.MapAdminModule`: group-level `.RequireAuthorization()` only.
- `SampleEndpoints.MapSampleModule`: public todo routes.
- Endpoints bind request records/body values, call LinKit `IMediator`, and wrap output through `ApiResults.Ok`.
- `ApiResponse` provides a consistent success/failure envelope and trace identifier.
- Swagger is registered with bearer scheme and mapped only for Development.
- Application-configured RFC 7807 `ProblemDetails`: **NOT_IMPLEMENTED**; no `AddProblemDetails`, `UseExceptionHandler`, or `ValidationProblem` usage exists.
- Custom response-envelope coverage is partial: endpoint successes, application exceptions, and authenticated-session validation failures use `ApiResponse`; source does not customize JWT challenge/forbidden, route-not-found, or minimal-API binding failure payloads.

Evidence:
- File: `src/API/Friday.API/Modules/Auth/AuthEndpoints.cs`; Class: `AuthEndpoints`; Method: `MapAuthModule`
- File: `src/API/Friday.API/Modules/Admin/AdminEndpoints.cs`; Class: `AdminEndpoints`; Method: `MapAdminModule`
- File: `src/API/Friday.API/Modules/Sample/SampleEndpoints.cs`; Class: `SampleEndpoints`; Method: `MapSampleModule`

## 9. CQRS Architecture

- **CONFIRMED** commands and queries are records implementing LinKit interfaces.
- **CONFIRMED** handlers implement `ICommandHandler<,>` / `IQueryHandler<,>` and expose `HandleAsync`.
- **CONFIRMED** API calls `SendAsync` for commands and `QueryAsync` for queries.
- **CONFIRMED** `[CqrsBehavior(typeof(ICommand), 0)]` applies transaction behavior to commands.
- **PARTIALLY_CONFIRMED** handler discovery is source-generator/attribute driven through `AddLinKitCqrs` and `CqrsContext`. `CqrsContext` names Admin Application and BuildingBlocks Application markers but does not name Sample Application, even though Sample endpoints use its handlers. Whether LinKit discovers Sample through another generated path is **UNKNOWN** without inspecting generated code/runtime behavior.
- **NOT_IMPLEMENTED** a dedicated validation behavior.

Evidence:
- File: `src/API/Friday.API/Cqrs/CqrsContext.cs`; Class: `CqrsContext`
- File: `src/BuildingBlocks/Friday.BuildingBlocks.Application/Behaviors/TransactionBehavior.cs`; Class: `TransactionBehavior`; Method: `HandleAsync`
- Representative file: `src/Modules/Admin/Friday.Modules.Admin.Application/Features/Auth/Login.cs`

## 10. Authentication

- **CONFIRMED** JWT bearer is the default authentication scheme.
- Signature, issuer, audience, lifetime and signing key are validated in `Program.cs`.
- Tokens are symmetric HMAC-SHA256 and contain `sub` (user ID), `jti` (session ID), `iat`, and zero or more role claims.
- Default access lifetime is 60 minutes, clamped from 1 minute to 24 hours.
- Refresh tokens are 32 random bytes encoded Base64; only lowercase SHA-256 hashes are persisted.
- Default refresh lifetime is 14 days, clamped from 1 to 365 days.
- Refresh rotates the token/hash and extends expiry on the same session row.
- Logout revokes a matching session and is idempotent.
- Every authenticated request performs database checks for session existence/revocation/expiry/user association and user active/locked state.
- **NOT_IMPLEMENTED** failed-login counting or automatic lockout.
- **NOT_IMPLEMENTED** MFA.
- **NOT_IMPLEMENTED** asymmetric signing/JWKS/OIDC.
- Refresh-token implementation status: **CONFIRMED**. Login creates a persisted session, refresh replaces the hash on that session, logout sets `RevokedAtUtc`, and user locking revokes all open sessions.
- Password verification handles `Failed` versus non-failed results, but `SuccessRehashNeeded` is not used to upgrade the stored hash.
- JWT validation does not explicitly configure `ClockSkew`, claim mapping, or authentication event handlers.
- Password reset changes the stored hash but does not revoke existing sessions. Role/right changes also do not revoke sessions; existing access tokens keep their issued role claims until a later login/refresh.

Evidence:
- File: `src/API/Friday.API/Program.cs`; Method: JWT registration and middleware pipeline
- File: `src/Modules/Admin/Friday.Modules.Admin.Infrastructure/Security/JwtTokenIssuer.cs`; Class: `JwtTokenIssuer`; Method: `CreateAccessToken`
- File: `src/API/Friday.API/Middlewares/AuthenticatedUserValidationMiddleware.cs`; Class: same; Method: `InvokeAsync`

## 11. Authorization

Current authorization is authentication-only for the Admin route group.

Protected endpoint trace (`GET /api/admin/users`):

1. `UseAuthentication` validates bearer token and constructs `ClaimsPrincipal`.
2. `UseAuthorization` evaluates endpoint metadata from group-level `RequireAuthorization()` using the default policy (authenticated user).
3. `AuthenticatedUserValidationMiddleware` queries session and user state.
4. `AdminEndpoints` calls `GetUsersQuery` through mediator.

Classification:

| Mechanism | Status | Behavior |
|---|---|---|
| Authenticated policy | CONFIRMED | Entire Admin group requires an authenticated principal. |
| Role claims | PARTIALLY_CONFIRMED | Active role codes are emitted into JWT, but no endpoint/policy checks them. |
| Rights/permissions model | PARTIALLY_CONFIRMED | Rights and role-right assignments persist and have CRUD, but no request authorization uses them. |
| Named/custom policies | NOT_IMPLEMENTED | None registered. |
| Custom authorization handler | NOT_IMPLEMENTED | None found. |
| Role requirement | NOT_IMPLEMENTED | No `RequireAuthorization` role policy or `RequireRole`/role attribute found. |
| Permission endpoint metadata | NOT_IMPLEMENTED | None found. |
| Database permission lookup | NOT_IMPLEMENTED | Per-request DB lookup checks session/user eligibility, not rights. |

Consequently, any valid active user/session can call user, role, right, password-reset and lock endpoints.

Deep verification notes:

- Authorization executes before `AuthenticatedUserValidationMiddleware`. A validly signed token first satisfies endpoint authorization; the later middleware prevents the endpoint from running if its database session or user state is invalid.
- Only active role **codes** are issued as `ClaimTypes.Role`. Rights are neither loaded during login/refresh nor emitted as claims.
- `AddAuthorization()` registers defaults only. No role requirement, named permission policy, fallback policy, custom handler, resource check, or endpoint-specific authorization metadata exists.
- The current permission concept is named `Right` in code and storage. It is administrative data, not an enforced authorization mechanism.

## 12. Login Flow

Exact flow for `POST /api/auth/login`:

1. **HTTP endpoint**
   - File: `src/API/Friday.API/Modules/Auth/AuthEndpoints.cs`
   - Class/method: `AuthEndpoints.MapAuthModule`, `/login` delegate
   - Input: JSON-bound `LoginCommand(Login, Password)`
   - Output: success `ApiResponse<LoginResponseDto>`
   - Calls: `IMediator.SendAsync`
   - Errors: propagated to global exception middleware
   - Transaction: endpoint itself has none.

2. **Command/handler**
   - File: `src/Modules/Admin/Friday.Modules.Admin.Application/Features/Auth/Login.cs`
   - Class/method: `LoginCommandHandler.HandleAsync`
   - Input/output: `LoginCommand` -> `LoginResponseDto`
   - Calls: user repository, password hasher, role repository, refresh utilities, session repository, JWT issuer
   - Errors: `FridayException` 401 for invalid credentials; 403 for inactive/locked
   - Transaction: LinKit `TransactionBehavior` begins transaction, commits on success, rolls back on exception.

3. **User/password data access**
   - File: `src/Modules/Admin/Friday.Modules.Admin.Infrastructure/Repositories/UserRepository.cs`
   - Class/method: `UserRepository.GetByLoginWithPasswordAsync`
   - Input: login string; matches normalized username, email, or user code
   - Output: `User?` with `UserRoles` and `PasswordCredential`
   - Calls: EF Core `FridayDbContext`
   - Database: `admin.users`, `admin.user_passwords`, `admin.user_roles`.

4. **Password/account validation**
   - Handler calls `PasswordHasher<CredentialUser>.VerifyHashedPassword`.
   - Missing user and incorrect password return the same 401 message.
   - `IsActive` and `IsLocked` are explicitly checked.
   - Failed-attempt tracking/automatic lockout: **NOT_IMPLEMENTED**.

5. **Role loading**
   - File: `src/Modules/Admin/Friday.Modules.Admin.Infrastructure/Repositories/RoleRepository.cs`
   - Method: `GetByIdsAsync`
   - Input: assigned role IDs; output: role entities
   - Handler retains distinct active role codes.
   - Permission/right loading during login: **NOT_IMPLEMENTED**.
   - `RoleRepository.GetByIdsAsync` loads role rows without `RoleRights`; login only needs active role codes.

6. **Refresh session**
   - File: `src/Modules/Admin/Friday.Modules.Admin.Application/Security/RefreshTokenUtilities.cs`
   - Methods: `GenerateOpaqueToken`, `Hash`
   - File: `src/Modules/Admin/Friday.Modules.Admin.Domain/Aggregates/UserAggregate/UserSession.cs`
   - Method: `Create`
   - Creates opaque token, stores its SHA-256 hash with expiry/IP/user-agent through `UserSessionRepository.AddAsync`.

7. **JWT generation**
   - File: `src/Modules/Admin/Friday.Modules.Admin.Infrastructure/Security/JwtTokenIssuer.cs`
   - Class/method: `JwtTokenIssuer.CreateAccessToken`
   - Input: user ID, session ID, active role codes
   - Output: serialized JWT plus UTC expiry
   - Claims: `sub`, `jti`, `iat`, roles
   - Signing: HMAC-SHA256 using configured symmetric secret.

8. **Commit/response**
   - `EfUnitOfWork.CommitAsync` saves the session, dispatches tracked domain events, then commits the relational transaction.
   - Response includes access token, access expiry, raw refresh token and `UserDto`.
   - Login-specific security logging: **NOT_IMPLEMENTED**; normal request logging still applies.
   - The response envelope is `ApiResponse<LoginResponseDto>` with code, message, data and trace ID.

## 13. Database Architecture

- Provider: **CONFIRMED PostgreSQL** in current configuration and generated migration.
- Context: **CONFIRMED one shared `FridayDbContext`**; it exposes entities via `Set<T>()`, not explicit `DbSet<T>` properties.
- Configuration: `ConnectionStrings:FridayDb`; `AdminDatabase` is configured but no code consumer was found.
- Fallback: missing `FridayDb` selects EF InMemory database `Friday.Shared`.
- Naming: explicit lower snake_case tables in `admin` and `localization` schemas; migration columns retain PascalCase property names.
- Transactions: command-wide UoW transaction for relational providers; queries are outside UoW behavior.
- Domain events: dispatched after `SaveChangesAsync` but before DB transaction commit.
- Migrations: EF schema migration `20260405022257_InitPostgres`; FluentMigrator timestamped localization data migrations; optional startup execution.
- Concurrency tokens/row versions: **NOT_IMPLEMENTED**.
- Soft delete/global query filters: **NOT_IMPLEMENTED**.
- Audit fields: `Entity` supplies `CreatedOnUtc` and `UpdatedOnUtc`; `Touch()` is manually invoked by domain operations. `UserSession` has created/revoked timestamps. No actor/audit-log entity exists.
- Repository pattern: **CONFIRMED** domain repository interfaces with EF implementations for Admin; Sample has an in-memory implementation.
- linq2db: factory registered, feature use **NOT_IMPLEMENTED**.
- `admin.user_roles` has a database FK to `admin.users` but no FK to `admin.roles` in the current migration.
- `admin.role_rights` has a database FK to `admin.roles` but no FK to `admin.rights` in the current migration.
- `admin.user_sessions.RefreshTokenHash` is indexed but not unique, and no row-version/concurrency token protects refresh rotation.

Confirmed application tables/entities:

| Schema.table | Entity | Notes |
|---|---|---|
| `admin.users` | `User` | Unique user code, username, email. |
| `admin.user_passwords` | `UserPassword` | One-to-one password hash row. |
| `admin.user_sessions` | `UserSession` | Refresh hash, expiry, revocation, IP/user-agent; indexed hash and user ID. |
| `admin.roles` | `Role` | Unique code. |
| `admin.rights` | `Right` | Unique code. |
| `admin.user_roles` | `UserRole` | Composite key join. |
| `admin.role_rights` | `RoleRight` | Composite key join. |
| `localization.error_messages` | `ErrorLocalizationMessage` | Unique module/error-code/language. |

Infrastructure-owned migration history/version tables also exist when migrations run, but their exact deployed names depend on EF/FluentMigrator configuration.

## 14. Configuration

- JSON configuration files: base, Development, Docker.
- Options: `JwtSettings`, `RegistrationOptions`, `DatabaseOptions`, `CacheOptions`, `OpenTelemetryOptions`, `LocalizationOptions`.
- Binding uses `Configure<T>` and direct startup binding for JWT validation.
- Environment variables override nested configuration in Compose.
- `Authentication:AllowPublicRegistration` is false in base but true in Development and Docker; handler enforces it.
- `Database:ApplyMigrationsOnStartup` is false in base/development and true in Docker.
- Redis is disabled in base/development and enabled in Docker.

## 15. Middleware

Confirmed order:

1. `CorrelationIdMiddleware`
2. Serilog request logging
3. `ExceptionHandlingMiddleware`
4. Development Swagger/UI
5. HTTPS redirection
6. Authentication
7. Authorization
8. `AuthenticatedUserValidationMiddleware`
9. Mapped endpoints

Exception handling maps `FridayException`, `KeyNotFoundException`, `ArgumentException`, `InvalidOperationException`, and unknown exceptions to the API failure envelope and localized messages. It logs the exception and its message at Error.

- `CorrelationIdMiddleware` accepts the first request `X-Correlation-Id` value, echoes it in the response, and adds it to the Serilog log context. It does not validate or normalize the supplied value.
- API response `TraceId` uses `Activity.Current.TraceId` or `HttpContext.TraceIdentifier`; it is not necessarily the same as a client-supplied correlation ID.
- Serilog request logging enriches host, scheme, user agent, and trace ID. It logs exceptions/5xx at Error, requests slower than 500 ms at Warning, and other requests at Information.
- Authentication/authorization challenge response customization and status-code pages: **NOT_IMPLEMENTED** in source; exact framework-generated payloads are **UNKNOWN** without runtime verification.

## 16. Docker / Deployment

- **CONFIRMED** multi-stage .NET 10 SDK/ASP.NET runtime Dockerfile.
- **CONFIRMED** Compose services: PostgreSQL 16 Alpine, Redis 7 Alpine, Jaeger all-in-one, Friday API.
- API listens on HTTP port 8080; Compose waits for PostgreSQL health.
- PostgreSQL/Redis persistent named volumes exist.
- Docker enables startup migrations, Redis caching, public registration, and OTLP export to Jaeger.
- Container runs as image default user; explicit non-root user: **NOT_IMPLEMENTED**.
- API health/liveness/readiness endpoints: **NOT_IMPLEMENTED**.
- CI/CD workflows: **NOT_IMPLEMENTED** (none found in inspected solution/repository structure).
- Metrics backend/dashboard/log aggregation: **PARTIALLY_CONFIRMED** instrumentation/export exists, but Compose includes only Jaeger; Prometheus/Grafana/Loki are absent.

Evidence:
- File: `docker/Dockerfile`
- File: `docker-compose.yml`
- File: `src/API/Friday.API/Configuration/OpenTelemetryServiceCollectionExtensions.cs`

## 17. Testing

- Test projects/source: **NOT_IMPLEMENTED**.
- Unit tests: **NOT_IMPLEMENTED**.
- Integration/database tests: **NOT_IMPLEMENTED**.
- Authentication/authorization security tests: **NOT_IMPLEMENTED**.
- Architecture/contract/E2E/performance tests: **NOT_IMPLEMENTED**.
- Build/runtime verification in this discovery: **NOT_RUN**, because the task requested source discovery and the environment is read-only; conclusions are static-code/configuration evidence.

## 18. Security Baseline

Only confirmed findings are listed.

| Severity | Finding | Evidence |
|---|---|---|
| INFO | Passwords are stored as ASP.NET Identity password hashes, not plaintext. | `Admin.Infrastructure/DependencyInjection.cs`; `UserPassword.cs`; login/register/reset handlers. |
| INFO | Refresh tokens are generated cryptographically, stored only as SHA-256 hashes, rotated on refresh, and revocable. | `RefreshTokenUtilities.cs`; `RefreshToken.cs`; `UserSession.cs`. |
| INFO | JWT validates signature, issuer, audience and lifetime; secret must be at least 32 characters. | `Program.cs`; `JwtTokenIssuer.cs`. |
| INFO | Invalid user and invalid password share one login error, reducing basic account enumeration. | `Login.cs`. |
| MEDIUM | All authenticated users can invoke every Admin endpoint, including password reset, user lock, role and right mutation. Roles are claims and rights are stored, but neither is enforced. | `AdminEndpoints.cs` group has only `.RequireAuthorization()`; no policies/handlers found. |
| HIGH | JWT signing secrets and database credentials are committed in base/Docker configuration and Compose. Labels say development/change-me, but executable defaults use them. | `appsettings.json`; `docker-compose.yml`; `DesignTimeDbContextFactory.cs`. |
| MEDIUM | Docker environment enables public registration while also using a committed JWT secret and credentials; this is unsafe if the Compose deployment is exposed beyond controlled development. | `appsettings.Docker.json`; `docker-compose.yml`; `Register.cs`. |
| MEDIUM | Login has no failed-attempt counter, throttling, or automatic lockout. An administrative lock flag exists but is not driven by failed authentication. | `Login.cs`; `User.cs`; no rate-limiter registration found. |
| LOW | Refresh and logout endpoints are anonymous. Possession of a refresh token is the credential; logout silently succeeds. This is intentional-looking behavior but has no endpoint rate limiting. | `AuthEndpoints.cs`; `RefreshToken.cs`; `Logout.cs`. |
| LOW | Exception middleware logs `exception.Message`; several exceptions include user/domain validation details. No password/token logging was found, but broad message logging increases future leakage risk. | `ExceptionHandlingMiddleware.InvokeAsync`. |
| LOW | JWT is symmetric and has no configured key rotation/key identifier mechanism. | `JwtTokenIssuer.cs`; `JwtSettings.cs`. |

- Password/token values in explicit log templates: **NOT_IMPLEMENTED** / not found.
- Deny-by-default authorization fallback policy: **NOT_IMPLEMENTED**; public routes remain public unless endpoint metadata requires authorization.
- Security audit log: **NOT_IMPLEMENTED**; domain notification handlers provide informational logs for some events, not durable security audit evidence.

## 19. Current Coding Conventions

- Namespaces mirror project and folder: `Friday.Modules.Admin.Application.Features.Auth`, etc.
- Types use PascalCase; locals/parameters camelCase; interfaces use `I` prefix.
- File-scoped namespaces and primary constructors are common.
- Feature files usually colocate request record and handler (`Login.cs`, `CreateRole.cs`).
- DTOs are immutable records with `Dto` suffix; endpoint-specific body types use `Request` suffix.
- Commands use `*Command`; queries use `*Query`; handlers vary between `*CommandHandler` and shorter `*Handler`.
- Domain aggregates use private setters/private constructors and static `Create` factories.
- Domain/application validation uses guard clauses, `ArgumentException`, or `FridayException`; no validator classes/pipeline.
- DI uses project-level `DependencyInjection` extension methods; API is the composition root.
- Logging is structured with named placeholders and Serilog enrichment.
- Errors are exception-based with centralized HTTP mapping and stable error-code constants.
- Success/failure HTTP envelopes use `ApiResponse`/`ApiResults`; no generic Result monad.
- Async repository and handler APIs consistently accept `CancellationToken`; EF calls propagate it.
- Read repositories generally use EF LINQ; `AsNoTracking` is not used in inspected read queries.
- EF mappings use `IEntityTypeConfiguration<T>` and explicit table/schema/index/length rules.
- Configuration uses options classes and named section constants.
- Time uses direct `DateTime.UtcNow` / `DateTimeOffset.UtcNow`; no testable clock abstraction.

## 20. Confirmed Technical Debt

This section records current limitations, not future architecture decisions.

1. Rights and roles are modeled but not enforced; sensitive Admin endpoints are authentication-only.
2. Secrets and database credentials exist in tracked executable configuration.
3. No automated tests exist for login, refresh rotation, authorization, transactions, migrations, or module boundaries.
4. Shared `FridayDbContext` and hard-coded module configuration assembly names couple module persistence to BuildingBlocks Infrastructure.
5. Admin-specific error codes and `UserCreatedIntegrationEvent` live in shared BuildingBlocks Application.
6. Integration events are in-process only; there is no durable broker/outbox/inbox.
7. Command domain events are saved and dispatched before transaction commit; handler side effects could occur before commit and have no durable delivery.
8. No rate limiting or automated login lockout.
9. No concurrency tokens/strategy in current persistence entities.
10. Sample repository uses an unsynchronized static `List<T>`; concurrent access durability/consistency is not guaranteed.
11. `CqrsContext` does not explicitly include Sample Application despite mapped Sample handlers; runtime registration needs verification.
12. Startup database migration is enabled in Docker and performed inside API startup rather than a separate deployment step.

## 21. Unknown / Requires Investigation

- **UNKNOWN** LinKit source-generated registrations and exact handler/behavior resolution order without inspecting generated `obj` artifacts or running the host.
- **UNKNOWN** whether Sample CQRS endpoints resolve successfully at runtime.
- **UNKNOWN** actual deployed database contents and whether all migrations have been applied.
- **UNKNOWN** production configuration/secrets source; only repository defaults were inspected.
- **UNKNOWN** intended semantics and authorization mapping of right codes.
- **UNKNOWN** operational deployment outside local Docker Compose.
- **UNKNOWN** whether an external reverse proxy supplies HTTPS, rate limiting, security headers, or health checks.
- **UNKNOWN** password hasher compatibility/version strategy across deployments; default Identity settings are used.
- **UNKNOWN** retention requirements for user sessions and logs.
- **UNKNOWN** behavior under concurrent refresh requests against the same session because no concurrency token/test exists.

## 22. Recommended Next Exploration

The five most valuable next discovery/verification targets are:

1. Run the host and verify register -> login -> authenticated Admin request -> refresh rotation -> logout, including failure responses.
2. Inspect/execute LinKit generated registrations to confirm Sample handler discovery and transaction behavior ordering.
3. Exercise role/right assignment and define the intended endpoint-to-permission mapping before any authorization implementation.
4. Apply migrations to an isolated PostgreSQL instance and inspect constraints, migration ordering, localization seeds, and repeat startup behavior.
5. Trace all user-administration mutation flows (create/reset password/lock/assign role) for authorization, session revocation, audit logging, and transaction semantics.
