# Friday CQRS and BuildingBlocks Module Pattern

Scope: current CQRS and BuildingBlocks architecture, using the Admin module as the reference implementation. This is an evidence-based description of existing code, not a proposed target architecture. Unrelated module functionality and generated `bin/`/`obj/` artifacts were not inspected.

Status vocabulary: **CONFIRMED**, **PARTIAL**, **NOT_IMPLEMENTED**, **UNKNOWN**.

## 1. Architecture Summary

Friday currently uses a hybrid modular-monolith pattern:

```text
Friday.API (composition root and HTTP adapter)
  -> Admin.Application (commands, queries, handlers, DTOs, application ports)
      -> Admin.Domain (aggregates, domain events, repository ports)
          -> BuildingBlocks.Domain
      -> BuildingBlocks.Application
  -> Admin.Infrastructure (repository/security implementations and EF mappings)
      -> Admin.Application + Admin.Domain + BuildingBlocks.Infrastructure
  -> BuildingBlocks.Infrastructure
      -> BuildingBlocks.Application + BuildingBlocks.Domain
```

CQRS is **CONFIRMED**, implemented with external `LinKit.Core.Cqrs`. Friday does not define local `ICommand`, `IQuery`, handler, mediator, notification, or pipeline interfaces. The Admin Application organizes request records and handlers by feature, while API minimal endpoints dispatch them through `IMediator`.

The layer dependency direction is inward at project-reference level, but boundaries are not strict Clean Architecture:

- Admin Application uses ASP.NET Core HTTP status constants, `IHttpContextAccessor`, and ASP.NET Identity abstractions.
- BuildingBlocks Domain references LinKit and the ASP.NET Core shared framework.
- BuildingBlocks Application contains Admin-specific error codes and a user-specific integration event.
- BuildingBlocks Infrastructure owns one shared `FridayDbContext` and hard-codes module infrastructure assembly names.

## 2. ICommand and IQuery

### Source and ownership

- Package: `LinKit.Core` 2.2.3, centrally versioned in `src/Directory.Packages.props`.
- Namespace: `LinKit.Core.Cqrs`.
- Friday-defined wrappers: **NOT_IMPLEMENTED**.
- Local definitions of `ICommand`/`IQuery`: **NOT_IMPLEMENTED**.

The interfaces are external contracts consumed directly by Admin Application.

### Command pattern

Admin commands are immutable records implementing `ICommand<TResponse>` and usually live in the same file as their handler.

Representative command:

- File: `src/Modules/Admin/Friday.Modules.Admin.Application/Features/Users/CreateUser.cs`
- Type: `CreateUserCommand`
- Contract: `ICommand<UserDto>`
- Responsibility: carry all data needed to create a user.

Other confirmed command shapes include:

- `LoginCommand : ICommand<LoginResponseDto>`
- `RefreshTokenCommand : ICommand<RefreshTokenResponseDto>`
- `LogoutCommand : ICommand<bool>`
- `AssignRoleToUserCommand : ICommand<UserDto>`
- `GrantRightsToRoleCommand : ICommand<RoleDto>`
- `CreateRightCommand : ICommand<RightDto>`

Command semantics in current code:

- May read and mutate domain state.
- Return DTOs or primitives directly.
- Are automatically targeted by `TransactionBehavior` through the non-generic `ICommand` marker.
- Propagate `CancellationToken` through handler and repository calls.

### Query pattern

Admin queries are immutable records implementing `IQuery<TResponse>` and are colocated with query handlers.

Representative queries:

- File: `src/Modules/Admin/Friday.Modules.Admin.Application/Features/Users/GetUserById.cs`
- Type: `GetUserByIdQuery`
- Contract: `IQuery<UserDto>`
- Responsibility: identify a single user read request.

- File: `src/Modules/Admin/Friday.Modules.Admin.Application/Features/Users/GetUsers.cs`
- Type: `GetUsersQuery`
- Contract: `IQuery<IReadOnlyList<UserDto>>`
- Responsibility: request a user list.

Other confirmed queries include `GetRolesQuery` and `GetRightsQuery`.

Query semantics in current code:

- Read through domain repository interfaces.
- Map domain entities to application DTOs inside handlers.
- Are not targeted by `TransactionBehavior`.
- Can still throw `FridayException` for expected failures, such as not found.
- Repository read methods return tracked EF entities; no separate read model or `AsNoTracking` convention is enforced.

## 3. Handlers

### Command handlers

- Interface: external `ICommandHandler<TCommand,TResponse>`.
- Method: `Task<TResponse> HandleAsync(TCommand request, CancellationToken cancellationToken)`.
- Location: Admin Application feature folders.
- Construction: primary-constructor dependency injection is common.

Representative implementation:

- File: `src/Modules/Admin/Friday.Modules.Admin.Application/Features/Users/CreateUser.cs`
- Class: `CreateUserHandler`
- Method: `HandleAsync`
- Responsibilities:
  - application-level input and uniqueness checks;
  - repository queries;
  - domain aggregate creation and role assignment;
  - password hashing through an injected port/framework abstraction;
  - adding the aggregate through `IUserRepository`;
  - returning `UserDto`.

The handler does not call `SaveChangesAsync`; normal persistence is deferred to the command transaction behavior.

### Query handlers

- Interface: external `IQueryHandler<TQuery,TResponse>`.
- Method: `Task<TResponse> HandleAsync(TQuery request, CancellationToken cancellationToken)`.
- Location: Admin Application feature folders.

Representative implementation:

- File: `src/Modules/Admin/Friday.Modules.Admin.Application/Features/Users/GetUserById.cs`
- Class: `GetUserByIdHandler`
- Method: `HandleAsync`
- Responsibility: call `IUserRepository.GetByIdAsync`, throw a typed application exception when missing, and map to `UserDto`.

### Handler naming and file convention

- Request suffixes: `Command`, `Query`.
- Handler suffixes: both `Handler` and `CommandHandler` occur; naming is not completely uniform.
- One feature file usually contains request and handler.
- DTOs live separately under `Application/Models`.

### Exceptional nested-command behavior

- File: `src/Modules/Admin/Friday.Modules.Admin.Application/Features/Auth/Register.cs`
- Class: `RegisterCommandHandler`
- Method: `HandleAsync`

This handler injects `IUnitOfWork` and calls `CommitAsync` explicitly after adding the new user, then invokes `IMediator.SendAsync(new LoginCommand(...))`.

Observed transaction sequence:

1. Outer `RegisterCommand` enters `TransactionBehavior` and begins a transaction.
2. Handler adds the user and explicitly commits/disposes that transaction.
3. Handler dispatches nested `LoginCommand`.
4. Nested command behavior can begin another transaction and persist the session.
5. Outer behavior calls `CommitAsync` again after the handler returns.

Therefore registration and automatic login/session creation are not one atomic command transaction. This is an existing exception and should not be inferred as the normal module template.

## 4. Dispatcher and Pipelines

### Request dispatcher

- Interface: `LinKit.Core.Cqrs.IMediator`.
- Implementation: external LinKit implementation; concrete type is not defined in Friday source.
- DI entry point: generated/external `AddLinKitCqrs()` called by `Program.cs`.

API dispatch conventions:

- Commands: `mediator.SendAsync(command, cancellationToken)`.
- Queries: `mediator.QueryAsync(query, cancellationToken)`.
- Notifications: `mediator.PublishAsync(notification, strategy, cancellationToken)`.

Evidence:

- File: `src/API/Friday.API/Program.cs`; method: top-level service registration.
- File: `src/API/Friday.API/Modules/Admin/AdminEndpoints.cs`; method: `MapAdminModule` endpoint delegates.
- File: `src/API/Friday.API/Modules/Auth/AuthEndpoints.cs`; method: `MapAuthModule` endpoint delegates.

### Discovery context

- File: `src/API/Friday.API/Cqrs/CqrsContext.cs`
- Class: `CqrsContext`
- Attribute: `[CqrsContext(typeof(AdminApplicationAssemblyMarker), typeof(BuildingBlockApplicationMarker))]`.
- Responsibility: identify Admin Application and BuildingBlocks Application assemblies for LinKit-generated CQRS discovery.

