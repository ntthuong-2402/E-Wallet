# Friday Admin Persistence Pattern

> Admin P1 delta (2026-08-10): `UserSession.Version` is an optimistic concurrency token; refresh hashes have a unique index; `user_roles.RoleId` and `role_rights.RightId` have restrictive FKs; Admin user reads use bounded no-tracking pagination/filtering. Older contrary statements below are historical.

Scope: persistence architecture currently used by the Admin module. This documents existing source behavior only; it is not a proposed database design. Generated `bin/`/`obj` content and unrelated functionality were excluded.

Status vocabulary: **CONFIRMED**, **PARTIAL**, **NOT_IMPLEMENTED**, **UNKNOWN**.

## 1. Persistence Summary

Admin uses EF Core through a shared BuildingBlocks context:

```text
Admin Application command/query handler
  -> Admin Domain repository interface
  -> Admin Infrastructure EF repository
  -> BuildingBlocks Infrastructure FridayDbContext
  -> configured EF Core provider
```

Logical ownership and physical ownership differ:

- Admin Domain owns the entities and repository interfaces.
- Admin Infrastructure owns EF entity configurations and repository implementations.
- BuildingBlocks Infrastructure owns `FridayDbContext`, provider selection, Unit of Work, schema migration files, and migration startup.
- The database schema groups Admin objects under PostgreSQL schema `admin`, but Admin does not have its own DbContext or migration assembly.

The persistence style is **repository plus Unit of Work**, not direct DbContext use from application handlers and not a generic repository abstraction.

## 2. DbContext

### Shared context

- File: `src/BuildingBlocks/Friday.BuildingBlocks.Infrastructure/Persistence/FridayDbContext.cs`
- Class: `FridayDbContext`
- Base: `Microsoft.EntityFrameworkCore.DbContext`
- Constructor: accepts `DbContextOptions<FridayDbContext>`.
- Explicit `DbSet<T>` properties: **NOT_IMPLEMENTED**.
- Access convention: repositories call `dbContext.Set<TEntity>()`.

`OnModelCreating` performs assembly-based configuration discovery:

1. Applies configurations from `Friday.BuildingBlocks.Infrastructure`.
2. Loads each name in `ModuleConfigurationAssemblyNames`.
3. Applies `IEntityTypeConfiguration<T>` types from those assemblies.

The hard-coded module list includes `Friday.Modules.Admin.Infrastructure` and `Friday.Modules.Sample.Infrastructure`. A missing module assembly is silently ignored only when `Assembly.Load` throws `FileNotFoundException`.

Admin consequence: its entity mappings are discovered indirectly by the shared context. BuildingBlocks Infrastructure therefore knows the concrete Admin infrastructure assembly name.

### Registration and lifetime

- File: `src/BuildingBlocks/Friday.BuildingBlocks.Infrastructure/DependencyInjection.cs`
- Class: `DependencyInjection`
- Method: `AddBuildingBlocksInfrastructure`
- Registration: `services.AddDbContext<FridayDbContext>(...)`.
- Effective conventional lifetime: scoped unless overridden; no override is present.
- Same scope also resolves scoped `EfUnitOfWork` and Admin repositories, so they share the scoped context.

### Connection strings

- Runtime connection string consumed: `ConnectionStrings:FridayDb`.
- `ConnectionStrings:AdminDatabase` exists in configuration but no Admin persistence registration consumes it.
- Missing/blank `FridayDb`: selects EF InMemory database named `Friday.Shared`.

### SaveChanges customization

- `FridayDbContext` does not override `SaveChanges` or `SaveChangesAsync`.
- Save interceptors: **NOT_IMPLEMENTED**.
- Automatic audit-field update during saving: **NOT_IMPLEMENTED**.
- Domain base `Entity` initializes UTC audit timestamps and exposes manual `Touch()`, but the context does not enforce or update them.
- Soft-delete/global query filters: **NOT_IMPLEMENTED**.
- Concurrency tokens/row versions: **NOT_IMPLEMENTED**.

## 3. EF Core Provider

### Current configured provider

- Status: **CONFIRMED PostgreSQL**.
- Runtime option: `Database:Provider = PostgreSql` in base and Docker settings.
- EF configuration: `options.UseNpgsql(connectionString)`.
- Design-time factory: always uses `UseNpgsql`.
- Current generated migration: named `InitPostgres` and contains Npgsql/PostgreSQL-specific column types and identity annotations.

