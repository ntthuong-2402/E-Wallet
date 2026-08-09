# Friday Configuration and Dependency Injection Pattern

Scope: only API composition, configuration objects/files, and service-registration extensions. Business behavior was not inspected except for targeted option consumers needed to establish whether values are static or monitored.

Status vocabulary: **CONFIRMED**, **PARTIAL**, **NOT_IMPLEMENTED**, **UNKNOWN**.

## 1. Composition Root

Friday.API has one executable composition root.

- File: `src/API/Friday.API/Program.cs`
- Method: top-level startup
- Responsibility: create `WebApplicationBuilder`, register cross-cutting services and modules, validate the JWT secret, build the host, optionally migrate the database, compose middleware, map endpoints, and run the application.

Registration order in source:

1. Serilog host integration.
2. OpenTelemetry.
3. BuildingBlocks Application.
4. BuildingBlocks Infrastructure.
5. Localization and registration options.
6. Error-message localizer.
7. LinKit CQRS.
8. Admin Application and Infrastructure.
9. Sample Application and Infrastructure.
10. HTTP context accessor.
11. JWT bearer authentication.
12. Authorization.
13. Swagger/OpenAPI.

The API project references both Application and Infrastructure projects and invokes their registration extensions directly. There is no independent module bootstrap interface or automatic module discovery confirmed in source.

## 2. Module and Layer Registration

### BuildingBlocks Application

- File: `src/BuildingBlocks/Friday.BuildingBlocks.Application/DependencyInjection.cs`
- Class/method: `DependencyInjection.AddBuildingBlocksApplication`
- Current behavior: returns the collection unchanged.
- Status: **CONFIRMED no-op registration extension**.

### BuildingBlocks Infrastructure

- File: `src/BuildingBlocks/Friday.BuildingBlocks.Infrastructure/DependencyInjection.cs`
- Class/method: `DependencyInjection.AddBuildingBlocksInfrastructure`
- Input: `IServiceCollection`, `IConfiguration`.
- Responsibility: bind database/cache options; choose cache implementation; configure linq2db and EF Core; conditionally register FluentMigrator; register domain-event, unit-of-work, and localization infrastructure.

Registrations:

| Service | Implementation/configuration | Lifetime |
|---|---|---|
| `DatabaseOptions` | `Database` section via options system | Options registration |
| `CacheOptions` | `Cache` section via options system | Options registration |
| `IMemoryCache` | `AddMemoryCache` | Framework-managed singleton services |
| `IDistributedCache` | StackExchange Redis, only when enabled and configured | Framework registration |
| `ICacheService` | `RedisCacheService` or `MemoryCacheService` | Singleton |
| `ILinqToDbConnectionFactory` | `LinqToDbConnectionFactory` factory | Scoped |
| `FridayDbContext` | Provider selected by `RelationalDbContextConfigurer` | Scoped by `AddDbContext` default |
| FluentMigrator runner | Provider-specific runner, only with `FridayDb` connection | Framework registration |
| `IDomainEventDispatcher` | `DomainEventDispatcher` | Scoped |
| `IUnitOfWork` | `EfUnitOfWork` | Scoped |
| `IErrorLocalizationStore` | `EfErrorLocalizationStore` | Scoped |

### Admin Application

- File: `src/Modules/Admin/Friday.Modules.Admin.Application/DependencyInjection.cs`
- Class/method: `DependencyInjection.AddAdminApplication`
- Current behavior: returns the collection unchanged.
- Status: **CONFIRMED no-op registration extension**.

Admin command/query discovery is delegated to LinKit CQRS from the API:

- File: `src/API/Friday.API/Program.cs`; call: `AddLinKitCqrs()`.
- File: `src/API/Friday.API/Cqrs/CqrsContext.cs`; class: `CqrsContext`.
- Evidence: `[CqrsContext(typeof(AdminApplicationAssemblyMarker), typeof(BuildingBlockApplicationMarker))]` identifies Admin Application and BuildingBlocks Application assemblies.
- Exact generated/package registration internals: **UNKNOWN** without inspecting external LinKit implementation or generated output, which is outside this task.

### Admin Infrastructure

- File: `src/Modules/Admin/Friday.Modules.Admin.Infrastructure/DependencyInjection.cs`
- Class/method: `DependencyInjection.AddAdminInfrastructure`
- Input: `IServiceCollection`, `IConfiguration`.

