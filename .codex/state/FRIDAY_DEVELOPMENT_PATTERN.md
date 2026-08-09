# Friday Current Development Pattern

Scope: a consolidated description of development conventions already confirmed in `PROJECT_MAP.md`, `ADMIN_BASELINE.md`, `MODULE_PATTERN.md`, `PERSISTENCE_PATTERN.md`, `API_PATTERN.md`, `CONFIGURATION_PATTERN.md`, and `TEST_PATTERN.md`. Admin is the primary reference because it is the only fully persisted feature module.

This document describes the current repository mechanism. It does not approve the mechanism, introduce a future architecture, or make new architecture decisions.

Status vocabulary: **CONFIRMED**, **PARTIAL**, **NOT_IMPLEMENTED**, **UNKNOWN**.

## 1. Architecture Context

Friday is a **CONFIRMED hybrid modular monolith**:

- one ASP.NET Core process and composition root;
- Minimal API endpoint groups;
- module projects split into Domain, Application, and Infrastructure;
- inward project-reference direction inside each module;
- feature-oriented Application files;
- LinKit-based CQRS;
- one shared `FridayDbContext`, connection, migration set, and Unit of Work;
- in-process domain/integration notification dispatch.

The current project-reference pattern is:

```text
Friday.API
  -> Module.Application
  -> Module.Infrastructure

Module.Infrastructure
  -> Module.Application
  -> Module.Domain
  -> BuildingBlocks.Infrastructure

Module.Application
  -> Module.Domain
  -> BuildingBlocks.Application

Module.Domain
  -> BuildingBlocks.Domain
```

Evidence: `Friday.slnx`, `src/src.sln`, and all module `*.csproj` files as indexed by `PROJECT_MAP.md`.

No Domain-to-Infrastructure or Admin-to-Sample project reference exists. Module persistence is nevertheless coupled through the shared DbContext and hard-coded infrastructure assembly discovery.

## 2. De Facto Module Shape

The closest confirmed full module reference is Admin:

```text
src/Modules/<Module>/
  Friday.Modules.<Module>.Domain/
  Friday.Modules.<Module>.Application/
  Friday.Modules.<Module>.Infrastructure/

src/API/Friday.API/Modules/<Module>/
  <Module>Endpoints.cs
```

### Domain project

Confirmed Admin responsibility:

- aggregate roots and supporting entities;
- aggregate behavior and guard clauses;
- domain events;
- entity-specific repository interfaces;
- domain-facing technical marker types where currently needed.

Project dependency: module Domain to `Friday.BuildingBlocks.Domain`.

### Application project

Confirmed Admin responsibility:

- commands, queries, and handlers;
- use-case orchestration;
- inline application validation;
- DTO mapping;
- application ports and settings;
- notification handlers;
- stable application-error selection.

Project dependencies: module Application to its Domain and `Friday.BuildingBlocks.Application`.

### Infrastructure project

Confirmed Admin responsibility:

- repository implementations;
- EF Core entity configurations;
- technical service implementations;
- module Infrastructure dependency registration.

Project dependencies: module Infrastructure to its Application, Domain, and `Friday.BuildingBlocks.Infrastructure`.

### API exposure

Confirmed responsibility:

- route groups and transport binding;
- endpoint authorization metadata;
- command/query dispatch;
- HTTP response wrapping.

The API directly references module Application and Infrastructure and invokes module registration methods from `Program.cs`.

## 3. Current Module-Wiring Mechanism

This section describes wiring required by the current implementation, not a preferred future module bootstrap design.

### Solution and project references

Admin and Sample each have Domain, Application, and Infrastructure projects listed in both solutions. `Friday.API` references their Application and Infrastructure projects.

### Dependency-injection extensions

Each layer exposes a static `DependencyInjection` class with an `Add<Module>Application` or `Add<Module>Infrastructure` extension.

- Admin Application registration currently returns `IServiceCollection` unchanged.
- Admin Infrastructure registers JWT/security services and scoped repositories.
- Sample Application registration is also a no-op.
- Sample Infrastructure registers its repository.

Concrete service registrations belong in Infrastructure in the existing examples. The API composition root calls the extensions explicitly.

### CQRS discovery