Relevant files:

- `src/BuildingBlocks/Friday.BuildingBlocks.Infrastructure/Persistence/DatabaseOptions.cs`
- `src/BuildingBlocks/Friday.BuildingBlocks.Infrastructure/Persistence/RelationalDatabaseProvider.cs`
- `src/BuildingBlocks/Friday.BuildingBlocks.Infrastructure/Persistence/RelationalDbContextConfigurer.cs`
- `src/API/Friday.API/DesignTimeDbContextFactory.cs`

### Compiled provider alternatives

`RelationalDbContextConfigurer` can select:

- SQL Server via `UseSqlServer`;
- PostgreSQL via `UseNpgsql`;
- MySQL via `UseMySQL`;
- Oracle via `UseOracle`;
- EF InMemory when the connection string is absent.

This is provider-selection capability, not proof that the Admin schema migration is portable across all providers. The checked-in EF migration is PostgreSQL-specific.

### Design-time behavior

- File: `src/API/Friday.API/DesignTimeDbContextFactory.cs`
- Class: `DesignTimeDbContextFactory`
- Method: `CreateDbContext`
- Responsibility: construct a PostgreSQL `FridayDbContext` for EF tooling.
- Connection override: `FRIDAY_DESIGN_TIME_PG` environment variable.
- Fallback: a hard-coded local PostgreSQL connection string.

## 4. Admin Entities and Tables

| Domain entity | Kind | Table | Key | Main persistence role |
|---|---|---|---|---|
| `User` | `AggregateRoot` | `admin.users` | integer `Id` | Identity/profile, active/locked state, password and role navigations. |
| `UserPassword` | Entity-like dependent | `admin.user_passwords` | `UserId` | One-to-one password hash. |
| `UserSession` | Standalone entity | `admin.user_sessions` | GUID `Id` | Refresh hash, expiry, revocation, IP and user agent. |
| `UserRole` | Join entity | `admin.user_roles` | `(UserId, RoleId)` | User-to-role assignment. |
| `Role` | `AggregateRoot` | `admin.roles` | integer `Id` | Role code/name/active state and right assignments. |
| `RoleRight` | Join entity | `admin.role_rights` | `(RoleId, RightId)` | Role-to-right assignment. |
| `Right` | `AggregateRoot` | `admin.rights` | integer `Id` | Permission/right catalog. |

`User`, `Role`, and `Right` inherit shared `Entity` audit/event behavior. `UserPassword`, `UserSession`, `UserRole`, and `RoleRight` do not inherit it.

## 5. Entity Configurations

All Admin mappings are in:

`src/Modules/Admin/Friday.Modules.Admin.Infrastructure/Persistence/Configurations/`

### UserConfiguration

- Class: `UserConfiguration : IEntityTypeConfiguration<User>`.
- Table: `admin.users`.
- Required: `UserCode`, `Username`, `Email`, `FullName`, `IsActive`, `IsLocked`, `CreatedOnUtc`, `UpdatedOnUtc`.
- Maximum lengths: user code 64, username 100, email/full name 256, phone 32, address 512, company 256, job title 128, notes 2000.
- Unique indexes: `UserCode`, `Username`, `Email`.
- Relationship: `User.HasMany(UserRoles)` with FK `UserRole.UserId`.
- Domain events ignored by EF.

### UserPasswordConfiguration

- Class: `UserPasswordConfiguration`.
- Table: `admin.user_passwords`.
- PK: `UserId`.
- `PasswordHash`: required, maximum 500.
- Relationship: required one-to-one dependent of `User`; FK `UserId`; cascade delete.

### UserSessionConfiguration

- Class: `UserSessionConfiguration`.
- Table: `admin.user_sessions`.
- PK: GUID `Id`.
- `RefreshTokenHash`: required, maximum 64.
- Optional lengths: IP 64, user agent 512.
- Indexes: `RefreshTokenHash` (non-unique), `UserId`.
- Relationship: FK `UserId` to `User`; cascade delete.
- Concurrency token: **NOT_IMPLEMENTED**.

### UserRoleConfiguration

- Class: `UserRoleConfiguration`.
- Table: `admin.user_roles`.
- Composite PK: `(UserId, RoleId)`.
- FK to user: supplied through `UserConfiguration` and present in migration.
- FK from `RoleId` to `admin.roles`: **NOT_IMPLEMENTED** in current model/migration.