Registrations:

| Service | Implementation | Lifetime |
|---|---|---|
| `JwtSettings` | `Authentication:Jwt` section | Options registration |
| `IJwtTokenIssuer` | `JwtTokenIssuer` | Singleton |
| `IPasswordHasher<CredentialUser>` | ASP.NET Core Identity `PasswordHasher<CredentialUser>` | Scoped |
| `IUserRepository` | `UserRepository` | Scoped |
| `IUserSessionRepository` | `UserSessionRepository` | Scoped |
| `IRoleRepository` | `RoleRepository` | Scoped |
| `IRightRepository` | `RightRepository` | Scoped |

### Sample module

- File: `src/Modules/Sample/Friday.Modules.Sample.Application/DependencyInjection.cs`
- Method: `AddSampleApplication`; current behavior is a no-op.
- File: `src/Modules/Sample/Friday.Modules.Sample.Infrastructure/DependencyInjection.cs`
- Method: `AddSampleInfrastructure`; registers `ITodoItemRepository` to `InMemoryTodoItemRepository` as Singleton.

This is included only to show the actual module-registration convention. Sample business behavior was not inspected.

## 3. Options Pattern

| Section | Options class | Registration/binding | Confirmed consumption behavior |
|---|---|---|---|
| `Database` | `DatabaseOptions` | `Configure<T>` plus direct `Get<T>` in BuildingBlocks Infrastructure | Provider/cache registration choices are captured during service registration; migration enablement is read directly from `IConfiguration` at startup. |
| `Cache` | `CacheOptions` | `Configure<T>` plus direct `Get<T>` | Cache implementation and connection are selected during service registration. |
| `Authentication:Jwt` | `JwtSettings` | `Configure<T>` in Admin Infrastructure plus direct `Get<T>` in `Program.cs` | Bearer validation captures direct-bound values; token issuer and auth handlers consume `IOptions<JwtSettings>`, not monitor/snapshot. |
| `Authentication` | `RegistrationOptions` | `Configure<T>` in `Program.cs` | `RegisterCommandHandler` uses `IOptionsMonitor<RegistrationOptions>.CurrentValue`, so this setting is explicitly monitored. |
| `Localization` | `LocalizationOptions` | `Configure<T>` in `Program.cs` | `ErrorMessageLocalizer` consumes `IOptions<LocalizationOptions>`. |
| `OpenTelemetry` | `OpenTelemetryOptions` | Direct `Get<T>` in logging and telemetry extensions | Telemetry/log-export topology is decided during host construction; the type is not registered as options. |

Evidence files:

- `src/BuildingBlocks/Friday.BuildingBlocks.Infrastructure/Persistence/DatabaseOptions.cs`
- `src/BuildingBlocks/Friday.BuildingBlocks.Infrastructure/Caching/CacheOptions.cs`
- `src/Modules/Admin/Friday.Modules.Admin.Application/Configuration/JwtSettings.cs`
- `src/Modules/Admin/Friday.Modules.Admin.Application/Configuration/RegistrationOptions.cs`
- `src/API/Friday.API/Common/LocalizationOptions.cs`
- `src/API/Friday.API/Configuration/OpenTelemetryOptions.cs`

Options validation:

- `AddOptions(...).Validate(...)`, data-annotation validation, and `ValidateOnStart`: **NOT_IMPLEMENTED**.
- `Program.cs` manually rejects a blank JWT secret or one shorter than 32 characters before host build.
- No equivalent startup validation is configured for JWT issuer/audience, database connection, cache connection, or telemetry service name.
- `JwtTokenIssuer` also guards secret length when issuing; token-duration consumers clamp configured values. These are runtime safeguards, not options validation registrations.

## 4. Database Registration

- File: `src/BuildingBlocks/Friday.BuildingBlocks.Infrastructure/DependencyInjection.cs`
- Method: `AddBuildingBlocksInfrastructure`.
- Primary connection lookup: `configuration.GetConnectionString("FridayDb")`.
- `ConnectionStrings:AdminDatabase`: present in all JSON environments and Compose, but **not consumed by searched production C# configuration code**.

Provider selection:

- File: `src/BuildingBlocks/Friday.BuildingBlocks.Infrastructure/Persistence/RelationalDbContextConfigurer.cs`
- Method: `Configure`.
- Supported switches: SQL Server, PostgreSQL, MySQL, Oracle.
- Missing/blank `FridayDb`: EF Core uses the named in-memory database `Friday.Shared`.
- Configured default and all supplied environment JSON values: PostgreSQL.

