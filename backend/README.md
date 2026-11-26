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

Test tự động không gọi Gemini thật (dùng `FakeGeminiClient`). Riêng smoke test `GeminiLive` gọi Gemini thật và tự bỏ qua (Skipped) khi thiếu `GEMINI_API_KEY`; CI chạy `dotnet test backend --filter "Category!=GeminiLive"`. Cách chạy smoke test: xem mục 8.

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
cd backend
ConnectionStrings__Default="Host=localhost;Port=5432;Database=tdmu_advisor;Username=postgres;Password=<POSTGRES_PASSWORD>" \
Auth__JwtKey="<chuỗi tối thiểu 32 ký tự>" \
dotnet ef migrations remove --project src/Tdmu.Advisor.Api --startup-project src/Tdmu.Advisor.Api
```

Migration được áp tự động khi chạy ở Development/Testing; xem lại file sinh ra trước khi commit.

### Áp migration cho Staging/Production

Ngoài Development/Testing, API **không** tự migrate. Khi khởi động, nếu CSDL còn migration chưa áp, API ghi log `Critical` liệt kê các migration còn thiếu và thoát với mã 1 (không chạy tiếp tới bước tạo admin). Áp migration trước mỗi lần triển khai bằng một trong hai cách:

1. Trực tiếp bằng `dotnet ef` (máy có .NET SDK và truy cập được CSDL đích):

   ```bash
   cd backend
   ConnectionStrings__Default="Host=<host>;Port=5432;Database=tdmu_advisor;Username=<user>;Password=<mật khẩu>" \
   Auth__JwtKey="<chuỗi tối thiểu 32 ký tự>" \
   dotnet ef database update --project src/Tdmu.Advisor.Api --startup-project src/Tdmu.Advisor.Api
   ```

   Hoặc truyền thẳng chuỗi kết nối: thêm `--connection "Host=...;Password=..."` (vẫn cần `Auth__JwtKey` vì Program kiểm tra khi build host).

2. Migration bundle (file chạy độc lập, hợp cho CI/CD, máy đích không cần SDK):

   ```bash
   cd backend
   ConnectionStrings__Default="Host=localhost;Database=x;Username=x;Password=x" Auth__JwtKey="<chuỗi tối thiểu 32 ký tự>" \
   dotnet ef migrations bundle --project src/Tdmu.Advisor.Api --startup-project src/Tdmu.Advisor.Api -o efbundle --self-contained -r linux-x64
   # trên máy triển khai (bundle dựng host của API nên cũng cần Auth__JwtKey):
   Auth__JwtKey="<chuỗi tối thiểu 32 ký tự>" ./efbundle --connection "Host=<host>;Port=5432;Database=tdmu_advisor;Username=<user>;Password=<mật khẩu>"
   ```

Bundle và `database update` chỉ áp các migration còn thiếu, chạy lại nhiều lần vẫn an toàn. Lưu ý migration `HocPhiBusinessKey` thêm unique `(release_id, nganh_lid, nam, don_vi)` cho `hoc_phi`: nếu CSDL cũ đã có học phí trùng khóa này thì phải xóa bản trùng trước khi áp.

## 7. Tham quan nhanh luồng xuất bản (qua Swagger)

1. `POST /api/auth/login` với email/mật khẩu admin, lấy `accessToken`, bấm **Authorize** và dán vào. (Sai mật khẩu 5 lần sẽ bị khóa 15 phút.)
2. CRUD tri thức ở `/api/admin/kb/{slug}`: thêm phương thức xét tuyển, ngành, điểm chuẩn (`mua-tuyen-sinh`, `phuong-thuc`, `to-hop`, `nganh`, `nganh-to-hop`, `chi-tieu`, `diem-chuan`, `hoc-phi`, `moc-thoi-gian`, ...). Có thể nhập hàng loạt qua `/api/admin/import/{kind}/template|preview|commit`.
3. `GET /api/admin/releases/diff` để xem khác biệt giữa bản nháp và release đang hoạt động.
4. `POST /api/admin/releases` (kèm ghi chú) để xuất bản bản nháp thành release mới và kích hoạt nó (chỉ một release hoạt động tại một thời điểm); `POST /api/admin/releases/{id}/activate` để chuyển lại sang một release cũ.
5. `GET /api/public/nganh` (không cần token) giờ thấy dữ liệu vừa xuất bản; `GET /api/public/nganh/{maNganh}` xem chi tiết.
6. Sửa bản nháp tiếp, hoặc `POST /api/admin/releases/{id}/restore-to-draft` để khôi phục một release cũ về bản nháp.

Toàn bộ `/api/admin/**` yêu cầu role `Admin`; `/api/public/**` giới hạn tốc độ theo IP (`RateLimit:PublicPermitsPerMinute`, mặc định 60/phút).

## 8. Cấu hình Gemini

### API key

Key Gemini đọc từ `Gemini:ApiKey`, **không bao giờ commit**. Khai báo bằng user-secrets:

```bash
cd backend/src/Tdmu.Advisor.Api
dotnet user-secrets set "Gemini:ApiKey" "<key từ Google AI Studio>"
```

hoặc biến môi trường `Gemini__ApiKey=<key>`. Thiếu key thì API vẫn chạy: chat trả câu "Hệ thống đang bận…" và tài liệu tải lên bị đánh dấu thất bại với lỗi "Chưa cấu hình Gemini API key".
Thiếu key thì FAQ nháp cũng không bao giờ được embed, nên xuất bản bị chặn (409) cho tới khi cấu hình key.

### Các khóa cấu hình

| Khóa | Mặc định | Ý nghĩa |
| --- | --- | --- |
| `Gemini:ApiKey` | (rỗng) | API key Gemini. |
| `Gemini:StreamIdleTimeoutSeconds` | `30` | Thời gian tối đa chờ dữ liệu mới khi đọc luồng stream; quá hạn coi như Gemini lỗi. |
| `Gemini:RequestTimeoutSeconds` | `60` | Hạn chờ mỗi request tới Gemini (với stream: tới lúc nhận header). |
| `Gemini:PdfTimeoutSeconds` | `180` | Hạn chờ riêng khi chuyển PDF dạng ảnh sang Markdown (PDF dạng ảnh lớn hơn 14 MB bị từ chối). |
| `Agent:ChatModel` | `gemini-3.5-flash` | Model dùng cho chat (và chuyển PDF sang Markdown). |
| `Agent:FallbackChatModel` | `gemini-3-flash-preview` | Model dự phòng: một vòng chat lỗi 429/503/mạng trước khi có chữ thì thử lại một lần sau ~800 ms trên model chính, lỗi tiếp thì chuyển phần còn lại của lượt sang model này và thử thêm một lần, sau đó mới trả "Hệ thống đang bận…". Để rỗng để tắt dự phòng. (Xử lý tài liệu và embed cũng tự thử lại một lần khi gặp 429/503.) |
| `Agent:MaxToolRounds` | `5` | Số vòng gọi model tối đa trong một câu hỏi (mỗi vòng có thể gọi tool). |
| `Embedding:Model` | `gemini-embedding-001` | Model embedding cho tài liệu, FAQ và câu hỏi. |
| `Embedding:Dimensions` | `768` | Số chiều vector; phải khớp cột `vector(768)` trong CSDL. |
| `Rag:FaqThreshold` | `0.85` | Độ tương đồng cosine tối thiểu để trả thẳng câu trả lời FAQ. |
| `Rag:TopK` | `5` | Số đoạn tài liệu tối đa đưa cho model. |
| `Ingestion:Enabled` | `true` | Bật worker nền xử lý tài liệu tải lên và embed FAQ. |
| `Ingestion:PollSeconds` | `5` | Chu kỳ quét việc của worker. |
| `Storage:Root` | `data/uploads` | Thư mục lưu file tải lên (tương đối với thư mục nội dung của API). |
| `RateLimit:ChatPermitsPerMinute` | `20` | Giới hạn `/api/chat` theo IP mỗi phút. |

Giá trị trong bảng được đặt bằng user-secrets hoặc biến môi trường như mọi khóa khác (ví dụ `Agent__ChatModel=gemini-3-flash-preview`). Model chat phải nằm trong danh sách `GET https://generativelanguage.googleapis.com/v1beta/models` của key và hỗ trợ `generateContent`.

### Smoke test với Gemini thật

`GeminiSmokeTests` (trait `Category=GeminiLive`) dựng API với `GeminiRestClient` thật: tạo và xuất bản ngành CNTT (điểm chuẩn 21,5, phương thức thi THPT), tải lên một tài liệu `.md` về ký túc xá, chạy `IngestionService` thật, rồi hỏi hai câu qua `/api/chat` và kiểm tra tool `tra_diem_chuan`, `tim_kiem_tri_thuc` và nguồn trích dẫn. Cần Docker và key:

```bash
GEMINI_API_KEY=<key> dotnet test backend --filter Category=GeminiLive
# Model mặc định quá tải hoặc không dùng được với key: ghi đè model chat cho riêng lần chạy này
GEMINI_API_KEY=<key> GEMINI_CHAT_MODEL=gemini-3-flash-preview dotnet test backend --filter Category=GeminiLive
```

Thiếu `GEMINI_API_KEY` thì test được báo Skipped. Test tự hỏi lại (tối đa 5 lần, cách nhau 20 giây) khi Gemini trả 429 hoặc 503, nên một lần chạy có thể mất vài phút và tốn khoảng 10 lượt gọi API.
