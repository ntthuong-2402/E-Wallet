# Friday API Request Pipeline Pattern

Scope: the existing executable API request pipeline. Findings are based only on source under `Friday.API` plus the Admin command/query and persistence behavior needed to trace representative requests. No runtime behavior is asserted where the source delegates formatting to ASP.NET Core.

Status vocabulary: **CONFIRMED**, **PARTIAL**, **NOT_IMPLEMENTED**, **UNKNOWN**.

## 1. API Style and Endpoint Registration

API style: **CONFIRMED minimal API**.

- MVC controller registration (`AddControllers`): **NOT_IMPLEMENTED**.
- Controller mapping (`MapControllers`): **NOT_IMPLEMENTED**.
- `ControllerBase` implementations: **NOT_IMPLEMENTED**.
- Endpoint registration uses `WebApplication` and `IEndpointRouteBuilder` extension methods.

### Composition root

- File: `src/API/Friday.API/Program.cs`
- Method: top-level startup.
- Responsibility: configure services, build the application, configure middleware, and map endpoints.

Mapped at startup:

1. `app.MapGet("/", ...)` - public root status response.
2. `app.MapAuthModule()` - Auth group.
3. `app.MapAdminModule()` - Admin group.
4. `app.MapSampleModule()` - Sample group.

### Auth endpoints

- File: `src/API/Friday.API/Modules/Auth/AuthEndpoints.cs`
- Class: `AuthEndpoints`
- Method: `MapAuthModule`
- Group: `/api/auth`, OpenAPI tag `Auth`.
- Routes: POST register, login, refresh, logout.
- Group authorization metadata: none.
- Delegate pattern: bind request command, inject `IMediator` and `CancellationToken`, call `SendAsync`, wrap through `ApiResults.Ok`.

### Admin endpoints

- File: `src/API/Friday.API/Modules/Admin/AdminEndpoints.cs`
- Class: `AdminEndpoints`
- Method: `MapAdminModule`
- Group: `/api/admin`, OpenAPI tag `Admin`.
- Authorization: group-level `.RequireAuthorization()`.
- Routes: user, role, and right command/query endpoints.
- Delegate pattern: bind route/body values, construct or receive command/query, dispatch through mediator, wrap through `ApiResults.Ok`.

### Sample endpoints

- File: `src/API/Friday.API/Modules/Sample/SampleEndpoints.cs`
- Class: `SampleEndpoints`
- Method: `MapSampleModule`
- Group: `/api/sample`, OpenAPI tag `Sample`.
- Routes: public todo POST/GET.

Endpoint methods remain thin HTTP adapters. They do not directly call repositories or DbContext.

## 2. Service Registration Relevant to Requests

Source order in `Program.cs`:

```text
Serilog host integration
OpenTelemetry
BuildingBlocks Application/Infrastructure
localization and registration options
IErrorMessageLocalizer
LinKit CQRS
Admin Application/Infrastructure
Sample Application/Infrastructure
IHttpContextAccessor
JWT bearer authentication
authorization defaults
Swagger/OpenAPI
```

Authentication uses the JWT bearer default scheme. Authorization uses `AddAuthorization()` without named policies, a custom default policy, or a fallback policy.

## 3. Middleware Order

Explicit source order after application build and optional startup migrations:

1. `CorrelationIdMiddleware`
2. Serilog request logging
3. `ExceptionHandlingMiddleware`
4. Swagger and Swagger UI, Development only
5. HTTPS redirection
6. Authentication
7. Authorization
8. `AuthenticatedUserValidationMiddleware`
9. Mapped endpoint execution

Evidence:

- File: `src/API/Friday.API/Program.cs`
- Lines/symbols: `UseMiddleware<CorrelationIdMiddleware>`, `UseSerilogRequestLogging`, `UseMiddleware<ExceptionHandlingMiddleware>`, `UseHttpsRedirection`, `UseAuthentication`, `UseAuthorization`, `UseMiddleware<AuthenticatedUserValidationMiddleware>`.

