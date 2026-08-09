# Friday Local Runtime and Docker Pattern

Scope: current checked-in local launch and Docker runtime configuration only. Apart from creating this requested state document, no existing files, containers, images, volumes, or networks were modified.

Status vocabulary: **CONFIRMED**, **NOT_IMPLEMENTED**, **UNKNOWN**.

## 1. Runtime Summary

The checked-in Docker topology defines four services:

1. `postgres`
2. `redis`
3. `jaeger`
4. `friday-api`

RabbitMQ is not defined or referenced.

Configured topology: **CONFIRMED** from `docker-compose.yml`.

Actual container/image/network/volume runtime state: **UNKNOWN**. The `docker` command is not installed or not available on PATH in the current environment, so `docker compose config` and `docker compose ps` could not run.

## 2. Runtime Files

| File | Status | Responsibility |
|---|---|---|
| `docker-compose.yml` | **CONFIRMED** | Defines the four-service local topology, ports, volumes, environment variables, health/dependency conditions, and API image build. |
| `docker/Dockerfile` | **CONFIRMED** | Multi-stage build and runtime image for Friday.API. |
| `.dockerignore` | **CONFIRMED** | Excludes Git/editor/build/log/node artifacts from the Docker build context. |
| `src/API/Friday.API/appsettings.Docker.json` | **CONFIRMED** | Docker-environment configuration overlay. |
| `src/API/Friday.API/appsettings.json` | **CONFIRMED** | Base configuration inherited by the Docker environment where Docker-specific values do not override it. |
| `src/API/Friday.API/Properties/launchSettings.json` | **CONFIRMED** | Local non-container Development launch profiles and ports. |

No additional Dockerfile or Compose file was found outside ignored build folders.

## 3. Service Classification

| Service/capability | Classification | Evidence |
|---|---|---|
| Friday API container definition | **CONFIRMED** | `friday-api` in `docker-compose.yml`. |
| PostgreSQL container definition | **CONFIRMED** | `postgres:16-alpine`. |
| Redis container definition | **CONFIRMED** | `redis:7-alpine`. |
| Jaeger/OTLP container definition | **CONFIRMED** | `jaegertracing/all-in-one:1.76.0`. |
| RabbitMQ/message broker | **NOT_IMPLEMENTED** | No Compose service, environment key, package/configuration reference, or AMQP port found. |
| API container health check | **NOT_IMPLEMENTED** | No `healthcheck` on `friday-api`; no Dockerfile `HEALTHCHECK`. |
| PostgreSQL health check | **CONFIRMED** | Compose uses `pg_isready -U friday -d friday`. |
| Redis health check | **NOT_IMPLEMENTED** | No Redis `healthcheck`. |
| Jaeger health check | **NOT_IMPLEMENTED** | No Jaeger `healthcheck`. |
| ASP.NET health endpoint | **NOT_IMPLEMENTED** | No `AddHealthChecks` or `MapHealthChecks`; root endpoint is only a status response. |
| Container TLS termination | **NOT_IMPLEMENTED** in supplied Docker topology | API is configured and published as HTTP only; no reverse proxy/TLS service is defined. |
| Actual running service state | **UNKNOWN** | Docker CLI unavailable in the analysis environment. |

## 4. Friday API Image

- File: `docker/Dockerfile`
- Build context: repository root, as configured by Compose.
- Dockerfile path: `docker/Dockerfile`.

### Build stage

- Base image: `mcr.microsoft.com/dotnet/sdk:10.0`.
- Working directory: `/src`.
- Copies the repository `src/` directory into the image.
- Changes to `/src/API/Friday.API`.
- Publishes `Friday.API.csproj` in Release mode to `/app/publish`.
- Sets `/p:UseAppHost=false`.

### Runtime stage