- File: `src/API/Friday.API/Cqrs/CqrsContext.cs`
- Type: `CqrsContext`
- Current attribute includes `AdminApplicationAssemblyMarker` and `BuildingBlockApplicationMarker`.
- `Program.cs` calls external/generated `AddLinKitCqrs()`.

Admin handlers are discovered through the Application assembly marker rather than registered by `AddAdminApplication`.

Sample Application is not explicitly listed in the confirmed `CqrsContext`, despite mapped Sample handlers. Therefore broadening this into a universal automatic module-discovery convention would be unsupported. Exact generated LinKit behavior is **UNKNOWN**.

### EF configuration discovery

- File: `src/BuildingBlocks/Friday.BuildingBlocks.Infrastructure/Persistence/FridayDbContext.cs`
- Type: `FridayDbContext`
- Current behavior: applies configurations from BuildingBlocks Infrastructure and hard-coded Admin/Sample Infrastructure assembly names.

A persisted module does not own a module DbContext in the current implementation. Its mappings must be visible to this shared model-building mechanism. This coupling is confirmed technical debt, not a new convention recommendation.

### API endpoint mapping

Static endpoint extensions such as `MapAdminModule` are invoked explicitly from `Program.cs`. There is no automatic module endpoint discovery.

## 4. End-to-End Feature Pattern

The confirmed normal command flow is:

```text
Minimal API endpoint
  -> IMediator.SendAsync(Command, CancellationToken)
  -> TransactionBehavior.BeginTransactionAsync
  -> ICommandHandler.HandleAsync
  -> domain repository interface
  -> aggregate factory/methods
  -> Infrastructure repository
  -> shared scoped FridayDbContext change tracker
  -> TransactionBehavior.CommitAsync
  -> FridayDbContext.SaveChangesAsync
  -> in-process domain event dispatch
  -> relational transaction commit
  -> DTO or primitive
  -> ApiResults.Ok
  -> ApiResponse<T>
```

The confirmed normal query flow is:

```text
Minimal API endpoint
  -> IMediator.QueryAsync(Query, CancellationToken)
  -> IQueryHandler.HandleAsync
  -> domain repository interface
  -> Infrastructure EF query through FridayDbContext
  -> tracked domain entity/entities
  -> handler maps to DTO
  -> ApiResults.Ok
  -> ApiResponse<T>
```

Failure flow:

```text
handler/domain/infrastructure exception
  -> command rollback when command behavior is active
  -> ExceptionHandlingMiddleware
  -> error-code/status mapping
  -> localized message lookup
  -> Error log
  -> ApiResponse failure envelope
```

## 5. API Convention

Status: **CONFIRMED Minimal API**.

- Controllers, `AddControllers`, and `MapControllers`: **NOT_IMPLEMENTED**.
- Each API module uses a static endpoint class and an `IEndpointRouteBuilder` extension such as `MapAdminModule`.
- Endpoints are grouped under paths such as `/api/auth`, `/api/admin`, and `/api/sample` and tagged for OpenAPI.
- Group-level `.RequireAuthorization()` is used by Admin.
- Endpoint delegates bind body/route values, inject `IMediator` and `CancellationToken`, dispatch, then call `ApiResults.Ok`.
- Endpoints do not directly resolve repositories or `FridayDbContext`.
- Commands use `mediator.SendAsync`; queries use `mediator.QueryAsync`.

Observed endpoint response behavior:

- successful application values are returned as HTTP 200 `ApiResponse<T>`;
- caught application/domain exceptions use non-generic `ApiResponse` failure envelopes;
- exact framework binding, JWT challenge/forbidden, 404, and 405 response formats are not standardized by application code.

## 6. Command Convention

Status: **CONFIRMED**.

- Commands are immutable records with a `Command` suffix.
- They directly implement external `LinKit.Core.Cqrs.ICommand<TResponse>`; Friday has no local wrapper.
- Commands and their handlers normally share one feature file.
- Response types are DTOs, read-only collections, or primitives rather than `Result<T>`.
- Commands may read and mutate domain state.
- Commands are selected by `TransactionBehavior<TRequest,TResponse>` and normally receive begin/commit/rollback behavior automatically.
- Cancellation tokens are passed from endpoint to mediator, handler, repository, and EF operation.

Examples:

- `LoginCommand : ICommand<LoginResponseDto>`
- `CreateUserCommand : ICommand<UserDto>`
- `AssignRoleToUserCommand : ICommand<UserDto>`
- `LogoutCommand : ICommand<bool>`

Normal handlers do not call `SaveChangesAsync` or explicitly commit. `RegisterCommandHandler` is a confirmed exception: it explicitly commits, then dispatches a nested `LoginCommand`. That split transaction is not the normal feature pattern.

## 7. Query Convention

Status: **CONFIRMED**.

- Queries are immutable records with a `Query` suffix.
- They implement external `IQuery<TResponse>`.
- Query and handler normally share one feature file.
- Queries read through domain repository interfaces.
- Handlers map returned domain entities to Application DTOs.
- Queries are not wrapped by the command transaction behavior.
- Queries may throw `FridayException` for expected failures such as not-found.

Examples:

- `GetUserByIdQuery : IQuery<UserDto>`
- `GetUsersQuery : IQuery<IReadOnlyList<UserDto>>`
- `GetRolesQuery`
- `GetRightsQuery`

Current Admin reads return tracked EF entities. `AsNoTracking`, SQL-level DTO projection, and a separate read-store pattern are **NOT_IMPLEMENTED**.

## 8. Handler Convention

Status: **CONFIRMED with naming variation**.

- Location: `Friday.Modules.<Module>.Application/Features/<Area>/`.
- Method: `Task<TResponse> HandleAsync(TRequest request, CancellationToken cancellationToken)`.
- Interfaces: external `ICommandHandler<TCommand,TResponse>` or `IQueryHandler<TQuery,TResponse>`.
- Primary-constructor dependency injection is common.
- Handlers orchestrate repositories, domain behavior, application checks, security/technical ports, event publication, and DTO mapping.
- Handlers do not contain EF Core queries directly in the Admin reference.
- Normal command handlers defer persistence to the global transaction behavior.

Handler suffixes are not uniform:

- `LoginCommandHandler` and `RegisterCommandHandler` use `CommandHandler`.
- `CreateUserHandler` and `GetUserByIdHandler` use the shorter `Handler` form.

Therefore either form is existing code, but no single handler naming suffix can be claimed as a repository-wide enforced standard.

## 9. Domain Convention

Status: **CONFIRMED for Admin aggregates**.

- Aggregate roots such as `User`, `Role`, and `Right` inherit `AggregateRoot`.
- Shared `Entity` supplies integer ID, `CreatedOnUtc`, `UpdatedOnUtc`, and an in-memory domain-event collection.
- Aggregate state uses private setters and private constructors.
- Static `Create` factories establish initial valid state.
- Public domain methods perform state changes, update timestamps, and may raise domain events.
- Guard clauses throw `ArgumentException` or framework guard exceptions for invalid domain input/state.
- Domain events are immutable notification records implementing shared `IDomainEvent` and include `OccurredOnUtc`.
- Join/dependent entities may be standalone types and do not necessarily inherit shared `Entity`.

Repository ports are entity/aggregate-specific interfaces in the module Domain project. A generic repository abstraction is **NOT_IMPLEMENTED**.

No domain Result type, testable clock abstraction, soft-delete base, or optimistic-concurrency base convention exists.

## 10. Infrastructure Convention

Status: **CONFIRMED for Admin**.

- Repository implementations live under `Friday.Modules.<Module>.Infrastructure/Repositories`.
- Technical implementations such as JWT issuance live under focused Infrastructure folders.
- Repositories implement module Domain interfaces and receive the shared scoped `FridayDbContext` through constructor injection.
- Repository types are registered as scoped services in `Add<Module>Infrastructure`.
- `AddAsync` attaches entities but does not call `SaveChangesAsync`.
- Updates generally mutate tracked entities; explicit repository `Update` methods are not the established Admin pattern.
- Reads use EF LINQ and asynchronous terminal operations.
- Includes are selected per repository method.
- Dapper/linq2db is not used by Admin repositories.

## 11. DbContext and Persistence Convention

Status: **CONFIRMED shared persistence model**.

- DbContext: `FridayDbContext` in BuildingBlocks Infrastructure.
- Lifetime: scoped through `AddDbContext`.
- Default/configured provider: PostgreSQL.
- Module-specific DbContext: **NOT_IMPLEMENTED**.
- Module-specific physical migration ownership: **NOT_IMPLEMENTED**.
- Admin uses logical schema `admin` within the shared model/migration.