ASP.NET Core may insert routing infrastructure implicitly for mapped minimal endpoints. No explicit `UseRouting` appears, so its exact generated/internal placement is **UNKNOWN** from application source alone.

### Ordering implications

- Correlation/log context wraps all later application middleware.
- Serilog request logging measures all downstream work.
- Exception handling catches exceptions thrown by HTTPS/authentication/authorization/session validation/endpoints when those components propagate exceptions.
- Authentication constructs the principal before authorization.
- Authorization evaluates endpoint metadata before database-backed session/user validation middleware.
- Session validation can still stop the request before the endpoint executes.

## 4. Authentication Middleware

### Registration

- File: `src/API/Friday.API/Program.cs`
- Method: top-level `AddAuthentication(...).AddJwtBearer(...)` configuration.
- Scheme: `JwtBearerDefaults.AuthenticationScheme`.

Token validation parameters explicitly enable:

- issuer validation;
- audience validation;
- lifetime validation;
- issuer-signing-key validation.

Issuer, audience, and symmetric signing key come from `Authentication:Jwt`.

Not configured in source:

- `JwtBearerEvents`;
- custom `OnChallenge` response;
- custom `OnForbidden` response;
- custom authentication failure body;
- explicit inbound claim mapping;
- explicit clock skew.

Therefore the exact payload for cryptographic authentication failure/challenge is **UNKNOWN** without runtime verification; the application does not explicitly put it in `ApiResponse` format.

### Per-request database session validation

- File: `src/API/Friday.API/Middlewares/AuthenticatedUserValidationMiddleware.cs`
- Class: `AuthenticatedUserValidationMiddleware`
- Method: `InvokeAsync`.

Behavior:

- If unauthenticated, immediately calls the next middleware.
- Resolves user ID from `ClaimTypes.NameIdentifier` or JWT `sub`.
- Resolves session GUID from JWT `jti`.
- Loads `UserSession` and validates existence, revocation, refresh-session expiry, user association.
- Loads `User` and validates existence, active state, and lock state.
- Returns 401/403 without calling next when validation fails.

These failures use localized `ApiResponse.Fail` JSON and a trace ID.

This middleware runs for every authenticated request, including authenticated requests to endpoints without authorization metadata.

## 5. Authorization Middleware

- Registration: `builder.Services.AddAuthorization()`.
- Execution: `app.UseAuthorization()` after authentication.
- Protected endpoint metadata: `.RequireAuthorization()` on the entire Admin group.

Confirmed authorization rule:

- Admin endpoints require an authenticated principal under the framework default policy.
- No named policy, role requirement, right/permission requirement, resource handler, or custom authorization handler exists.

Not configured in source:

- custom forbidden response body;
- custom challenge response body;
- status-code pages.

Exact framework response bodies for authorization-generated 401/403 are **UNKNOWN** without runtime execution and are not explicitly created by `ApiResponse` helpers.

## 6. Exception Handling

- File: `src/API/Friday.API/Middlewares/ExceptionHandlingMiddleware.cs`
- Class: `ExceptionHandlingMiddleware`
- Methods: `InvokeAsync`, private `Map`.

It wraps downstream execution in `try/catch`, maps the exception, localizes its message, logs it at Error, and writes JSON.

Mappings:

| Exception | HTTP status | Error code |
|---|---:|---|
| `FridayException` | exception-defined | exception-defined |
| `KeyNotFoundException` | 404 | `NOT_FOUND` |
| `ArgumentException` | 400 | `BAD_REQUEST` |
| `InvalidOperationException` | 400 | `BAD_REQUEST` |
| any other exception | 500 | `INTERNAL_SERVER_ERROR` |

For unknown exceptions, the public message is generic. For the other mapped exception types, the exception message is used as localization fallback.

### Localization

- File: `src/API/Friday.API/Common/ErrorMessageLocalizer.cs`
- Class: `ErrorMessageLocalizer`
- Method: `GetMessageAsync`.

The middleware supplies the request `Accept-Language`. The localizer derives a module from the error-code prefix, tries normalized language, base language, then English, caches successful lookups in memory, and falls back to the source message.