- Base image: `mcr.microsoft.com/dotnet/aspnet:10.0`.
- Working directory: `/app`.
- Exposes TCP port `8080` as image metadata.
- Sets `ASPNETCORE_URLS=http://+:8080`.
- Copies only published output from the build stage.
- Starts with `dotnet Friday.API.dll`.

Image build style: **CONFIRMED multi-stage Linux .NET build**.

Explicit non-root `USER`: **NOT_IMPLEMENTED** in the Dockerfile. The effective default identity of the referenced runtime image was not inspected and is therefore **UNKNOWN**.

Dockerfile health check: **NOT_IMPLEMENTED**.

Image digest pinning: **NOT_IMPLEMENTED**. The SDK/runtime use the mutable `10.0` tag.

## 5. Docker Compose Topology

Compose file: `docker-compose.yml`.

### `postgres`

Classification: **CONFIRMED configured service**.

- Image: `postgres:16-alpine`.
- Container environment:
  - `POSTGRES_USER=friday`
  - `POSTGRES_PASSWORD=friday`
  - `POSTGRES_DB=friday`
- Host/container port: `5432:5432`.
- Persistent named volume: `postgres_data:/var/lib/postgresql/data`.
- Health command: `pg_isready -U friday -d friday`.
- Health timing: 5-second interval, 5-second timeout, 10 retries.

The image is version-family tagged rather than digest pinned. Actual image version/runtime state: **UNKNOWN**.

### `redis`

Classification: **CONFIRMED configured service**.

- Image: `redis:7-alpine`.
- Host/container port: `6379:6379`.
- Command: `redis-server --appendonly yes`.
- Persistent named volume: `redis_data:/data`.
- Health check: **NOT_IMPLEMENTED**.
- Authentication/TLS configuration: **NOT_IMPLEMENTED** in Compose.

Actual Redis connectivity and persistence state: **UNKNOWN**.

### `jaeger`

Classification: **CONFIRMED configured service**.

- Image: `jaegertracing/all-in-one:1.76.0`.
- `COLLECTOR_OTLP_ENABLED=true`.
- Ports:
  - `16686:16686` for the Jaeger UI.
  - `4317:4317` for OTLP gRPC.
  - `4318:4318` for OTLP HTTP.
- Persistent volume: **NOT_IMPLEMENTED**.
- Health check: **NOT_IMPLEMENTED**.

Actual telemetry receipt/retention: **UNKNOWN**.

### `friday-api`

Classification: **CONFIRMED configured service**.

- Built locally from the repository root and `docker/Dockerfile`.
- Host/container port: `8080:8080`.
- Environment: `ASPNETCORE_ENVIRONMENT=Docker`.
- Listener: `ASPNETCORE_URLS=http://+:8080`.
- Persistent application/log volume: **NOT_IMPLEMENTED**.
- Container health check: **NOT_IMPLEMENTED**.

Actual API startup and request reachability: **UNKNOWN**.

## 6. Exposed Ports

| Host port | Container/service | Purpose | Status |
|---:|---|---|---|
| 8080 | `friday-api:8080` | HTTP API | **CONFIRMED configured** |
| 5432 | `postgres:5432` | PostgreSQL | **CONFIRMED configured** |
| 6379 | `redis:6379` | Redis | **CONFIRMED configured** |
| 16686 | `jaeger:16686` | Jaeger UI | **CONFIRMED configured** |
| 4317 | `jaeger:4317` | OTLP gRPC | **CONFIRMED configured** |
| 4318 | `jaeger:4318` | OTLP HTTP | **CONFIRMED configured** |
| 5672 / 15672 | RabbitMQ | AMQP / management | **NOT_IMPLEMENTED** |

All database, cache, and telemetry ports are published to the host rather than being limited to the Compose network.

## 7. Container Dependencies and Startup Order

The only explicit dependency edges are from `friday-api`:

```text
postgres --service_healthy--> friday-api
redis ----service_started---> friday-api
jaeger ---service_started---> friday-api
```

### PostgreSQL dependency