Normal command persistence:

1. `TransactionBehavior` calls `IUnitOfWork.BeginTransactionAsync`.
2. Handler/repository changes tracked entities.
3. Behavior calls `IUnitOfWork.CommitAsync`.
4. `EfUnitOfWork` calls `FridayDbContext.SaveChangesAsync`.
5. Domain events on tracked BuildingBlocks entities are published sequentially.
6. Owned relational transaction commits.

Failure causes rollback and rethrow. Queries do not use this explicit transaction behavior.

Domain/integration event dispatch is in-process and non-durable. Outbox, inbox, broker delivery, and retries are **NOT_IMPLEMENTED**.

## 12. DTO Convention

Status: **CONFIRMED**.

- DTOs are immutable records with a `Dto` suffix.
- Module DTOs live under `Friday.Modules.<Module>.Application/Models`.
- Examples: `UserDto`, `RoleDto`, `RightDto`, `LoginResponseDto`.
- Feature-specific request bodies that are not CQRS request objects use a `Request` suffix at the API boundary.
- Handlers manually map domain entities to DTOs.
- AutoMapper or another mapping framework is **NOT_IMPLEMENTED**.
- Persistence entities are not returned directly by the documented Admin endpoints.

## 13. Naming Convention

| Element | Confirmed pattern |
|---|---|
| Projects | `Friday.Modules.<Module>.<Layer>` |
| API endpoint class | `<ModuleOrArea>Endpoints` |
| Endpoint registration method | `Map<ModuleOrArea>Module` |
| Command | `<Action><Subject>Command` |
| Query | `Get<Subject>Query`, `Get<Subject>ByIdQuery` |
| Handler | `<RequestBase>Handler` or `<RequestBase>CommandHandler`; inconsistent |
| DTO | `<Subject>Dto`, specialized response such as `LoginResponseDto` |
| API-only body | `<Action><Subject>Request` |
| Repository interface | `I<Aggregate>Repository` |
| Repository implementation | `<Aggregate>Repository` |
| EF mapping | `<Entity>Configuration` |
| Domain event | `<PastTenseEvent>DomainEvent` |
| DI method | `Add<Module>Application`, `Add<Module>Infrastructure` |
| Assembly marker | `<Module>ApplicationAssemblyMarker` |

Types use PascalCase, locals/parameters use camelCase, and interfaces use the `I` prefix.

## 14. Namespace and Folder Convention

Status: **CONFIRMED**.

- File-scoped namespaces are common.
- Namespaces mirror project and folder location.
- Representative Application namespace: `Friday.Modules.Admin.Application.Features.Auth`.
- Representative repository namespace: `Friday.Modules.Admin.Infrastructure.Repositories`.
- Representative API namespace: `Friday.API.Modules.Admin`.
- Application features are grouped by business area under `Features/<Area>`.
- Request and handler are usually colocated in one feature file named for the use case, such as `Login.cs`, `CreateUser.cs`, or `GetUsers.cs`.
- DTOs are separated under `Application/Models`.
- Domain aggregates are grouped under `Domain/Aggregates/<Aggregate>Aggregate`.
- Repository ports live under `Domain/Repositories`.
- EF mappings live under `Infrastructure/Persistence/Configurations`.

## 15. Validator Convention

Status: **NOT_IMPLEMENTED**.

Friday has no confirmed validator class or validation pipeline convention:

- no FluentValidation package/use;
- no `IValidator<T>` or `AbstractValidator<T>`;
- no validation CQRS behavior;
- no Minimal API validation filter;
- no structured multi-field validation error response.

Current validation is distributed across:

1. Minimal API framework binding;
2. inline checks in Application handlers;
3. domain factory/method guard clauses;
4. database constraints.

Application checks usually throw `FridayException`; domain guards generally throw `ArgumentException`. A new validator pattern cannot be derived from current source.

## 16. Entity Convention

Status: **CONFIRMED with multiple entity shapes**.

Aggregate-root pattern:

- inherit shared `AggregateRoot`;
- integer key inherited from `Entity`;
- UTC audit fields inherited from `Entity`;
- private construction/state mutation;
- static creation factory;
- aggregate methods enforce behavior;
- in-memory domain events raised through the base entity.

