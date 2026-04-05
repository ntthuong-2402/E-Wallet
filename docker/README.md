# Friday — Docker & PostgreSQL

Compose khởi chạy **PostgreSQL 16**, **Redis 7**, **Jaeger** (UI trace OpenTelemetry) và **Friday.API** (.NET 10). API dùng Npgsql (`Database:Provider = PostgreSql`).

## Yêu cầu

- [Docker](https://docs.docker.com/get-docker/) và Docker Compose v2.

## Chạy toàn bộ stack

Từ thư mục gốc repo (`Friday/`):

```bash
docker compose up -d --build
```

- API: `http://localhost:8080`
- PostgreSQL: `localhost:5432` (user / password / database: `friday`)
- Redis: `localhost:6379`
- **Jaeger UI** (trace): `http://localhost:16686` — chọn service `Friday.API`, gọi vài request vào API rồi Search trace.
- OTLP gRPC (nếu chạy API trên máy host nhưng gửi trace vào Jaeger trong Docker): `localhost:4317` — trong `appsettings.Development.json` hoặc user-secrets đặt `OpenTelemetry:OtlpEndpoint` = `http://localhost:4317`.

Dừng và xóa container (giữ volume DB):

```bash
docker compose down
```

Xóa luôn dữ liệu Postgres/Redis trong volume:

```bash
docker compose down -v
```

## Chỉ chạy Postgres + Redis (phát triển local)

Khi bạn muốn chạy API bằng `dotnet run` nhưng DB/Redis trong Docker:

```bash
docker compose up -d postgres redis
```

Connection string mặc định trong `appsettings.json` / `appsettings.Development.json` đã trỏ `localhost:5432` và Redis `localhost:6379`.  
Bật Redis cache trong Development: trong `appsettings.Development.json` đặt `Cache:UseRedis` = `true`.

## Build image API riêng

```bash
docker build -f docker/Dockerfile -t friday-api:latest .
```

## EF Core — tạo / cập nhật schema (PostgreSQL)

**Thư mục chứa file migration** (EF luôn ghi vào đây khi `migrations add`):

`src/BuildingBlocks/Friday.BuildingBlocks.Infrastructure/Migrations/`

**Xem danh sách migration** (không tạo file):

```bash
dotnet ef migrations list ^
  --project src/BuildingBlocks/Friday.BuildingBlocks.Infrastructure/Friday.BuildingBlocks.Infrastructure.csproj ^
  --startup-project src/API/Friday.API/Friday.API.csproj ^
  --context FridayDbContext
```

Chúng được sinh cho **PostgreSQL (Npgsql)**. Factory design-time nằm tại `Friday.API/DesignTimeDbContextFactory.cs` để `dotnet ef` load đủ assembly module (Admin, Sample).

### Biến môi trường (tùy chọn)

```bash
# Windows PowerShell
$env:FRIDAY_DESIGN_TIME_PG = "Host=127.0.0.1;Port=5432;Database=friday;Username=friday;Password=friday"

# Linux / macOS
export FRIDAY_DESIGN_TIME_PG="Host=127.0.0.1;Port=5432;Database=friday;Username=friday;Password=friday"
```

Nếu không set, factory dùng chuỗi mặc định `postgres`/`postgres` trên DB `friday` (đổi cho khớp môi trường của bạn).

### Tạo migration mới (sau khi đổi entity)

Chạy từ máy dev (Postgres đã listen, ví dụ sau `docker compose up -d postgres`):

```bash
dotnet ef migrations add TenMigrationMoi ^
  --project src/BuildingBlocks/Friday.BuildingBlocks.Infrastructure/Friday.BuildingBlocks.Infrastructure.csproj ^
  --startup-project src/API/Friday.API/Friday.API.csproj ^
  --context FridayDbContext
```

(Linux/macOS: bỏ `^`, xuống dòng hoặc dùng `\`.)

### Áp migration vào database

**Cách 1 — CLI:**

```bash
dotnet ef database update ^
  --project src/BuildingBlocks/Friday.BuildingBlocks.Infrastructure/Friday.BuildingBlocks.Infrastructure.csproj ^
  --startup-project src/API/Friday.API/Friday.API.csproj ^
  --context FridayDbContext
```

**Cách 2 — Khi chạy trong Docker:** trong compose, `Database__ApplyMigrationsOnStartup=true` và `appsettings.Docker.json` cũng bật; API sẽ gọi `MigrateAsync` lúc khởi động (xem `ApplyEfThenDataMigrationsAsync`).

### Gỡ migration vừa thêm (nếu chưa apply)

```bash
dotnet ef migrations remove ^
  --project src/BuildingBlocks/Friday.BuildingBlocks.Infrastructure/Friday.BuildingBlocks.Infrastructure.csproj ^
  --startup-project src/API/Friday.API/Friday.API.csproj ^
  --context FridayDbContext
```

### Ghi chú

- Cần cài tool: `dotnet tool install --global dotnet-ef` (hoặc dùng `dotnet ef` nếu đã có).
- **JWT:** `Program.cs` yêu cầu `Authentication:Jwt:Secret` tối thiểu 32 ký tự. Trong `docker-compose.yml` đã set biến môi trường `Authentication__Jwt__Secret` cho môi trường container — **đổi trước khi lên production**.
- **FluentMigrator** (data seed localization, v.v.) chạy sau EF migrate khi app start; với PostgreSQL, runner đã cấu hình `AddPostgres15_0()`.

## Sửa `FridayDbContext` và module

`FridayDbContext` áp dụng cấu hình EF từ:

1. Assembly `Friday.BuildingBlocks.Infrastructure`
2. `Friday.Modules.Admin.Infrastructure`
3. `Friday.Modules.Sample.Infrastructure`

Thêm module mới: bổ sung tên assembly vào mảng `ModuleConfigurationAssemblyNames` trong `FridayDbContext.cs`.