Status: **CONFIRMED health-gated**.

`friday-api` waits for the PostgreSQL health check to report healthy before Compose starts it.

### Redis dependency

Status: **CONFIRMED start-gated only**.

The condition is `service_started`, not readiness. Redis can be started without Compose proving it is ready to accept requests.

### Jaeger dependency

Status: **CONFIRMED start-gated only**.

The condition is `service_started`, not readiness. Compose does not prove OTLP readiness before starting the API.

### API readiness propagation

Status: **NOT_IMPLEMENTED**.

Because `friday-api` has no health check, Compose cannot expose or use an application readiness state for downstream consumers.

## 8. Docker Environment Variables

The API service sets:

| Variable | Effective purpose |
|---|---|
| `ASPNETCORE_ENVIRONMENT=Docker` | Selects the Docker environment and its JSON overlay under the standard ASP.NET Core host. |
| `ASPNETCORE_URLS=http://+:8080` | Binds Kestrel to HTTP on all interfaces at port 8080. |
| `ConnectionStrings__FridayDb` | Connects the shared DbContext/migrations to `postgres:5432`. |
| `ConnectionStrings__AdminDatabase` | Supplies a second equivalent connection string; current production C# does not consume it. |
| `ConnectionStrings__Redis` | Redis fallback connection using service DNS. |
| `Database__Provider=PostgreSql` | Selects PostgreSQL infrastructure. |
| `Database__ApplyMigrationsOnStartup=true` | Enables EF migrations followed by data migrations during API startup. |
| `Cache__UseRedis=true` | Selects Redis-backed cache service. |
| `Cache__RedisConnectionString` | Direct Redis endpoint override. |
| `Authentication__Jwt__Secret` | Overrides the base JWT signing secret. |
| `OpenTelemetry__OtlpEndpoint` | Sends configured trace/metric export to Jaeger OTLP gRPC. |

Environment-variable nested keys use double underscores and override JSON configuration under the standard host.

## 9. Effective Docker Application Configuration

- File: `src/API/Friday.API/appsettings.Docker.json`.
- Overlay selection: `ASPNETCORE_ENVIRONMENT=Docker`.

Docker JSON confirms:

- PostgreSQL service DNS connection.
- Redis service DNS connection and Redis cache enabled.
- PostgreSQL provider.
- startup migrations enabled.
- OpenTelemetry enabled with service name `Friday.API` and Jaeger endpoint.
- public registration enabled.
- classic Serilog console sink.

Values inherited from base `appsettings.json` unless overridden include JWT issuer/audience/token lifetimes, localization cache duration, Serilog levels/enrichers, `OpenTelemetry:LogExport=Serilog`, and allowed hosts.

Compose overrides the Docker JSON/base values for the principal connection strings, flags, OTLP endpoint, and JWT secret.

## 10. Database Runtime Pattern

Database service: **CONFIRMED PostgreSQL 16 Alpine configuration**.

- One logical database: `friday`.
- One configured application credential pair: `friday` / `friday`.
- API connects by Compose DNS name `postgres`.
- Data is persisted in the named volume `postgres_data`.
- API startup is health-gated on `pg_isready`.
- API startup migrations are enabled in both Docker JSON and Compose environment.

Migration success, applied schema version, database contents, and volume existence: **UNKNOWN** because the Docker runtime could not be queried.

Database backup/restore automation: **NOT_IMPLEMENTED** in the supplied Docker setup.

## 11. Redis Runtime Pattern

Redis service: **CONFIRMED configured and enabled for Docker**.

- API address: `redis:6379`.
- Append-only persistence is enabled.
- Named volume: `redis_data`.
- Key instance prefix from Docker JSON: `Friday:`.
- Compose publishes Redis directly on host port 6379.

Redis authentication, TLS, readiness health check, and resource limits: **NOT_IMPLEMENTED** in supplied configuration.

Actual cache selection/connectivity at runtime: **UNKNOWN**.