Exact generated registrations and mediator implementation details are **UNKNOWN** without inspecting generated output or runtime service descriptors. Generated `obj` files were intentionally excluded.

### Transaction pipeline

- File: `src/BuildingBlocks/Friday.BuildingBlocks.Application/Behaviors/TransactionBehavior.cs`
- Class: `TransactionBehavior<TRequest,TResponse>`
- Contract: `IPipelineBehavior<TRequest,TResponse>` constrained to `ICommand<TResponse>`.
- Attribute: `[CqrsBehavior(typeof(ICommand), 0)]`.
- Responsibility:
  1. begin UoW transaction;
  2. call next handler;
  3. commit on success;
  4. rollback and rethrow on failure.

Queries do not enter this behavior. Whether behavior order `0` is inner or outer relative to any LinKit built-in behaviors is **UNKNOWN** from Friday source alone.

## 5. Domain and Integration Event Dispatch

### Domain event contract

- File: `src/BuildingBlocks/Friday.BuildingBlocks.Domain/Abstractions/IDomainEvent.cs`
- Interface: `IDomainEvent : INotification`
- Responsibility: make domain events publishable through LinKit and require `OccurredOnUtc`.

### Event collection

- File: `src/BuildingBlocks/Friday.BuildingBlocks.Domain/Entities/Entity.cs`
- Class: `Entity`
- Methods: `Raise`, `ClearDomainEvents`
- Responsibility: retain domain events in memory on an entity.

### Dispatcher port and implementation

- File: `src/BuildingBlocks/Friday.BuildingBlocks.Application/Abstractions/IDomainEventDispatcher.cs`
- Interface: `IDomainEventDispatcher`
- Responsibility: application-facing port for dispatching events from tracked entities.

- File: `src/BuildingBlocks/Friday.BuildingBlocks.Infrastructure/Persistence/DomainEventDispatcher.cs`
- Class: `DomainEventDispatcher`
- Method: `DispatchAsync`
- Responsibility: sequentially publish each entity event through `IMediator`, then clear it.

### Commit ordering

- File: `src/BuildingBlocks/Friday.BuildingBlocks.Infrastructure/Persistence/EfUnitOfWork.cs`
- Class: `EfUnitOfWork`
- Method: `CommitAsync`

Current order:

```text
SaveChangesAsync
  -> collect tracked entities with events
  -> publish domain events sequentially
  -> commit database transaction
```

This means event handlers run after EF writes but before the relational transaction commits. Event publication is in-process and not durable.

### Integration events

- File: `src/BuildingBlocks/Friday.BuildingBlocks.Application/IntegrationEvents/IIntegrationEvent.cs`
- Interface: `IIntegrationEvent : INotification`
- File: `src/BuildingBlocks/Friday.BuildingBlocks.Application/IntegrationEvents/UserCreatedIntegrationEvent.cs`
- Type: `UserCreatedIntegrationEvent`

Admin’s `UserCreatedDomainEventHandler` publishes `UserCreatedIntegrationEvent` through the same in-process mediator. No broker, outbox, inbox, or durable integration-event dispatcher participates. Despite its name, this is currently an in-process notification.

## 6. Validation

Validation pipeline status: **NOT_IMPLEMENTED**.

No `FluentValidation`, `IValidator<T>`, `AbstractValidator<T>`, validation behavior, or local validation abstraction was found in the scoped projects.

Current validation occurs in three places:

1. **HTTP binding**
   - Minimal API binds request records.
   - Dedicated validation filters/endpoint validators: **NOT_IMPLEMENTED**.

2. **Application handlers**
   - Required input checks, existence/uniqueness checks, and application eligibility checks are inline.
   - Expected failures throw `FridayException` with an error code and optional HTTP status.
   - Example: `CreateUserHandler.HandleAsync` checks password, username/email/user-code uniqueness, and role existence.

3. **Domain objects**
   - Factories and methods use guard clauses and throw `ArgumentException`/framework guard exceptions.
   - Example: `User.Create`, `User.AssignRole`, `Role.Create`, `Right.Create`.

Database constraints provide a final persistence layer for selected uniqueness, required fields, keys, and relationships, but are not integrated into a validation-result pipeline.

