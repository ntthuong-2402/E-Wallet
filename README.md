# Friday Modular Monolith

Code base `Friday` duoc scaffold theo huong:

- Single deployable (`Friday.API`)
- Modular architecture (`src/Modules/*`)
- Clean architecture (Domain -> Application -> Infrastructure -> API)
- DDD tactical patterns (Entity, AggregateRoot, ValueObject, DomainEvent)
- `BuildingBlocks` cho logic dung chung nhu event, unit of work, LinqToDB connection factory

## Current Modules

- `Sample`: module mau de tham khao cach to chuc module.

## Run

```bash
dotnet run --project src/API/Friday.API/Friday.API.csproj
```

Mặc định cấu hình dùng **PostgreSQL** và Redis (localhost). Có thể chạy Postgres + Redis + API bằng Docker Compose; chi tiết lệnh migration và deploy: **[docker/README.md](docker/README.md)**.

## Docker

```bash
docker compose up -d --build
```

API: `http://localhost:8080`. **Jaeger (trace OTLP):** `http://localhost:16686`. Chi tiết: [docker/README.md](docker/README.md).

## EF Core — `dotnet ef` và chỗ file migration

**Thư mục file migration** (sau khi chạy `migrations add`, EF ghi vào đây):

`src/BuildingBlocks/Friday.BuildingBlocks.Infrastructure/Migrations/`

Mỗi lần add sẽ có thêm (tên + timestamp thay đổi theo máy bạn):

- `20xxxxxxxxxxxx_TenBanDat.cs` — lớp `Up` / `Down`
- `20xxxxxxxxxxxx_TenBanDat.Designer.cs`
- `FridayDbContextModelSnapshot.cs` (một file, cập nhật liên tục)

Cài tool (một lần):

```bash
dotnet tool install --global dotnet-ef
```

**Liệt kê migration đã có** (không tạo file):

```bash
dotnet ef migrations list --project src/BuildingBlocks/Friday.BuildingBlocks.Infrastructure/Friday.BuildingBlocks.Infrastructure.csproj --startup-project src/API/Friday.API/Friday.API.csproj --context FridayDbContext
```

**Tạo migration mới** (sẽ sinh file trong thư mục trên):

```bash
dotnet ef migrations add TenBanDat --project src/BuildingBlocks/Friday.BuildingBlocks.Infrastructure/Friday.BuildingBlocks.Infrastructure.csproj --startup-project src/API/Friday.API/Friday.API.csproj --context FridayDbContext
```

**Áp vào PostgreSQL** (cần DB đang chạy, connection khớp `appsettings`):

```bash
dotnet ef database update --project src/BuildingBlocks/Friday.BuildingBlocks.Infrastructure/Friday.BuildingBlocks.Infrastructure.csproj --startup-project src/API/Friday.API/Friday.API.csproj --context FridayDbContext
```

**Gỡ migration cuối** (chỉ khi chưa `database update` hoặc đã rollback tương ứng):

```bash
dotnet ef migrations remove --project src/BuildingBlocks/Friday.BuildingBlocks.Infrastructure/Friday.BuildingBlocks.Infrastructure.csproj --startup-project src/API/Friday.API/Friday.API.csproj --context FridayDbContext
```

Design-time dùng `src/API/Friday.API/DesignTimeDbContextFactory.cs`. Tùy chọn set `FRIDAY_DESIGN_TIME_PG` nếu connection khác mặc định — xem thêm [docker/README.md](docker/README.md).

Sample endpoints:

- `POST /api/sample/todos`
- `GET /api/sample/todos`

Admin endpoints:

- `POST /api/admin/users`
- `GET /api/admin/users`
- `POST /api/admin/users/{userId}/roles/{roleId}`
- `POST /api/admin/users/{userId}/lock`
- `POST /api/admin/roles`
- `GET /api/admin/roles`
- `POST /api/admin/roles/{roleId}/rights` (body: `int[]`)
- `POST /api/admin/rights`
- `GET /api/admin/rights`