## 12. RabbitMQ Runtime Pattern

RabbitMQ: **NOT_IMPLEMENTED**.

No RabbitMQ container, AMQP environment variable, RabbitMQ package/configuration reference, exchange/queue setup, port mapping, volume, health check, or dependency edge was found in the inspected runtime/source configuration.

## 13. Health and Readiness

| Check | Classification | Detail |
|---|---|---|
| PostgreSQL container health | **CONFIRMED** | `pg_isready`; gates API startup. |
| Redis container health | **NOT_IMPLEMENTED** | API depends only on service start. |
| Jaeger container health | **NOT_IMPLEMENTED** | API depends only on service start. |
| API container health | **NOT_IMPLEMENTED** | No Compose or Dockerfile health check. |
| ASP.NET Core health checks | **NOT_IMPLEMENTED** | No health service/endpoint registration. |
| Liveness endpoint | **NOT_IMPLEMENTED** | Root status route is not a dependency-aware health check. |
| Readiness endpoint | **NOT_IMPLEMENTED** | No readiness checks for DB/Redis/telemetry. |

## 14. Local Non-Docker Runtime

- File: `src/API/Friday.API/Properties/launchSettings.json`.
- Both launch profiles set `ASPNETCORE_ENVIRONMENT=Development`.
- HTTP profile: `http://localhost:5035`.
- HTTPS profile: `https://localhost:7041;http://localhost:5035`.
- Both request opening `swagger` in a browser.

The Development settings point PostgreSQL and Redis configuration to localhost. Redis use remains disabled, startup migrations remain disabled, and public registration is enabled.

- File: `src/API/Friday.API/Friday.API.http`.
- Targets `http://localhost:5035/weatherforecast/`.
- Current API source does not map `/weatherforecast/`, so this request file is stale relative to the current endpoint map.

Whether PostgreSQL, Redis, Jaeger, or the API is currently running locally outside Docker: **UNKNOWN**; no process/socket/database probes were performed, and Docker itself is unavailable.

## 15. Security and Operational Configuration Facts

These are source facts, not runtime penetration-test results:

- Compose and Docker JSON contain literal development PostgreSQL credentials.
- Compose contains a literal placeholder-style JWT signing secret.
- Redis is published without configured authentication or TLS.
- PostgreSQL and telemetry collector ports are published to the host.
- The API is HTTP-only inside/published by Compose; no reverse proxy or TLS termination is supplied.
- Public registration is enabled in the Docker environment.
- Startup database migrations run as part of API process startup.
- No CPU/memory limits, restart policies, log volume, or application health check are configured.
- Named volumes make PostgreSQL and Redis data persist across ordinary container replacement; actual volume state is **UNKNOWN**.

## 16. Final Classification

### CONFIRMED

- Four-service Compose definition: API, PostgreSQL, Redis, Jaeger.
- Multi-stage .NET 10 API Dockerfile.
- HTTP API on port 8080.
- PostgreSQL and Redis named volumes.
- PostgreSQL health check and health-gated API dependency.
- Redis and Jaeger start-only API dependencies.
- Docker-specific PostgreSQL, Redis, migrations, registration, and OTLP configuration.
- Local Development launch profiles on ports 5035 and 7041.

### NOT_IMPLEMENTED

- RabbitMQ.
- API, Redis, and Jaeger health checks.
- ASP.NET liveness/readiness endpoints.
- Docker TLS/reverse proxy.
- Explicit Dockerfile non-root user.
- Image digest pinning.
- Redis authentication/TLS.
- Container resource limits and restart policies.
- Database backup/restore automation.

### UNKNOWN

- Whether any container or local service is currently running.
- Effective resolved Compose model and image availability.
- Actual database migration/schema/data state.
- Actual PostgreSQL, Redis, API, or OTLP connectivity.
- Existing named volumes and their contents.
- Effective runtime user inherited from the .NET base image.