Other database services:

- `ILinqToDbConnectionFactory` is scoped and receives the selected provider and `FridayDb` connection. If that connection is missing, its registration uses a LocalDB SQL Server fallback string while still passing the configured provider. This fallback can be inconsistent when the provider remains PostgreSQL.
- FluentMigrator is registered only when `FridayDb` is nonblank and selects a provider-specific runner.
- File: `src/BuildingBlocks/Friday.BuildingBlocks.Infrastructure/Persistence/DatabaseMigrationStartup.cs`.
- Method: `ApplyEfThenDataMigrationsAsync`.
- Startup migration runs only when `Database:ApplyMigrationsOnStartup` is true; it applies EF migrations first, then available FluentMigrator migrations.

## 5. JWT Configuration

- Section: `Authentication:Jwt`.
- Class: `JwtSettings`.
- Fields: `Secret`, `Issuer`, `Audience`, `AccessTokenMinutes`, `RefreshTokenDays`.

JWT configuration has two consumers established at startup:

1. `Program.cs` directly binds `JwtSettings`, validates secret length, and captures issuer, audience, and symmetric signing key in `TokenValidationParameters` for JWT bearer authentication.
2. `AddAdminInfrastructure` registers `JwtSettings`; `JwtTokenIssuer` and Admin auth handlers consume `IOptions<JwtSettings>` for token issuance and lifetimes.

Bearer validation explicitly enables issuer, audience, lifetime, and signing-key validation. The default authentication scheme is `JwtBearerDefaults.AuthenticationScheme`. Authorization uses default `AddAuthorization()` configuration; no named/fallback policies are registered here.

Because validation and issuance use separate bindings and neither uses `IOptionsMonitor`, live configuration changes are not confirmed to update either path consistently. Restart is the safe behavior established by source.

## 6. Middleware Registration and Activation

The three custom middleware types are added directly to the pipeline with `UseMiddleware<T>` in `Program.cs`:

- `CorrelationIdMiddleware`
- `ExceptionHandlingMiddleware`
- `AuthenticatedUserValidationMiddleware`

There are no explicit `AddScoped`/`AddTransient` registrations for these middleware classes. They use conventional middleware activation through `UseMiddleware<T>` rather than `IMiddleware` registrations.

Framework middleware/service pairs:

- `AddAuthentication(...).AddJwtBearer(...)` / `UseAuthentication()`.
- `AddAuthorization()` / `UseAuthorization()`.
- `AddFridaySwagger()` / Development-only `UseSwagger()` and `UseSwaggerUI()`.
- Serilog is attached through `builder.Host.UseSerilog(...)`; request logging uses `UseSerilogRequestLogging(...)`.
- OpenTelemetry is service-only instrumentation through `AddOpenTelemetry()`; no custom request middleware is registered for it.

## 7. Environment-Specific Configuration

The application starts with `WebApplication.CreateBuilder(args)`, so it uses the standard ASP.NET Core configuration host. No custom `AddJsonFile`, custom configuration provider, or provider reordering appears in source.

### Base: `appsettings.json`

- PostgreSQL at localhost with checked-in development credentials.
- Redis disabled; memory cache selected.
- Startup migrations disabled.
- OpenTelemetry enabled, but no OTLP endpoint; Serilog mode selected.
- Public registration disabled.
- JWT secret, issuer, audience, and lifetimes supplied.
- Serilog Information default level and classic console sink; application code also adds rolling compact-JSON files when not using a valid OTLP log sink.

### Development: `appsettings.Development.json`

- Selected by the launch profiles through `ASPNETCORE_ENVIRONMENT=Development`.
- Overrides default logging to Debug and EF command logging to Information.
- Keeps local PostgreSQL, Redis disabled, and startup migrations disabled.
- Configures local OTLP gRPC endpoint `http://127.0.0.1:4317`.
- Enables public registration.
- Inherits JWT settings from the base file because it does not replace the `Jwt` subsection.
- Swagger/UI is enabled by the explicit `app.Environment.IsDevelopment()` check in `Program.cs`.

### Docker: `appsettings.Docker.json`