### RoleConfiguration

- Class: `RoleConfiguration`.
- Table: `admin.roles`.
- Required: `Code`, `Name`, `IsActive`, `CreatedOnUtc`, `UpdatedOnUtc`.
- Maximum lengths: code 100, name 200.
- Unique index: `Code`.
- Relationship: `Role.HasMany(RoleRights)` with FK `RoleRight.RoleId`.
- Domain events ignored by EF.

### RoleRightConfiguration

- Class: `RoleRightConfiguration`.
- Table: `admin.role_rights`.
- Composite PK: `(RoleId, RightId)`.
- FK to role: supplied through `RoleConfiguration` and present in migration.
- FK from `RightId` to `admin.rights`: **NOT_IMPLEMENTED** in current model/migration.

### RightConfiguration

- Class: `RightConfiguration`.
- Table: `admin.rights`.
- Required: `Code`, `Name`, `Description`, `CreatedOnUtc`, `UpdatedOnUtc`.
- Maximum lengths: code 120, name 200, description 500.
- Unique index: `Code`.
- Domain events ignored by EF.

## 6. Schema Conventions and Constraints

- Table/schema names: explicitly lowercase snake_case under schema `admin`.
- Generated column names: property/PascalCase names such as `UserCode`, `CreatedOnUtc`.
- PostgreSQL timestamps: migration uses `timestamp with time zone` for Admin UTC date/time properties.
- Integer aggregate IDs: PostgreSQL identity-by-default.
- User session ID: PostgreSQL `uuid`.
- String lengths and requiredness: explicit for main fields.
- Unique business identifiers: database unique indexes exist for user code, username, email, role code, and right code.
- Cascade deletion: user -> password, sessions, user roles; role -> role rights.
- Missing referential constraints: user-role -> role and role-right -> right.
- Check constraints: **NOT_IMPLEMENTED**.
- Soft-delete columns/filters: **NOT_IMPLEMENTED**.
- Optimistic concurrency columns: **NOT_IMPLEMENTED**.

## 7. Repository Pattern

### Interfaces

Repository ports live in Admin Domain:

- `IUserRepository`
- `IUserSessionRepository`
- `IRoleRepository`
- `IRightRepository`

Location: `src/Modules/Admin/Friday.Modules.Admin.Domain/Repositories/`.

They are entity-specific interfaces rather than a generic repository. They expose asynchronous add, lookup, existence, list, and feature-specific session operations.

### Implementations

Implementations live in Admin Infrastructure:

- `UserRepository`
- `UserSessionRepository`
- `RoleRepository`
- `RightRepository`

Location: `src/Modules/Admin/Friday.Modules.Admin.Infrastructure/Repositories/`.

Registration:

- File: `src/Modules/Admin/Friday.Modules.Admin.Infrastructure/DependencyInjection.cs`
- Method: `AddAdminInfrastructure`
- Lifetime: scoped for every repository.

### Write behavior

- Repository `AddAsync` methods call `DbSet.AddAsync` through `Set<T>()`.
- They attach new entities to the change tracker but do not call `SaveChangesAsync`.
- Updates generally mutate already tracked domain entities; no explicit repository `Update` method is used.
- Deletes: no Admin repository delete methods were found.
- Bulk update/delete: **NOT_IMPLEMENTED**.

### Read behavior

- All inspected reads use EF LINQ and async terminal operations.
- `UserRepository` selectively includes `UserRoles` and/or `PasswordCredential`.
- `RoleRepository.GetByIdAsync` and `ListAsync` include `RoleRights`; `GetByIdsAsync` does not.
- `RightRepository` uses direct entity queries.
- `UserSessionRepository` queries by ID/hash/user and updates loaded session objects.
- `AsNoTracking`: **NOT_IMPLEMENTED** in Admin repositories; query results are tracked.
- Projection to DTOs at SQL level: **NOT_IMPLEMENTED**; handlers map loaded entities.
- Dapper/linq2db use by Admin repositories: **NOT_IMPLEMENTED**.

## 8. Unit of Work

### Abstraction

- File: `src/BuildingBlocks/Friday.BuildingBlocks.Application/Abstractions/IUnitOfWork.cs`
- Interface: `IUnitOfWork`
- Methods: `BeginTransactionAsync`, `CommitAsync`, `RollbackAsync`.