Supporting entity pattern:

- join types use explicit foreign-key properties and often composite keys;
- dependents such as `UserPassword` use their parent key;
- session-like types may use GUID identity and may not inherit `Entity`.

There is no single base class used by every persisted type. Soft delete, concurrency tokens, automatic audit interception, and universal database-generated ID conventions are **NOT_IMPLEMENTED**.

## 17. EF Configuration Convention

Status: **CONFIRMED**.

- One `<Entity>Configuration` class per entity.
- Each implements `IEntityTypeConfiguration<TEntity>`.
- Location: module Infrastructure `Persistence/Configurations`.
- Table/schema names are explicitly configured.
- Admin tables use lowercase snake_case under schema `admin`.
- CLR-generated column names remain PascalCase in the current migration.
- Keys, composite keys, requiredness, maximum lengths, unique indexes, and relationships are configured explicitly where present.
- Domain-event properties are ignored for aggregate entities.
- Main UTC timestamps map to PostgreSQL `timestamp with time zone` in the checked-in migration.
- Business identifiers such as user code, username, email, role code, and right code use unique indexes.

Current gaps are part of the observed pattern boundary: some join-side foreign keys are missing, and check constraints, soft-delete filters, and optimistic concurrency tokens are not implemented.

Schema migrations and the model snapshot currently live in BuildingBlocks Infrastructure, not in the Admin module. FluentMigrator data migrations also live there and are used for localization data.

## 18. Dependency-Injection Convention

Status: **CONFIRMED explicit extension-method registration**.

- `Friday.API/Program.cs` is the composition root.
- Each project exposes a static `DependencyInjection` extension class.
- Extension methods return `IServiceCollection` to allow chaining.
- Infrastructure extensions accept `IConfiguration` when configuration binding is required.
- Repositories, Unit of Work, DbContext, dispatcher, and password hasher are scoped.
- Cache and JWT issuer implementations are singleton in current registrations.
- Options use named section constants plus `Configure<T>` or direct startup binding.
- CQRS handlers/behaviors are discovered through external LinKit generation/registration rather than explicit per-handler registrations.
- Custom middleware is added with conventional `UseMiddleware<T>` and is not registered as `IMiddleware`.

Application registration extensions exist as module conventions but are currently no-ops for Admin and Sample.

## 19. Error and Result Convention

### Application result

Generic `Result`/`Result<T>`: **NOT_IMPLEMENTED**.

Handlers return successful values directly and represent expected failures with exceptions.

### Expected application errors

- Type: `FridayException`.
- Carries stable error code, public/fallback message, and HTTP status.
- Stable constants are stored in `ErrorCodes`.
- Current `ErrorCodes` includes Admin-specific vocabulary in shared BuildingBlocks Application.

### Domain errors

- Domain guard failures use `ArgumentException` or guard exceptions.
- Dedicated domain-error object hierarchy: **NOT_IMPLEMENTED**.

### HTTP mapping

`ExceptionHandlingMiddleware` maps:

| Exception | Response behavior |
|---|---|
| `FridayException` | Uses exception status and code. |
| `KeyNotFoundException` | 404 / `NOT_FOUND`. |
| `ArgumentException` | 400 / `BAD_REQUEST`. |
| `InvalidOperationException` | 400 / `BAD_REQUEST`. |
| Other exception | 500 / `INTERNAL_SERVER_ERROR` with generic public message. |

Messages are localized through `IErrorMessageLocalizer`. Application-created success/failure bodies use `ApiResponse<T>`/`ApiResponse` with `Code`, `Message`, `Data`, and `TraceId`.

RFC 7807 `ProblemDetails` is **NOT_IMPLEMENTED**.

## 20. Logging Convention

Status: **CONFIRMED structured logging**.

- Host logging uses Serilog.
- Log templates use named structured placeholders.
- `Application=Friday.API` is enriched globally.
- Request logging enriches `RequestHost`, `RequestScheme`, `UserAgent`, and `TraceId`.
- `CorrelationIdMiddleware` adds `CorrelationId` to downstream log context and response headers.
- Exception middleware logs every caught exception at Error, including expected application 4xx exceptions.
- Some Application notification handlers inject `ILogger<T>` and emit informational domain/integration-event messages.
- Secrets/password/token values were not found in explicit log templates.