- Compose sets `ASPNETCORE_ENVIRONMENT=Docker`, causing the Docker environment file to overlay base configuration under the standard host.
- Uses service DNS names for PostgreSQL, Redis, and Jaeger.
- Enables Redis, startup migrations, and public registration.
- Does not override the base JWT subsection itself.
- Compose environment variables override connection strings, database/cache flags, OTLP endpoint, and the JWT secret using double-underscore nested keys.
- Swagger/UI remains disabled because `Docker` is not `Development`.

Configuration precedence beyond these supplied files and Compose environment variables follows the standard host created by `WebApplication.CreateBuilder`; no project-specific precedence customization exists.

## 8. Logging and Telemetry Configuration

### Serilog

- File: `src/API/Friday.API/Configuration/FridaySerilogWebApplicationBuilderExtensions.cs`
- Method: `AddFridaySerilog`.
- Reads the `Serilog` section and services, enriches `Application=Friday.API`, then selects output using directly bound `OpenTelemetryOptions`.
- `LogExport=OpenTelemetry` with a valid absolute endpoint selects the Serilog OTLP sink.
- Otherwise it loads `SerilogClassic` and adds a rolling compact-JSON file under the content root `logs/`.
- The file sink rolls daily and at 100 MB, retains 14 files, is shared, and flushes every second.

### OpenTelemetry

- File: `src/API/Friday.API/Configuration/OpenTelemetryServiceCollectionExtensions.cs`
- Method: `AddFridayOpenTelemetry`.
- `Enabled=false` skips SDK registration.
- Enabled instrumentation covers ASP.NET Core, HTTP clients, and runtime metrics; tracing uses `AlwaysOnSampler`.
- A valid absolute `OtlpEndpoint` enables trace/metric exporters; an empty/invalid endpoint leaves instrumentation registered without an exporter.
- Protocol is gRPC by default or HTTP/protobuf only when configured as `HttpProtobuf`.

## 9. Secret and Configuration Handling

Confirmed safeguards:

- Startup refuses JWT secrets shorter than 32 characters.
- Compose demonstrates environment-variable override syntax for the JWT secret and connections.
- No secret values are logged by the inspected registration/configuration code.

Confirmed risks/gaps:

- `appsettings.json` contains a checked-in JWT signing secret and PostgreSQL username/password.
- `docker-compose.yml` contains another literal JWT signing secret and literal PostgreSQL credentials.
- These values are visibly development placeholders, but there is no environment guard that rejects those known placeholders outside Development.
- `appsettings.Docker.json` also contains literal database credentials; Compose overrides them with equivalent literals.
- No user-secrets identifier, external secret-store provider, key-per-file provider, or custom secrets integration is configured in the API project/source inspected.
- No strongly typed options validation is registered beyond the manual JWT-secret length check.
- Docker enables public registration through both its environment overlay and supplied settings, increasing the importance of deployment-specific overrides.

Whether production deployment injects safer values externally is **UNKNOWN**; no production deployment configuration was inspected or inferred.

## 10. Configuration Ownership Summary

| Concern | Owning registration location | Configuration owner |
|---|---|---|
| Host/API auth, authorization, localization, Swagger | `Friday.API/Program.cs` and `Configuration/` extensions | API composition root |
| Database, cache, Unit of Work, localization storage | BuildingBlocks Infrastructure `DependencyInjection` | Shared infrastructure |
| JWT issuance and Admin repositories/security | Admin Infrastructure `DependencyInjection` | Admin infrastructure |
| CQRS discovery/dispatch registration | API `AddLinKitCqrs` plus `CqrsContext` marker list | API composition root/external LinKit package |
| Sample in-memory repository | Sample Infrastructure `DependencyInjection` | Sample infrastructure |

The dominant pattern is explicit extension-method registration invoked by the API composition root. Application-layer registration extensions currently exist as module conventions but are no-ops; concrete dependencies are registered in Infrastructure or directly in the API.

## 11. Unknowns and Boundaries

- Exact LinKit generated/package DI registrations and lifetimes: **UNKNOWN** within this source-only scope.
- Production environment configuration and secret injection: **UNKNOWN**.
- Runtime configuration reload behavior from deployed providers: **PARTIAL**; only registration uses `IOptionsMonitor`, while startup-selected infrastructure and JWT paths do not.
- Whether `ConnectionStrings:AdminDatabase` is reserved for future use: **UNKNOWN**; it is currently unused by searched production C#.
- Configuration-provider behavior was not runtime-tested.