### EF implementation

- File: `src/BuildingBlocks/Friday.BuildingBlocks.Infrastructure/Persistence/EfUnitOfWork.cs`
- Class: `EfUnitOfWork`
- Dependencies: shared `FridayDbContext`, `IDomainEventDispatcher`.
- Registration: scoped `IUnitOfWork -> EfUnitOfWork`.

`BeginTransactionAsync`:

- returns without action if this UoW already holds a transaction;
- returns without action if the DbContext already has a current transaction;
- returns without action for non-relational providers;
- otherwise calls `Database.BeginTransactionAsync`.

`CommitAsync`:

1. calls `FridayDbContext.SaveChangesAsync`;
2. finds tracked `Entity` objects with domain events;
3. dispatches those events;
4. commits and disposes the owned relational transaction, when present.

`RollbackAsync`:

- rolls back/disposes its owned transaction when present;
- otherwise clears the change tracker.

`InMemoryUnitOfWork` exists as a no-op implementation but is not selected by current DI. When EF InMemory is chosen, the registered `EfUnitOfWork` remains active: begin is a no-op, commit still calls `SaveChangesAsync` and dispatches events, and rollback clears tracking.

## 9. Command Transaction Handling

- File: `src/BuildingBlocks/Friday.BuildingBlocks.Application/Behaviors/TransactionBehavior.cs`
- Class: `TransactionBehavior<TRequest,TResponse>`.
- Selection: LinKit `[CqrsBehavior(typeof(ICommand), 0)]` and generic constraint `ICommand<TResponse>`.

Normal command flow:

```text
BeginTransactionAsync
  -> command handler
      -> repositories query/attach/mutate tracked entities
  -> CommitAsync
      -> SaveChangesAsync
      -> domain event dispatch
      -> database transaction commit
```

On handler/commit failure, the behavior calls `RollbackAsync` and rethrows.

Queries are not wrapped by this behavior. They execute through the scoped context without an explicit application transaction.

### Registration command exception

- File: `src/Modules/Admin/Friday.Modules.Admin.Application/Features/Auth/Register.cs`
- Class: `RegisterCommandHandler`
- Method: `HandleAsync`

This handler explicitly calls `IUnitOfWork.CommitAsync` after adding the user, then dispatches a nested `LoginCommand`. The nested command can start and commit a second transaction for the session. The outer transaction behavior calls `CommitAsync` again when the register handler returns.

Consequences of existing behavior:

- user registration and login-session creation are not one atomic transaction;
- the normal rule that handlers defer commit to the pipeline is violated;
- an error during nested login can occur after the user was already committed.

Exact nested runtime behavior depends on LinKit pipeline/scoping and remains **PARTIAL** without runtime verification, but the explicit early commit is source-confirmed.

## 10. SaveChanges and Domain Events

There is one normal application save path: `EfUnitOfWork.CommitAsync` calls `dbContext.SaveChangesAsync(cancellationToken)`.

Ordering is significant:

```text
SaveChangesAsync
  -> inspect ChangeTracker for BuildingBlocks Entity domain events
  -> publish each event sequentially through IMediator
  -> clear entity events
  -> commit relational transaction
```

- Domain handlers execute after SQL writes have been sent but before transaction commit.
- If event dispatch throws while a relational transaction is owned, the command behavior rolls the transaction back.
- Event publication is in-process and has no outbox/durable delivery.
- Events are cleared only after all events for an entity are published successfully.
- Entities not inheriting BuildingBlocks `Entity`, including `UserSession`, are not collected by this dispatch mechanism.
- A handler may perform additional tracked changes during event publication, but `EfUnitOfWork.CommitAsync` does not call `SaveChangesAsync` a second time afterward. Whether current event handlers create such changes is outside this persistence-only baseline; the save behavior itself is confirmed.

Direct `SaveChangesAsync` also occurs inside shared FluentMigrator data-migration classes for localization data; that is migration/seeding behavior, not the normal Admin repository command path.

## 11. Migrations

### EF Core schema migration

Location: `src/BuildingBlocks/Friday.BuildingBlocks.Infrastructure/Migrations/`.

Confirmed artifacts:

- `20260405022257_InitPostgres.cs`
- `20260405022257_InitPostgres.Designer.cs`
- `FridayDbContextModelSnapshot.cs`