### Logging behavior

The exception middleware logs every caught exception at Error, including expected `FridayException` cases such as invalid credentials and not-found errors.

## 7. Validation and Validation Errors

Dedicated validation framework: **NOT_IMPLEMENTED**.

No source usage was found for:

- FluentValidation;
- `IValidator<T>` / `AbstractValidator<T>`;
- validation pipeline behavior;
- MVC model-state configuration;
- minimal-API endpoint validation filter;
- `Results.ValidationProblem`.

Current validation sources:

1. Minimal API framework binding for route/body parameters.
2. Inline application-handler checks that throw `FridayException`.
3. Domain guard clauses that throw `ArgumentException` or guard exceptions.
4. Database constraints after handler execution.

Confirmed custom error behavior:

- `FridayException` validation/business failures are mapped by `ExceptionHandlingMiddleware`.
- Domain `ArgumentException` is mapped to HTTP 400 `BAD_REQUEST`.

Framework binding/deserialization failures:

- Application source supplies no custom formatter.
- Whether a specific failure reaches the custom middleware or is formatted internally by ASP.NET Core depends on framework behavior.
- Exact status/body is **UNKNOWN** without runtime verification.

There is no structured field-error dictionary or multi-error validation response in application source.

## 8. Response and Error Format

### Success envelope

- File: `src/API/Friday.API/Common/ApiResponse.cs`
- Type: `ApiResponse<T>`.
- File: `src/API/Friday.API/Common/ApiResults.cs`
- Method: `ApiResults.Ok`.

Shape:

```text
Code
Message
Data
TraceId
```

`ApiResults.Ok` returns HTTP 200 with code `SUCCESS`, message `Success`, data, and trace ID unless callers override code/message.

### Custom failure envelope

- Type: non-generic `ApiResponse`.
- Factory: `ApiResponse.Fail`.

Shape:

```text
Code
Message
Data = null
TraceId
```

Used by:

- `ExceptionHandlingMiddleware`;
- `AuthenticatedUserValidationMiddleware`.

### ProblemDetails

- RFC 7807 `ProblemDetails` registration/use: **NOT_IMPLEMENTED**.
- `AddProblemDetails`: not found.
- `UseExceptionHandler`: not found.
- `Results.Problem` / `Results.ValidationProblem`: not found.

`ApiResponse` is a custom envelope, not `ProblemDetails`.

### Format consistency boundary

Custom envelope coverage is **PARTIAL**:

- endpoint success: `ApiResponse<T>`;
- caught application/domain exceptions: `ApiResponse`;
- database-backed session/user failure: `ApiResponse`;
- JWT challenge/forbidden: application format **NOT_CONFIGURED**;
- route not found/method not allowed: application format **NOT_CONFIGURED**;
- framework request-binding failure: application format **NOT_CONFIGURED**.

The exact framework-generated bodies for the last three categories are **UNKNOWN** without runtime verification.

## 9. Correlation and Request IDs

- File: `src/API/Friday.API/Middlewares/CorrelationIdMiddleware.cs`
- Class: `CorrelationIdMiddleware`
- Method: `InvokeAsync`.

Correlation ID selection:

1. first request `X-Correlation-Id` value;
2. current `Activity.TraceId`;
3. `HttpContext.TraceIdentifier`.

Behavior:

- echoes the chosen value in response header `X-Correlation-Id`;
- adds `CorrelationId` to Serilog `LogContext` for downstream logs;
- performs no validation, normalization, length limit, or generated-format enforcement on a supplied value.

Response envelope trace ID selection:

1. `Activity.Current.TraceId`;
2. `HttpContext.TraceIdentifier`.

Therefore a caller-supplied `X-Correlation-Id` can differ from `ApiResponse.TraceId`. Both identifiers can be present: correlation ID in response header/log property and trace ID in the JSON envelope/request log diagnostic context.

## 10. Request Logging and Observability

### Serilog host configuration

- File: `src/API/Friday.API/Configuration/FridaySerilogWebApplicationBuilderExtensions.cs`
- Class: `FridaySerilogWebApplicationBuilderExtensions`
- Method: `AddFridaySerilog`.