Consequences of the existing pattern:

- Validation is distributed rather than centrally discoverable.
- Application errors and domain guard errors follow different exception types.
- No automatic aggregation of multiple validation failures exists.
- Handler behavior on malformed null collection properties is not standardized by a validator layer.

## 7. Result and Error Pattern

### Application result pattern

Generic `Result`/`Result<T>` abstraction: **NOT_IMPLEMENTED**.

Handlers return their success values directly:

- DTO (`UserDto`, `RoleDto`, `LoginResponseDto`);
- collection (`IReadOnlyList<UserDto>`);
- primitive (`bool`).

Expected failures are represented by exceptions rather than result values.

### Typed application exception

- File: `src/BuildingBlocks/Friday.BuildingBlocks.Application/Exceptions/FridayException.cs`
- Class: `FridayException`
- Responsibility: carry an error code, message, and HTTP status code.

This application abstraction directly contains HTTP semantics through `StatusCode`.

### Error-code catalog

- File: `src/BuildingBlocks/Friday.BuildingBlocks.Application/Errors/ErrorCodes.cs`
- Class: `ErrorCodes`
- Responsibility: stable common and Admin error-code constants.

Admin-specific codes are stored in BuildingBlocks Application, so the shared project contains module business vocabulary.

### HTTP error mapping

- File: `src/API/Friday.API/Middlewares/ExceptionHandlingMiddleware.cs`
- Class: `ExceptionHandlingMiddleware`
- Methods: `InvokeAsync`, `Map`
- Responsibility:
  - map `FridayException`, `KeyNotFoundException`, `ArgumentException`, `InvalidOperationException`, and unknown exceptions to status/code/message;
  - localize the public message;
  - log the exception;
  - return a failure envelope.

### API envelope

- File: `src/API/Friday.API/Common/ApiResponse.cs`
- Types: `ApiResponse<T>`, `ApiResponse`
- Responsibility: HTTP success/failure envelope containing code, message, data, and trace ID.
- File: `src/API/Friday.API/Common/ApiResults.cs`
- Class/method: `ApiResults.Ok`
- Responsibility: create HTTP 200 success results.

`ApiResponse<T>` is an API transport envelope, not a domain/application Result pattern.

## 8. Dependency Injection Registration

### Composition root

- File: `src/API/Friday.API/Program.cs`
- Responsibility: register BuildingBlocks, LinKit CQRS, Admin Application, and Admin Infrastructure.

Relevant order:

```text
AddBuildingBlocksApplication()
AddBuildingBlocksInfrastructure(configuration)
AddLinKitCqrs()
AddAdminApplication()
AddAdminInfrastructure(configuration)
```

### CQRS registration

- `AddLinKitCqrs()`: generated/external extension; registers mediator, discovered handlers, notifications, and attributed behaviors according to LinKit conventions.
- `CqrsContext`: points discovery at Admin Application and BuildingBlocks Application marker types.
- Exact generated lifetime and registration mechanics: **UNKNOWN** within non-generated Friday source.

### BuildingBlocks Application registration

- File: `src/BuildingBlocks/Friday.BuildingBlocks.Application/DependencyInjection.cs`
- Method: `AddBuildingBlocksApplication`
- Current behavior: no registrations; returns the service collection unchanged.

### BuildingBlocks Infrastructure registration

- File: `src/BuildingBlocks/Friday.BuildingBlocks.Infrastructure/DependencyInjection.cs`
- Method: `AddBuildingBlocksInfrastructure`
- Confirmed registrations:
  - database and cache options;
  - memory cache and optionally distributed Redis cache;
  - `ICacheService` as singleton Memory/Redis implementation;
  - scoped linq2db connection factory;
  - scoped `FridayDbContext`;
  - conditional FluentMigrator runner;
  - scoped `IDomainEventDispatcher -> DomainEventDispatcher`;
  - scoped `IUnitOfWork -> EfUnitOfWork`;
  - scoped `IErrorLocalizationStore -> EfErrorLocalizationStore`.

### Admin Application registration