There is no feature-specific logging interface or mandatory per-handler logging pattern. Security audit logging is **NOT_IMPLEMENTED**.

OpenTelemetry instruments ASP.NET Core, HTTP clients, and runtime metrics when enabled; this is host observability rather than a feature-handler convention.

## 21. Test Convention

Status: **NOT_IMPLEMENTED**.

No automated testing pattern can be derived:

- no test projects in either solution;
- no test SDK/framework;
- no unit or integration tests;
- no database fixture strategy;
- no `WebApplicationFactory` usage;
- no mocking library or test doubles in test context;
- no test naming convention;
- no authentication test setup;
- no test-data builders/fixtures;
- no Login/Admin tests;
- no CI `dotnet test` or coverage execution.

`Friday.API.http` is only a manual, assertion-free request aid and currently targets an unmapped `/weatherforecast/` route. It does not establish a test convention.

Therefore this document does not specify how a new feature should be tested; doing so would introduce a new decision not confirmed by the repository.

## 22. Consolidated Current Feature Reference

For a normal persisted Admin-style command, the current repository pattern is:

1. Define aggregate behavior and an entity-specific repository interface in Module Domain.
2. Define an immutable `ICommand<TResponse>` record and its handler together under Application `Features/<Area>`.
3. Perform use-case checks inline in the handler and use aggregate factories/methods for domain changes.
4. Throw `FridayException` for expected application failures and return an Application DTO/primitive on success.
5. Implement the repository and EF mapping in Module Infrastructure using the shared `FridayDbContext`.
6. Register repository/technical implementations in `Add<Module>Infrastructure`.
7. Ensure current explicit CQRS/EF/module composition mechanisms include the module assembly where applicable.
8. Map a thin Minimal API endpoint that binds input, dispatches through `IMediator`, and wraps success through `ApiResults.Ok`.
9. Rely on `TransactionBehavior` and `EfUnitOfWork` for the normal command save/rollback path.
10. Let centralized middleware map thrown failures to localized API envelopes and request logging.

For a normal query:

1. Define an immutable `IQuery<TResponse>` and colocated handler.
2. Read through the Domain repository interface.
3. Let Infrastructure issue the EF query through the shared context.
4. Map tracked entities to DTOs in the handler.
5. Dispatch from a thin API endpoint using `QueryAsync` and wrap through `ApiResults.Ok`.

These steps summarize existing Admin mechanics. They are not a commitment that future modules must retain the shared DbContext, exception-based errors, external CQRS coupling, missing validation pipeline, or absent tests.

## 23. Confirmed Exceptions and Boundaries

The following must not be mistaken for clean universal conventions:

1. Admin is the only full persisted reference; Sample is an in-memory demonstration module.
2. Handler suffixes are inconsistent.
3. `RegisterCommandHandler` manually commits and nests Login, unlike normal commands.
4. Application and Domain layers have some ASP.NET Core/LinKit coupling.
5. Shared BuildingBlocks contains Admin-specific error/event vocabulary.
6. Shared Infrastructure knows concrete module assemblies.
7. All modules share one DbContext/UoW/migration boundary.
8. Queries are tracked and entity-based; no read-model convention exists.
9. Validation is inline/distributed; validators do not exist.
10. Failures are exception-based; `Result<T>` does not exist.
11. Events are in-process and non-durable.
12. Tests and a testing convention do not exist.
13. Rights/permissions are modeled but not enforced by current Admin endpoint authorization.

## 24. Evidence Index

Primary consolidated sources:

- `.codex/state/PROJECT_MAP.md` - project boundaries, dependencies, architecture, conventions.
- `.codex/state/ADMIN_BASELINE.md` - Login/Admin reference flow and security objects.
- `.codex/state/MODULE_PATTERN.md` - CQRS, handlers, validation, errors, events, and layer responsibilities.
- `.codex/state/PERSISTENCE_PATTERN.md` - DbContext, repositories, mappings, transactions, saves, and migrations.
- `.codex/state/API_PATTERN.md` - endpoints, middleware, envelopes, logging, and request flow.
- `.codex/state/CONFIGURATION_PATTERN.md` - composition root, service lifetimes, module registration, and options.
- `.codex/state/TEST_PATTERN.md` - confirmed absence of automated test conventions.