Confirmed behavior:

- Serilog replaces/integrates with host logging.
- Adds `Application = Friday.API`.
- Uses configured enrichers and sinks.
- Can write OTLP logs when configured with a valid endpoint.
- Otherwise uses classic configuration and a rolling compact-JSON file under `logs/log-.json`.
- Rolling file: daily, 100 MiB size limit, 14 retained files, shared access, one-second flush interval.

### Request logging middleware

- File: `src/API/Friday.API/Program.cs`
- Method: `UseSerilogRequestLogging` configuration.

Diagnostic properties:

- `RequestHost`;
- `RequestScheme`;
- `UserAgent`;
- `TraceId`.

Level selection:

- Error when downstream exception reaches request logger or response status is 500+;
- Warning when elapsed time exceeds 500 ms;
- Information otherwise.

Because `ExceptionHandlingMiddleware` is downstream and converts caught exceptions into responses, an expected caught 4xx is logged separately at Error by exception middleware while the completed request can be logged at Information (or Warning if slow) by Serilog request logging. This follows directly from middleware order and the configured level predicates.

### OpenTelemetry

- File: `src/API/Friday.API/Configuration/OpenTelemetryServiceCollectionExtensions.cs`
- Class: `OpenTelemetryServiceCollectionExtensions`
- Method: `AddFridayOpenTelemetry`.

When enabled:

- resource service name is configured;
- tracing uses an always-on sampler;
- ASP.NET Core instrumentation records exceptions;
- HTTP client instrumentation is enabled;
- ASP.NET Core, HTTP client, and runtime metrics are enabled;
- OTLP export is added only when a valid endpoint is configured.

## 11. Health Endpoints

- `AddHealthChecks`: **NOT_IMPLEMENTED**.
- `MapHealthChecks`/health endpoint mapping: **NOT_IMPLEMENTED**.
- Liveness endpoint: **NOT_IMPLEMENTED**.
- Readiness endpoint: **NOT_IMPLEMENTED**.

The public root `GET /` returns a status message but does not check dependencies and is not registered through ASP.NET Core health checks. It is a simple status endpoint, not confirmed liveness/readiness.

## 12. Successful Request Trace

Representative request: authenticated HTTPS `GET /api/admin/users` with valid JWT, active session, and active/unlocked user.

### Step 1: correlation

- File: `src/API/Friday.API/Middlewares/CorrelationIdMiddleware.cs`
- Class/method: `CorrelationIdMiddleware.InvokeAsync`.
- Selects/echoes correlation ID and pushes it into Serilog context.

### Step 2: request measurement/log scope

- File: `src/API/Friday.API/Program.cs`
- Method: `UseSerilogRequestLogging` delegate configuration.
- Starts Serilog request timing and later enriches/completes the request log.

### Step 3: exception boundary

- File: `src/API/Friday.API/Middlewares/ExceptionHandlingMiddleware.cs`
- Class/method: `ExceptionHandlingMiddleware.InvokeAsync`.
- Opens the downstream `try/catch`; no exception occurs in this trace.

### Step 4: HTTPS

- File: `src/API/Friday.API/Program.cs`
- Middleware: `UseHttpsRedirection`.
- Request is already HTTPS, so execution continues.

### Step 5: JWT authentication

- File: `src/API/Friday.API/Program.cs`
- Middleware: `UseAuthentication`.
- Validates token and establishes authenticated `ClaimsPrincipal`.

### Step 6: endpoint authorization

- File: `src/API/Friday.API/Program.cs`; middleware: `UseAuthorization`.
- File: `src/API/Friday.API/Modules/Admin/AdminEndpoints.cs`; method: `MapAdminModule`.
- Evaluates group-level `RequireAuthorization`; authenticated principal satisfies current default requirement.

### Step 7: session/user validation

- File: `src/API/Friday.API/Middlewares/AuthenticatedUserValidationMiddleware.cs`
- Class/method: `AuthenticatedUserValidationMiddleware.InvokeAsync`.
- Reads `sub`/name identifier and `jti`, loads session and user, validates session and user status, calls next.