- File: `src/Modules/Admin/Friday.Modules.Admin.Application/DependencyInjection.cs`
- Method: `AddAdminApplication`
- Current behavior: no registrations; returns the service collection unchanged.

Admin handlers appear to be registered by LinKit discovery rather than this extension method.

### Admin Infrastructure registration

- File: `src/Modules/Admin/Friday.Modules.Admin.Infrastructure/DependencyInjection.cs`
- Method: `AddAdminInfrastructure`
- Confirmed registrations:
  - JWT settings;
  - singleton JWT issuer;
  - scoped password hasher;
  - scoped user, session, role, and right repositories.

## 9. BuildingBlocks Responsibilities

### Friday.BuildingBlocks.Domain

Confirmed responsibility: shared domain primitives.

| Type | Responsibility | Current coupling |
|---|---|---|
| `Entity` | Integer ID, UTC audit timestamps, domain-event collection. | Uses direct `DateTime.UtcNow`. |
| `AggregateRoot` | Marker base for aggregate roots. | Inherits `Entity`. |
| `ValueObject` | Component-based equality and hash code. | No Admin consumer confirmed. |
| `IDomainEvent` | Domain notification contract with occurrence time. | Extends LinKit `INotification`. |

It does not define repositories, UoW, caching, authentication, current user, or clock ports.

### Friday.BuildingBlocks.Application

Confirmed responsibility: shared application ports and cross-cutting mediator behavior.

| Type/area | Responsibility |
|---|---|
| `IUnitOfWork` | Transaction lifecycle abstraction. |
| `TransactionBehavior` | Command-wide transaction policy. |
| `IDomainEventDispatcher` | Port for dispatching tracked domain events. |
| `ICacheService` | Application cache port. |
| `IErrorLocalizationStore` | Error localization lookup port. |
| `IDataSeeder` | Idempotent seeding port. |
| `FridayException` / `ErrorCodes` | Exception-based application errors. |
| Integration event contracts | LinKit notification-shaped in-process integration events. |
| Assembly marker | CQRS discovery anchor. |

Boundary exceptions:

- `ErrorCodes.Admin` is Admin-specific.
- `UserCreatedIntegrationEvent` is user/business-specific.
- `FridayException.StatusCode` carries HTTP transport semantics.

### Friday.BuildingBlocks.Infrastructure

Confirmed responsibility: cross-cutting technical implementations.

| Area | Responsibility |
|---|---|
| Persistence | Shared EF DbContext, UoW, provider configuration, linq2db factory. |
| Event dispatch | In-process domain event publication through LinKit. |
| Migrations | EF schema migrations and FluentMigrator data migrations/startup runner. |
| Caching | Memory and Redis `ICacheService` implementations. |
| Localization | EF-backed localized error messages. |
| Hosting | Root service-provider accessor. |

Boundary exception: `FridayDbContext` hard-codes Admin and Sample infrastructure assembly names, so generic infrastructure knows concrete modules.

## 10. Admin Layer Responsibilities

### Friday.Modules.Admin.Domain

Confirmed responsibility:

- entities and aggregate roots (`User`, `Role`, `Right`);
- join/session/password domain entities;
- domain behavior and guard clauses;
- domain events;
- repository interfaces;
- `CredentialUser` marker for password hashing.

It references only BuildingBlocks Domain at project level. However, its repository interfaces contain asynchronous persistence-shaped operations, and `CredentialUser` exists primarily to support an infrastructure hashing registration.

### Friday.Modules.Admin.Application

Confirmed responsibility:

- commands, queries, handlers, and notification handlers;
- use-case orchestration;
- application-level validation and error selection;
- DTO mapping;
- application security ports/settings (`IJwtTokenIssuer`, `JwtSettings`);
- dependencies on domain repository interfaces;
- publishing in-process integration events.

It does not implement persistence. It is not framework-neutral because it uses ASP.NET Core HTTP abstractions/status codes, ASP.NET Identity interfaces, options, and logging.

### Friday.Modules.Admin.Infrastructure

Confirmed responsibility:

- EF repository implementations;
- EF entity configurations;
- JWT issuer implementation;
- password-hasher binding;
- Admin technical DI registration.