The migration belongs to BuildingBlocks Infrastructure and contains both shared localization and Admin schema objects. It is not module-owned physically.

Admin objects created:

- schema `admin`;
- `rights`;
- `roles`;
- `users`;
- `role_rights`;
- `user_passwords`;
- `user_roles`;
- `user_sessions`;
- indexes and the relationships described in this document.

The migration is PostgreSQL-specific (`Npgsql:ValueGenerationStrategy`, PostgreSQL types).

### FluentMigrator data migrations

Location: `src/BuildingBlocks/Friday.BuildingBlocks.Infrastructure/DataMigrations/`.

The checked-in timestamped migrations seed/update localization messages, including Admin/authentication error messages. They resolve the shared `FridayDbContext` and call `SaveChangesAsync` inside migration execution.

FluentMigrator scans the BuildingBlocks Infrastructure assembly using `DataMigrationAssemblyMarker`. Runner selection follows `DatabaseOptions.Provider`.

### Startup application

- File: `src/BuildingBlocks/Friday.BuildingBlocks.Infrastructure/Persistence/DatabaseMigrationStartup.cs`
- Class: `DatabaseMigrationStartup`
- Method: `ApplyEfThenDataMigrationsAsync`.

When `Database:ApplyMigrationsOnStartup` is true:

1. create an async service scope;
2. resolve `FridayDbContext`;
3. skip non-relational providers;
4. call EF `Database.MigrateAsync`;
5. resolve FluentMigrator runner when available;
6. call `MigrateUp` for data migrations.

Configuration:

- base/development: startup migration disabled;
- Docker: startup migration enabled.

## 12. Confirmed Persistence Strengths

1. Application handlers depend on explicit entity-specific repository ports.
2. Repository implementations and EF mappings stay in Admin Infrastructure.
3. Normal commands centralize transaction, save, and rollback behavior.
4. Async EF calls propagate cancellation tokens.
5. Main uniqueness requirements have database unique indexes.
6. Length/required constraints are explicit for core fields.
7. PostgreSQL UTC date/time columns use `timestamp with time zone`.
8. Migrations and model snapshot are checked in.

## 13. Confirmed Gaps and Risks

1. Admin has logical schema ownership but shares DbContext, migrations, connection, and UoW with other modules.
2. BuildingBlocks Infrastructure hard-codes concrete module assembly names.
3. The checked-in migration is PostgreSQL-specific despite runtime multi-provider selection.
4. `AdminDatabase` configuration is unused, which can imply an isolation that does not exist.
5. Missing FKs allow orphaned `user_roles.RoleId` and `role_rights.RightId` through paths outside validated application handlers.
6. No optimistic concurrency tokens protect User, Role, Right, or UserSession updates.
7. Refresh-token rotation can race because `UserSession` has no row version/concurrency predicate.
8. All Admin repository reads are tracked and entity-based; no read-only/projection convention exists.
9. No SaveChanges interceptor enforces UTC/audit updates or other cross-cutting persistence rules.
10. Domain events are dispatched before transaction commit and are not durable.
11. `RegisterCommandHandler` performs an early manual commit and splits one use case across transactions.
12. Startup schema migration is enabled in Docker inside API startup.
13. No persistence/integration/migration/concurrency tests were found in the established project baseline.

## 14. Unknowns Requiring Verification

- Actual applied migration version and live database schema.
- Runtime behavior of nested command transactions under LinKit.
- Required concurrency semantics for session refresh, user changes, and role-right replacement.
- Production migration execution/rollback process outside local Docker configuration.
- Whether alternate providers have ever been exercised with the current PostgreSQL migration/model.
- Query plans and index utilization for case-normalized username/role/right lookups.
- Data retention and cleanup policy for expired/revoked sessions.

## 15. Reference Persistence Flow

For a normal Admin command, the current pattern is:

```text
CQRS command handler
  -> domain repository interface
  -> scoped EF repository implementation
  -> shared scoped FridayDbContext change tracker
  -> command TransactionBehavior
  -> scoped EfUnitOfWork.CommitAsync
  -> SaveChangesAsync
  -> in-process domain event dispatch
  -> relational transaction commit
```

For an Admin query:

```text
CQRS query handler
  -> domain repository interface
  -> scoped EF repository implementation
  -> tracked EF entity query through FridayDbContext.Set<T>()
  -> DTO mapping in application handler
```
