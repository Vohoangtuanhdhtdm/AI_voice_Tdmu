# Tdmu.Advisor – Backend (Kế hoạch 1: nền tảng và kho tri thức)

API .NET 8 (minimal APIs) + PostgreSQL (pgvector). Admin nhập tri thức tuyển sinh, xuất bản thành *release*, API công khai chỉ đọc từ release đang hoạt động.

## 1. Cài đặt công cụ

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) (`dotnet --version` phải ra `8.0.x`).
- [Docker](https://docs.docker.com/get-docker/) (chạy PostgreSQL và chạy test).
- Công cụ EF Core (chỉ khi cần tạo migration): `dotnet tool install --global dotnet-ef --version 8.*`
  - Thư mục `~/.dotnet/tools` phải nằm trong `PATH`. Linux/macOS: `export PATH="$PATH:$HOME/.dotnet/tools"` (thêm vào `~/.bashrc` hoặc `~/.zshrc`). Windows: thêm `%USERPROFILE%\.dotnet\tools` vào biến môi trường `Path`.

## 2. Chạy PostgreSQL

Từ thư mục gốc repo:

```bash
cp .env.example .env        # rồi điền POSTGRES_PASSWORD (và các key khác nếu có)
docker compose up -d postgres
```

`docker-compose.yml` yêu cầu `POSTGRES_PASSWORD` trong `.env`; thiếu thì compose báo lỗi. Cơ sở dữ liệu nghe ở `localhost:5432` (user `postgres`).

## 3. Khai báo bí mật (user-secrets)

Bí mật không bao giờ được commit; `appsettings.json` chỉ chứa giá trị rỗng. Dùng user-secrets cho project Api (đã có `UserSecretsId`):

```bash
cd backend/src/Tdmu.Advisor.Api
dotnet user-secrets init   # không sao nếu đã có
dotnet user-secrets set "ConnectionStrings:Default" "Host=localhost;Port=5432;Database=tdmu_advisor;Username=postgres;Password=<POSTGRES_PASSWORD>"
dotnet user-secrets set "Auth:JwtKey" "<chuỗi ngẫu nhiên tối thiểu 32 ký tự>"
dotnet user-secrets set "Auth:SeedAdminEmail" "admin@local.test"
dotnet user-secrets set "Auth:SeedAdminPassword" "<mật khẩu mạnh>"
```

Cũng có thể dùng biến môi trường (dấu `:` thành `__`, ví dụ `Auth__JwtKey`). Thiếu hoặc quá ngắn `Auth:JwtKey` (< 32 ký tự) thì ứng dụng không khởi động.

## 4. Chạy API

```bash
cd backend/src/Tdmu.Advisor.Api
dotnet run
```

Môi trường mặc định khi `dotnet run` là Development: tự động áp migration và tạo tài khoản admin từ `Auth:SeedAdminEmail` / `Auth:SeedAdminPassword`. Xem cổng trong log (`Now listening on: ...`).

- Swagger UI: `http://localhost:<cổng>/swagger` (chỉ ở Development). Bấm **Authorize** và dán access token.
- Kiểm tra sống: `GET /health`.

### DỮ LIỆU MẪU

Muốn có sẵn vài ngành/điểm chuẩn để thử, bật cờ (chỉ có tác dụng ở Development):

```bash
dotnet user-secrets set "Seed:SampleData" "true"
```

Mặc định cờ này là `false`. Dữ liệu mẫu chỉ phục vụ phát triển, không dùng cho môi trường thật.

## 5. Chạy test

```bash
dotnet test backend
```

Test dùng Testcontainers nên **cần Docker đang chạy**; mỗi lần chạy tự dựng một PostgreSQL riêng, không đụng đến CSDL local của bạn.

## 6. Tạo migration

```bash
cd backend
ConnectionStrings__Default="Host=localhost;Port=5432;Database=tdmu_advisor;Username=postgres;Password=<POSTGRES_PASSWORD>" \
Auth__JwtKey="<chuỗi tối thiểu 32 ký tự>" \
dotnet ef migrations add <TenMigration> \
  --project src/Tdmu.Advisor.Api --startup-project src/Tdmu.Advisor.Api \
  -o Infrastructure/Migrations
```

`migrations add` không cần PostgreSQL đang chạy, nhưng `migrations remove` và `database update` thì cần (chạy `docker compose up -d postgres` ở mục 2). Hoàn tác migration vừa tạo (chưa áp):

```bash
dotnet ef migrations remove --project src/Tdmu.Advisor.Api --startup-project src/Tdmu.Advisor.Api
```

Migration được áp tự động khi chạy ở Development/Testing; xem lại file sinh ra trước khi commit.

## 7. Tham quan nhanh luồng xuất bản (qua Swagger)

1. `POST /api/auth/login` với email/mật khẩu admin, lấy `accessToken`, bấm **Authorize** và dán vào. (Sai mật khẩu 5 lần sẽ bị khóa 15 phút.)
2. CRUD tri thức ở `/api/admin/kb/{slug}`: thêm phương thức xét tuyển, ngành, điểm chuẩn (`mua-tuyen-sinh`, `phuong-thuc`, `to-hop`, `nganh`, `nganh-to-hop`, `chi-tieu`, `diem-chuan`, `hoc-phi`, `moc-thoi-gian`, ...). Có thể nhập hàng loạt qua `/api/admin/import/{kind}/template|preview|commit`.
3. `GET /api/admin/releases/diff` để xem khác biệt giữa bản nháp và release đang hoạt động.
4. `POST /api/admin/releases` (kèm ghi chú) để xuất bản bản nháp thành release mới và kích hoạt nó (chỉ một release hoạt động tại một thời điểm); `POST /api/admin/releases/{id}/activate` để chuyển lại sang một release cũ.
5. `GET /api/public/nganh` (không cần token) giờ thấy dữ liệu vừa xuất bản; `GET /api/public/nganh/{maNganh}` xem chi tiết.
6. Sửa bản nháp tiếp, hoặc `POST /api/admin/releases/{id}/restore-to-draft` để khôi phục một release cũ về bản nháp.

Toàn bộ `/api/admin/**` yêu cầu role `Admin`; `/api/public/**` giới hạn tốc độ theo IP (`RateLimit:PublicPermitsPerMinute`, mặc định 60/phút).