It depends on Admin Application, Admin Domain, and BuildingBlocks Infrastructure. It uses the shared BuildingBlocks `FridayDbContext`; it does not own a module-specific DbContext.

### Friday.API

Confirmed responsibility in this pattern:

- composition root;
- minimal HTTP endpoints;
- command/query dispatch;
- HTTP response envelopes;
- exception-to-HTTP mapping;
- middleware and endpoint authorization.

Endpoints are thin: they bind requests, dispatch through `IMediator`, and format results.

## 11. Reference Flow Through the Pattern

Representative Admin command:

```text
Admin minimal endpoint
  -> IMediator.SendAsync(CreateUserCommand)
  -> TransactionBehavior.BeginTransactionAsync
  -> CreateUserHandler.HandleAsync
  -> IUserRepository / IRoleRepository ports
  -> Admin domain aggregate behavior
  -> EF repository attaches entity to FridayDbContext
  -> TransactionBehavior.CommitAsync
  -> EfUnitOfWork.SaveChangesAsync
  -> DomainEventDispatcher.PublishAsync
  -> relational commit
  -> UserDto
  -> ApiResults.Ok / ApiResponse<UserDto>
```

Representative Admin query:

```text
Admin minimal endpoint
  -> IMediator.QueryAsync(GetUserByIdQuery)
  -> GetUserByIdHandler.HandleAsync
  -> IUserRepository.GetByIdAsync
  -> UserDto or FridayException
  -> ApiResults.Ok or ExceptionHandlingMiddleware
```

## 12. Confirmed Pattern Strengths

1. Commands and queries have explicit request/response types.
2. Endpoint delegates remain thin and do not directly use repositories.
3. Application handlers depend on repository interfaces rather than EF implementations.
4. Project references preserve Domain <- Application <- Infrastructure direction within Admin.
5. Command transaction behavior centralizes normal commit/rollback handling.
6. Cancellation tokens are consistently passed through dispatch, handlers, and repositories.
7. Domain events are explicit and separated from aggregate methods.
8. API error envelopes and trace identifiers are consistent.

## 13. Confirmed Gaps and Caveats

1. Friday does not own CQRS interfaces; module code directly depends on LinKit contracts.
2. Exact LinKit generated handler registrations and behavior ordering are not visible without generated output/runtime inspection.
3. No validation pipeline or validator abstraction exists.
4. No application `Result<T>` pattern exists; expected failures use exceptions.
5. `FridayException` mixes application errors with HTTP status semantics.
6. Shared `ErrorCodes` and integration events contain Admin-specific vocabulary.
7. Shared infrastructure knows concrete module assembly names.
8. One shared DbContext/UoW crosses module persistence boundaries.
9. Domain and integration events are in-process and non-durable.
10. Domain event handlers run before the database transaction commits.
11. `RegisterCommandHandler` manually commits and dispatches a nested command, breaking the normal single-command transaction pattern.
12. Query repositories return tracked entities and no separate read-store convention exists.
13. Application and Domain BuildingBlocks have ASP.NET Core/LinKit framework coupling.

## 14. Unknowns Requiring Verification

- Exact service lifetimes generated by `AddLinKitCqrs`.
- Exact handler selection and duplicate-registration behavior.
- Complete pipeline ordering semantics for `[CqrsBehavior(..., 0)]`.
- Nested mediator behavior and transaction scoping under runtime LinKit implementation.
- Whether domain/integration notification failures are retried or simply abort the command (no retry behavior exists in Friday source).
- Runtime behavior when multiple tracked entities raise multiple events and one handler fails.

## 15. Practical Reference for Existing Modules

When describing—not prescribing—the current Admin module pattern:

- Domain owns aggregates, domain events, and repository ports.
- Application owns CQRS request/handler pairs, DTOs, orchestration, inline validation, and application ports.
- Infrastructure implements repositories and technical services using the shared BuildingBlocks infrastructure.
- API owns transport, mediation calls, response envelopes, and exception mapping.
- Commands normally commit through the global transaction behavior.
- Queries bypass the command transaction behavior.
- Failures are exceptions, not result values.
- LinKit supplies CQRS contracts, mediator, discovery, and pipeline primitives.