### Step 8: endpoint and query

- File: `src/API/Friday.API/Modules/Admin/AdminEndpoints.cs`
- Method: `MapAdminModule`, `GET /users` delegate.
- Calls `mediator.QueryAsync(new GetUsersQuery(), cancellationToken)`.
- File: `src/Modules/Admin/Friday.Modules.Admin.Application/Features/Users/GetUsers.cs`
- Class/method: `GetUsersHandler.HandleAsync`.
- Loads users through `IUserRepository.ListAsync` and maps them to `UserDto[]`.

### Step 9: success response

- File: `src/API/Friday.API/Common/ApiResults.cs`
- Method: `Ok`.
- Returns HTTP 200 `ApiResponse<IReadOnlyList<UserDto>>` with `SUCCESS` and trace ID.

### Step 10: completion logging

- Serilog request logging records completion with host, scheme, user agent, trace ID, elapsed time, status, and correlation property from outer log context.

## 13. Failed Request Trace

Representative request: HTTPS `POST /api/auth/login` with an unknown login or incorrect password.

### Step 1: outer middleware

- Correlation middleware selects/echoes ID.
- Serilog request logging starts timing.
- Exception middleware opens its `try/catch`.
- HTTPS redirection allows the HTTPS request through.

### Step 2: authentication and authorization

- No valid authenticated principal is required for the public Auth group.
- Authentication middleware can leave the request unauthenticated.
- Authorization finds no endpoint authorization requirement.
- `AuthenticatedUserValidationMiddleware` sees unauthenticated identity and calls next without database session validation.

### Step 3: endpoint dispatch

- File: `src/API/Friday.API/Modules/Auth/AuthEndpoints.cs`
- Method: `MapAuthModule`, login delegate.
- Binds `LoginCommand` and calls `IMediator.SendAsync`.

### Step 4: command transaction and handler

- File: `src/BuildingBlocks/Friday.BuildingBlocks.Application/Behaviors/TransactionBehavior.cs`
- Class/method: `TransactionBehavior.HandleAsync`.
- Begins command transaction and invokes handler.
- File: `src/Modules/Admin/Friday.Modules.Admin.Application/Features/Auth/Login.cs`
- Class/method: `LoginCommandHandler.HandleAsync`.
- User lookup returns no password credential or password verification returns `Failed`.
- Handler throws `FridayException` with `ADMIN_INVALID_CREDENTIALS`, HTTP 401, message `Invalid login or password.`

### Step 5: rollback and propagation

- Transaction behavior catches the exception, calls `RollbackAsync`, and rethrows.
- Endpoint does not create a success response.

### Step 6: error mapping/localization

- Exception middleware catches `FridayException`.
- Resolves localized message from `Accept-Language`, falling back to the exception message.
- Logs the exception at Error with code, exception message, and trace ID.
- Writes HTTP 401 JSON `ApiResponse`:

```text
Code = ADMIN_INVALID_CREDENTIALS
Message = localized or fallback message
Data = null
TraceId = Activity trace ID or HttpContext trace identifier
```

### Step 7: completion logging

- Exception is handled before returning to Serilog request logging.
- The request logger observes a 401 response rather than a propagated exception.
- It logs at Information unless elapsed time exceeds 500 ms, in which case Warning.
- Response includes the outer `X-Correlation-Id` header.

## 14. Confirmed Gaps and Unknowns

1. No application-configured ProblemDetails/RFC 7807 pipeline.
2. No standardized application envelope for framework-generated authentication challenge/forbidden responses.
3. No standardized application envelope for route-not-found/method-not-allowed responses.
4. No dedicated validation pipeline or field-error response.
5. Exact malformed-body/binding error format is unknown without runtime verification.
6. Expected application 4xx exceptions are logged at Error by exception middleware.
7. Client-supplied correlation IDs are trusted and can differ from response-body trace IDs.
8. No health/liveness/readiness endpoints.
9. No status-code-pages middleware.
10. Exact implicit routing placement is not explicit in source.
