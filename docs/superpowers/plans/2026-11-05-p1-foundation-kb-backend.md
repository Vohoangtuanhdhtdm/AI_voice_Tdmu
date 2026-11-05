# Kế hoạch 1: Nền tảng + KB CMS backend

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Dựng API .NET 8 + PostgreSQL. Cán bộ đăng nhập để thêm/sửa/xóa cơ sở tri thức tuyển sinh trên bản nháp, xem danh sách thay đổi, rồi xuất bản hoặc khôi phục release, có audit log và nhập dữ liệu từ CSV/Excel. Có thêm API công khai chỉ đọc release đang hoạt động, để kế hoạch 2–5 dùng.

**Architecture:** Modular monolith ASP.NET Core 8 (Minimal API) chia theo `Features/*`, dùng EF Core 8 + Npgsql + pgvector.
- **Release:** mọi bảng tri thức có `release_id` (`NULL` là nháp) và `logical_id`. `logical_id` giữ nguyên qua các release, dùng để so sánh thay đổi. Khi xuất bản, các dòng được sao chép mà không phải ánh xạ lại khóa.
- **Tham chiếu chéo giữa bảng tri thức** dùng `logical_id` của bản ghi đích, ví dụ `nganh_lid`. Ràng buộc tham chiếu được kiểm tra ở tầng ứng dụng.

**Tech Stack:**
- .NET 8 (`net8.0`), ASP.NET Core Minimal API
- EF Core 8: `Npgsql.EntityFrameworkCore.PostgreSQL` 8.0.x, `Pgvector.EntityFrameworkCore`, `EFCore.NamingConventions` 8.0.x
- ASP.NET Identity + JWT Bearer
- `ClosedXML` (xlsx), `CsvHelper` (csv), Swashbuckle
- Test: xUnit, `Microsoft.AspNetCore.Mvc.Testing`, `Testcontainers.PostgreSql`, `Respawn`

**Spec:** `docs/superpowers/specs/2026-11-05-tdmu-voice-advisor-design.md`. Kế hoạch này cài đặt các mục 3.1, 3.2, 4.1, 4.2, 4.3 (phần nhập CSV/Excel), 7 (phần API admin), 10 (auth, bí mật) và 11 (phần backend).

## Điều kiện tiên quyết
- Đã cài .NET SDK 8.0.x và Docker; Testcontainers cần Docker đang chạy.
- Làm việc trên nhánh tính năng, mỗi task một commit.

## Global Constraints
- Target `net8.0`. Gói Microsoft và EF Core dùng phiên bản `8.0.*`.
- PostgreSQL image `pgvector/pgvector:pg16`, bật các extension `vector`, `unaccent`, `pg_trgm`.
- Tên bảng và cột là snake_case tiếng Việt không dấu, đúng như spec mục 4.2, ví dụ `diem_chuan`, `nganh_lid`. Dùng `UseSnakeCaseNamingConvention()`. Tên class C# là PascalCase không dấu: `Nganh`, `DiemChuan`.
- Mọi bản ghi tri thức có `nguon_ten`, `nguon_url` (nullable).
- `embedding` là `vector(768)`.
- Chỉ một release có `is_active = true`. Chỉ một `mua_tuyen_sinh` có `la_hien_tai = true` trong cùng một scope.
- JWT access token sống 60 phút. Khóa tài khoản sau **5** lần đăng nhập sai, khóa 15 phút.
- Bí mật (`Gemini:ApiKey`, `Auth:JwtKey`, chuỗi kết nối, mật khẩu admin seed) chỉ lấy từ biến môi trường hoặc user-secrets. Không commit. `appsettings.json` chỉ chứa giá trị rỗng.
- Toàn bộ `/api/admin/**` yêu cầu role `Admin`. `/api/public/**` và `/health` không cần auth.
- Thông báo lỗi trả cho admin bằng tiếng Việt, dạng `ProblemDetails`.
- **Thay đổi so với spec, cần cập nhật spec trong Task 3:** điểm ĐGNL có thể lên tới 1200, nên `diem_chuan.diem` là `numeric(7,2)` thay vì `numeric(5,2)`, và bảng `phuong_thuc` có thêm cột `thang_diem numeric(7,2)` (mặc định 30).

## Review Focus
1. **CSV xuất từ Excel tiếng Việt:** dùng dấu `;` làm phân cách, số thập phân có dấu phẩy (`21,5`), có BOM UTF-8. Kỳ vọng: nhập đúng như CSV dấu `,`. Test ở Task 9.
2. **Bấm "Xuất bản" hai lần gần như cùng lúc:** kỳ vọng chỉ tạo đúng một release mới và vẫn chỉ một release active. Test ở Task 7.
3. **Xuất bản khi không có thay đổi:** kỳ vọng trả 409 "Không có thay đổi để xuất bản", không tạo release rỗng. Test ở Task 7.
4. **Xóa ngành còn được tham chiếu** bởi điểm chuẩn hoặc tổ hợp trong bản nháp: kỳ vọng trả 409, có liệt kê chỗ tham chiếu, không xóa lan. Test ở Task 6.
5. **Điểm chuẩn vượt thang điểm của phương thức,** ví dụ nhập 850 cho "thi THPT" (thang 30), hoặc 25 cho ĐGNL (thang 1200, hợp lệ): kỳ vọng kiểm tra theo `thang_diem` của phương thức, không theo một ngưỡng cố định. Test ở Task 6 và Task 9.

---

## Cấu trúc file

```
backend/
  global.json                                  SDK 8.0.x
  Tdmu.Advisor.sln
  src/Tdmu.Advisor.Api/
    Program.cs                                 đăng ký service, map endpoint, public partial class Program
    appsettings.json / appsettings.Development.json
    Infrastructure/
      AdvisorDbContext.cs                      DbSet + cấu hình model, kế thừa IdentityDbContext
      Migrations/
      Auth/AuthOptions.cs · JwtTokenService.cs · AuthEndpoints.cs · AdminSeeder.cs
      Auditing/ICurrentUser.cs · AuditInterceptor.cs
      Http/ProblemResults.cs                   helper tạo 400/404/409 ProblemDetails tiếng Việt
    Features/KnowledgeBase/
      Entities/*.cs                            KbEntity + 10 thực thể tri thức + Release + AuditLog
      KbModelConfiguration.cs                  index, unique, kiểu cột
      KbScope.cs · QueryableScopeExtensions.cs
      Crud/KbCrudEndpoints.cs · KbEntityDefinitions.cs · KbValidators.cs · KbReferenceChecker.cs
      Releases/ReleaseService.cs · ReleaseDiff.cs · ReleaseEndpoints.cs
      Query/IKnowledgeQuery.cs · KnowledgeQuery.cs · PublicEndpoints.cs
      Import/ImportService.cs · ImportEndpoints.cs · ImportModels.cs · NumberParser.cs · ToHopReading.cs
      Seed/DevSampleSeeder.cs                  dữ liệu MẪU cho môi trường Development
  tests/Tdmu.Advisor.Tests/
    Infrastructure/ApiFactory.cs · ApiCollection.cs · AuthHelper.cs
    *Tests.cs                                  theo từng task
docker-compose.yml                             service postgres (dev)
.github/workflows/ci.yml
```

---

### Task 1: Khung solution, health check, docker-compose, CI

**Files:**
- Create: `backend/global.json`, `backend/Tdmu.Advisor.sln`, `backend/src/Tdmu.Advisor.Api/{Tdmu.Advisor.Api.csproj,Program.cs,appsettings.json,appsettings.Development.json}`
- Create: `backend/tests/Tdmu.Advisor.Tests/{Tdmu.Advisor.Tests.csproj,Infrastructure/ApiFactory.cs,Infrastructure/ApiCollection.cs,HealthTests.cs}`
- Create: `docker-compose.yml`, `.github/workflows/ci.yml`

**Interfaces:**
- Produces:
  - `ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime`:
    - khởi động `PostgreSqlContainer` (image `pgvector/pgvector:pg16`) và ghi đè `ConnectionStrings:Default`;
    - `Task ResetDatabaseAsync()` (Respawn; bỏ qua `__EFMigrationsHistory` và các bảng `AspNet*`).
  - `[Collection("api")]`: collection fixture dùng chung một `ApiFactory`.
  - `GET /health` → `200 {"status":"ok"}`.

- [ ] **Step 1: Tạo solution và project**

```bash
cd backend && dotnet new globaljson --sdk-version 8.0.100 --roll-forward latestFeature
dotnet new sln -n Tdmu.Advisor
dotnet new web -n Tdmu.Advisor.Api -o src/Tdmu.Advisor.Api -f net8.0
dotnet new xunit -n Tdmu.Advisor.Tests -o tests/Tdmu.Advisor.Tests -f net8.0
dotnet sln add src/Tdmu.Advisor.Api tests/Tdmu.Advisor.Tests
dotnet add tests/Tdmu.Advisor.Tests reference src/Tdmu.Advisor.Api
```

Thêm các gói trong Tech Stack vào đúng project: gói test vào project test, còn lại vào Api. Thêm `public partial class Program {}` vào cuối `Program.cs`.

- [ ] **Step 2: Viết test thất bại `HealthTests.Health_returns_ok`**

```csharp
[Collection("api")]
public class HealthTests(ApiFactory f)
{
    [Fact]
    public async Task Health_returns_ok()
    {
        var res = await f.CreateClient().GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        Assert.Equal("ok", (await res.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("status").GetString());
    }
}
```

- [ ] **Step 3: Chạy test, kỳ vọng FAIL (404)**

Run: `dotnet test backend --filter HealthTests`

- [ ] **Step 4: Map `GET /health` trong `Program.cs`; viết `ApiFactory` và `ApiCollection`**

- [ ] **Step 5: Chạy test, kỳ vọng PASS**

- [ ] **Step 6: Viết `docker-compose.yml` và CI**
- `docker-compose.yml`: service `postgres` (image `pgvector/pgvector:pg16`, cổng `5432`, volume `pgdata`, biến `POSTGRES_PASSWORD` lấy từ `.env`).
- `.github/workflows/ci.yml`: chạy trên `push` và `pull_request`, `ubuntu-latest`, `actions/setup-dotnet` với `8.0.x`, rồi chạy `dotnet test backend --configuration Release`.

- [ ] **Step 7: Commit** — `feat(backend): scaffold .NET 8 API with health check, test fixture, CI`

---

### Task 2: DbContext, migration đầu tiên, extension PostgreSQL

**Files:**
- Create: `Infrastructure/AdvisorDbContext.cs`, `Infrastructure/Migrations/*` (sinh tự động)
- Modify: `Program.cs` (đăng ký DbContext; tự chạy `Database.MigrateAsync()` khi khởi động ở môi trường Development hoặc Testing)
- Test: `tests/.../DatabaseTests.cs`

**Interfaces:**
- Produces: `AdvisorDbContext : IdentityDbContext<IdentityUser>`, cấu hình bằng `UseNpgsql(cs, o => o.UseVector()).UseSnakeCaseNamingConvention()`; `modelBuilder.HasPostgresExtension("vector"/"unaccent"/"pg_trgm")`.

- [ ] **Step 1: Viết test thất bại `DatabaseTests.Required_extensions_are_installed`**

Test chạy `SELECT extname FROM pg_extension` qua DbContext, rồi kiểm tra kết quả chứa đủ `vector`, `unaccent`, `pg_trgm`.

- [ ] **Step 2: Chạy test, kỳ vọng FAIL**
- [ ] **Step 3: Cài đặt DbContext, sau đó tạo migration**

```bash
dotnet ef migrations add Init -p src/Tdmu.Advisor.Api -o Infrastructure/Migrations
```

- [ ] **Step 4: Chạy test, kỳ vọng PASS**
- [ ] **Step 5: Commit** — `feat(backend): add DbContext with pgvector, unaccent, pg_trgm`

---

### Task 3: Thực thể tri thức, release, audit log

**Files:**
- Create: `Features/KnowledgeBase/Entities/{KbEntity,MuaTuyenSinh,Nganh,PhuongThuc,ToHop,NganhToHop,DiemChuan,ChiTieu,HocPhi,MocThoiGian,Faq,TaiLieu,TaiLieuChunk,Release,AuditLog}.cs`
- Create: `Features/KnowledgeBase/KbModelConfiguration.cs`, migration `KnowledgeBase`
- Modify: `docs/superpowers/specs/2026-11-05-tdmu-voice-advisor-design.md` mục 4.2. Ghi lại thay đổi `numeric(7,2)` + `thang_diem`, và quy ước tham chiếu bằng `*_lid`.
- Test: `tests/.../KbSchemaTests.cs`

**Interfaces:**
- Produces:
  - **`abstract class KbEntity`**:
    - `Guid Id`, `Guid LogicalId`, `Guid? ReleaseId`, `string? NguonTen`, `string? NguonUrl`, `DateTimeOffset CapNhatLuc`;
    - `abstract object ContentKey()`: trả về một `record` chứa mọi trường nội dung, gồm cả các `*Lid`, nhưng không gồm `Id`, `ReleaseId`, `CapNhatLuc`. Hai bản ghi có `ContentKey()` bằng nhau thì được coi là không thay đổi;
    - `abstract string Label()`: chuỗi hiển thị trong danh sách thay đổi, ví dụ `"Điểm chuẩn CNTT 2026 – thi THPT"`.
  - **Các thực thể:** trường theo spec mục 4.2. Tham chiếu dùng `Guid NganhLid`, `Guid PhuongThucLid`, `Guid? PhuongThucLid`, `Guid ToHopLid`, `Guid? NganhLid` (HocPhi), `Guid TaiLieuLid`.
    - `PhuongThuc.ThangDiem` (decimal, mặc định 30).
    - `HocPhi.DonVi` là enum `DonViHocPhi { TinChi, Nam, HocKy }`, lưu dạng chuỗi.
    - `TaiLieu.TrangThai` là enum `TrangThaiTaiLieu { ChoXuLy, DangXuLy, Xong, ThatBai }`, lưu dạng chuỗi.
  - **`Release`**: `Guid Id, int SoHieu, string GhiChu, string TaoBoi, DateTimeOffset TaoLuc, bool IsActive`.
  - **`AuditLog`**: `long Id, string Entity, Guid EntityLogicalId, string Action, string? BeforeJson, string? AfterJson, string UserId, DateTimeOffset At`. `BeforeJson` và `AfterJson` có kiểu `jsonb`.

**Ràng buộc DB** (unique dùng `NULLS NOT DISTINCT`, nên bản nháp với `release_id NULL` cũng bị ràng buộc; viết bằng `HasIndex(...).IsUnique().AreNullsDistinct(false)`):
- `nganh(release_id, ma_nganh)`, `phuong_thuc(release_id, ma)`, `to_hop(release_id, ma)`
- `nganh_to_hop(release_id, nganh_lid, nam, to_hop_lid)`
- `diem_chuan(release_id, nganh_lid, nam, phuong_thuc_lid)`
- `chi_tieu(release_id, nganh_lid, nam, phuong_thuc_lid)`
- `(release_id, logical_id)` unique trên mọi bảng tri thức
- `release(is_active)` unique có điều kiện `WHERE is_active`
- `mua_tuyen_sinh(release_id)` unique có điều kiện `WHERE la_hien_tai`

**Index:**
- HNSW `vector_cosine_ops` trên `faq.embedding` và `tai_lieu_chunk.embedding`
- GIN trên `tai_lieu_chunk.tsv`
- GIN `gin_trgm_ops` trên `nganh.ten`

**Kiểu cột:** `diem_chuan.diem` là `numeric(7,2)`; `hoc_phi.muc` là `numeric(14,2)`.

- [ ] **Step 1: Viết test thất bại**
  - `Duplicate_diem_chuan_in_draft_is_rejected`: lưu hai `DiemChuan` có cùng `(null, nganhLid, 2026, ptLid)` → `DbUpdateException` với `PostgresException.SqlState == "23505"`.
  - `Same_diem_chuan_in_draft_and_release_is_allowed`: một bản ghi có `ReleaseId = null`, một bản ghi có `ReleaseId = r.Id` → lưu thành công.
  - `Only_one_active_release`: hai `Release` cùng có `IsActive = true` → 23505.
- [ ] **Step 2: Chạy test, kỳ vọng FAIL (chưa có bảng)**
- [ ] **Step 3: Cài đặt các thực thể và cấu hình model; tạo migration `KnowledgeBase`**
- [ ] **Step 4: Chạy test, kỳ vọng PASS**
- [ ] **Step 5: Cập nhật spec mục 4.2 như phần Files; commit** — `feat(kb): add knowledge-base entities, release and audit schema`

---

### Task 4: Đăng nhập admin (Identity + JWT)

**Files:**
- Create: `Infrastructure/Auth/{AuthOptions,JwtTokenService,AuthEndpoints,AdminSeeder}.cs`, `Infrastructure/Http/ProblemResults.cs`
- Create: `tests/.../Infrastructure/AuthHelper.cs`, `tests/.../AuthTests.cs`

**Interfaces:**
- Produces:
  - `AuthOptions { JwtKey, Issuer = "tdmu-advisor", Audience = "tdmu-advisor-admin", AccessTokenMinutes = 60, SeedAdminEmail, SeedAdminPassword }`, bind từ section `Auth`.
  - `POST /api/auth/login { email, password }` → `200 { accessToken, expiresAt }` | `401` | `423` (bị khóa, ProblemDetails "Tài khoản tạm khóa do đăng nhập sai nhiều lần").
  - Policy mặc định cho group `/api/admin`: `RequireRole("Admin")`.
  - `AdminSeeder`: lúc khởi động, nếu chưa có user `SeedAdminEmail` thì tạo user đó với role `Admin`.
  - Test helper: `Task<HttpClient> AuthHelper.AdminClientAsync(ApiFactory f)`.
  - Test chạy với `ASPNETCORE_ENVIRONMENT=Testing`; `ApiFactory` cấp `Auth:*` qua `UseSetting`.

- [ ] **Step 1: Viết test thất bại**
  - `Login_with_valid_credentials_returns_token`: 200, `accessToken` không rỗng, `expiresAt` cách hiện tại khoảng 60 phút (±1 phút).
  - `Admin_endpoint_without_token_returns_401`: gọi `GET /api/admin/ping` → 401.
  - `Five_wrong_passwords_lock_account`: 5 lần sai → lần thứ 6, dù đúng mật khẩu, vẫn trả 423.
- [ ] **Step 2: Chạy test, kỳ vọng FAIL**
- [ ] **Step 3: Cài đặt.** Cấu hình `IdentityOptions.Lockout`: `MaxFailedAccessAttempts = 5`, `DefaultLockoutTimeSpan = 15 phút`. Map `GET /api/admin/ping` → 200 (dùng cho test và kiểm tra token).
- [ ] **Step 4: Chạy test, kỳ vọng PASS**
- [ ] **Step 5: Commit** — `feat(auth): admin login with JWT and lockout`

---

### Task 5: Audit log tự động cho bản nháp

**Files:**
- Create: `Infrastructure/Auditing/{ICurrentUser,AuditInterceptor}.cs`
- Modify: `Program.cs` (đăng ký interceptor; `HttpCurrentUser` đọc `ClaimTypes.NameIdentifier`, nếu không có thì dùng `"system"`)
- Test: `tests/.../AuditTests.cs`

**Interfaces:**
- Produces:
  - `AuditInterceptor : SaveChangesInterceptor`. Với mỗi `KbEntity` có `ReleaseId == null` ở trạng thái Added/Modified/Deleted, thêm một dòng `AuditLog`:
    - `Action` là `"Created"`, `"Updated"` hoặc `"Deleted"`;
    - JSON before/after dùng `System.Text.Json`, đã bỏ `Id`.
  - Dòng `AuditLog` được ghi trong cùng lần `SaveChanges`.
  - Các thay đổi trên bản ghi đã thuộc release (`ReleaseId != null`) **không** sinh audit theo từng dòng; ReleaseService ghi audit riêng.

- [ ] **Step 1: Viết test thất bại `Create_update_delete_draft_entity_writes_audit_rows`**

Thêm rồi sửa rồi xóa một `Nganh` nháp qua DbContext (giả lập user `"u1"`) → có đúng 3 dòng audit:
- thứ tự Action là `Created`, `Updated`, `Deleted`;
- dòng `Updated` có `BeforeJson` chứa tên cũ, `AfterJson` chứa tên mới;
- cả 3 dòng có `UserId == "u1"`.

- [ ] **Step 2: Chạy test, kỳ vọng FAIL**
- [ ] **Step 3: Cài đặt**
- [ ] **Step 4: Chạy test, kỳ vọng PASS**
- [ ] **Step 5: Commit** — `feat(kb): audit draft changes via SaveChanges interceptor`

---

### Task 6: API CRUD bản nháp cho 10 loại tri thức

**Files:**
- Create: `Features/KnowledgeBase/Crud/{KbCrudEndpoints,KbEntityDefinitions,KbValidators,KbReferenceChecker}.cs`
- Create: `Features/KnowledgeBase/Import/ToHopReading.cs` (dùng chung với Task 9)
- Test: `tests/.../KbCrudTests.cs`, `tests/.../ToHopReadingTests.cs`

**Interfaces:**
- Produces:
  - **Route** `/api/admin/kb/{slug}`, với slug là `mua-tuyen-sinh`, `nganh`, `phuong-thuc`, `to-hop`, `nganh-to-hop`, `diem-chuan`, `chi-tieu`, `hoc-phi`, `moc-thoi-gian`, `faq`:

    | Lệnh | Trả về |
    |---|---|
    | `GET ?nam=` | danh sách bản nháp |
    | `GET /{logicalId}` | một bản ghi |
    | `POST` | 201 + body, có `logicalId` mới |
    | `PUT /{logicalId}` | 200 |
    | `DELETE /{logicalId}` | 204 |

  - **Đăng ký và kiểu dữ liệu:**
    - `KbEntityDefinitions`: mỗi slug có một `KbEntityDefinition<TEntity, TDto>`, gồm map hai chiều Entity ⇄ DTO, validator và reference checker. Endpoint generic: `MapKbCrud<TEntity, TDto>(RouteGroupBuilder, string slug)`.
    - DTO là `record` có `Guid? LogicalId` và các trường nội dung, tham chiếu bằng `*Lid`.
  - **`static class ToHopReading`**: `string Generate(string ma)` đọc từng chữ số. Ví dụ `"A00"` → `"A không không"`, `"D14"` → `"D một bốn"`.
  - **Lỗi trả về:**

    | Trường hợp | Mã | Thông báo |
    |---|---|---|
    | Dữ liệu không hợp lệ | 400 `ValidationProblem` | lỗi theo trường, tiếng Việt |
    | Trùng khóa nghiệp vụ (23505) | 409 | "Bản ghi đã tồn tại" |
    | Xóa bản ghi còn được tham chiếu | 409 | `ProblemDetails` có `extensions.thamChieu: string[]` (danh sách `Label()`) |
    | `logicalId` không có trong bản nháp | 404 | |

- **Quy tắc validate** (giá trị cố định):
  - `nam` nằm trong 2000–2100.
  - `diem` phải ≥ 0 và ≤ `thang_diem` của phương thức tham chiếu.
  - `so_luong` > 0; `muc` > 0.
  - `to_hop.ma` khớp regex `^[A-Z][0-9]{2}$`. Nếu `cach_doc` rỗng thì tự sinh bằng `ToHopReading.Generate`.
  - Mọi `*Lid` phải trỏ tới một bản ghi có trong bản nháp.
  - `ten`, `cau_hoi`, `tra_loi` không được rỗng.
  - Đặt một `mua_tuyen_sinh` là `la_hien_tai = true` thì tự bỏ cờ này ở các mùa khác trong bản nháp.

- [ ] **Step 1: Viết test thất bại**
  - `ToHopReadingTests`: `Generate("A00") == "A không không"`, `Generate("D14") == "D một bốn"`.
  - `Create_and_get_nganh`: POST → 201; GET theo `logicalId` trả về đúng `ten` và `tenGoiKhac`.
  - `Diem_over_scale_is_rejected`: phương thức `thi-thpt` có `thangDiem` 30, gửi `diem` = 850 → 400, lỗi ở trường `diem`.
  - `Dgnl_score_within_scale_is_accepted`: phương thức `dgnl` có `thangDiem` 1200, gửi `diem` = 850 → 201.
  - `Duplicate_diem_chuan_returns_409`
  - `Delete_referenced_nganh_returns_409_with_references`: `thamChieu` chứa label của điểm chuẩn tham chiếu tới ngành đó.
  - `Setting_current_season_unsets_others`
  - `Crud_requires_admin`: gọi không có token → 401.
- [ ] **Step 2: Chạy test, kỳ vọng FAIL**
- [ ] **Step 3: Cài đặt** endpoint generic, 10 định nghĩa, validator, reference checker. Bắt `PostgresException` mã `23505` để trả 409.
- [ ] **Step 4: Chạy test, kỳ vọng PASS**
- [ ] **Step 5: Commit** — `feat(kb): draft CRUD API for knowledge-base entities`

---

### Task 7: Release: so sánh thay đổi, xuất bản, kích hoạt, khôi phục về bản nháp

**Files:**
- Create: `Features/KnowledgeBase/Releases/{ReleaseService,ReleaseDiff,ReleaseEndpoints}.cs`
- Test: `tests/.../ReleaseTests.cs`

**Interfaces:**
- Consumes: `KbEntity.ContentKey()`, `Label()` (Task 3); thứ tự các loại thực thể trong `KbEntityDefinitions` (Task 6), cộng thêm `TaiLieu` và `TaiLieuChunk`.
- Produces:
  - `enum DiffKind { Added, Modified, Removed }`
  - `record DiffItem(string Entity, Guid LogicalId, DiffKind Kind, string Label)`
  - `record ReleaseDiff(IReadOnlyList<DiffItem> Items)`
  - `class ReleaseService`:
    - `Task<ReleaseDiff> DiffAsync(CancellationToken ct)`: so sánh bản nháp với release đang active theo `LogicalId` và `ContentKey()`. Nếu chưa có release nào thì mọi bản ghi nháp là `Added`.
    - `Task<Release> PublishAsync(string ghiChu, string userId, CancellationToken ct)`:
      1. mở transaction và gọi `pg_advisory_xact_lock(727001)`;
      2. tính lại diff; nếu rỗng thì ném `NoChangesException`;
      3. tạo release với `SoHieu = max + 1`;
      4. sao chép mọi dòng nháp: `Id` mới, `LogicalId` giữ nguyên, `ReleaseId` mới, giữ nguyên `embedding`;
      5. tắt release cũ, bật release mới;
      6. ghi `AuditLog` với `Action = "Published"`.
    - `Task ActivateAsync(Guid releaseId, string userId, CancellationToken ct)`: ghi audit `"Activated"`.
    - `Task RestoreToDraftAsync(Guid releaseId, string userId, CancellationToken ct)`: xóa toàn bộ bản nháp rồi chép các dòng của release vào bản nháp. Không sinh audit theo từng dòng, chỉ một dòng audit `"RestoredToDraft"`.
  - **Endpoint:**

    | Endpoint | Trả về |
    |---|---|
    | `GET /api/admin/releases` | danh sách release, mới nhất trước |
    | `GET /api/admin/releases/diff` | `ReleaseDiff` |
    | `POST /api/admin/releases { ghiChu }` | 201 release, hoặc 409 "Không có thay đổi để xuất bản" |
    | `POST /api/admin/releases/{id}/activate` | 204 |
    | `POST /api/admin/releases/{id}/restore-to-draft` | 204 |

- [ ] **Step 1: Viết test thất bại**
  - `Diff_with_no_release_lists_all_drafts_as_added`
  - `Diff_detects_modified_and_removed`: xuất bản; sau đó sửa điểm một bản ghi và xóa một ngành không còn tham chiếu → diff gồm đúng một `Modified` và một `Removed`.
  - `Publish_copies_drafts_and_activates`: sau khi xuất bản, số dòng có `ReleaseId == r.Id` bằng số dòng nháp; `LogicalId` giống nhau; chỉ `r.IsActive`.
  - `Publish_without_changes_returns_409` *(Review Focus 3)*
  - `Concurrent_publish_creates_single_release` *(Review Focus 2)*: gọi `Task.WhenAll` hai lần `POST /api/admin/releases` → đúng một 201 và một 409; DB có đúng 1 release active.
  - `Activate_older_release_switches_active`
  - `Restore_to_draft_replaces_draft_content`
- [ ] **Step 2: Chạy test, kỳ vọng FAIL**
- [ ] **Step 3: Cài đặt.** Việc sao chép dùng EF: đọc dòng nháp bằng `AsNoTracking`, đặt `Id`/`ReleaseId` mới, rồi `AddRange`. Dữ liệu tuyển sinh có quy mô vài nghìn dòng nên cách này đủ nhanh.
- [ ] **Step 4: Chạy test, kỳ vọng PASS**
- [ ] **Step 5: Commit** — `feat(kb): release diff, publish, activate, restore`

---

### Task 8: IKnowledgeQuery, API công khai, dữ liệu mẫu cho dev

**Files:**
- Create: `Features/KnowledgeBase/{KbScope,QueryableScopeExtensions}.cs`, `Features/KnowledgeBase/Query/{IKnowledgeQuery,KnowledgeQuery,PublicEndpoints}.cs`, `Features/KnowledgeBase/Seed/DevSampleSeeder.cs`
- Test: `tests/.../KnowledgeQueryTests.cs`

**Interfaces:**
- Produces (kế hoạch 3 sẽ mở rộng interface này cho các tool):
  - `enum KbScope { Published, Draft }`
  - `IQueryable<T> InScope<T>(this IQueryable<T> q, Guid? releaseId) where T : KbEntity`: lọc `ReleaseId == releaseId`, với `null` nghĩa là bản nháp.
  - **`interface IKnowledgeQuery`:**
    - `Task<Guid?> ResolveReleaseAsync(KbScope scope, CancellationToken ct)`: `Draft` trả `null`. `Published` trả id của release active; nếu chưa có thì ném `NoActiveReleaseException`.
    - `Task<int?> CurrentYearAsync(KbScope scope, CancellationToken ct)`
    - `Task<IReadOnlyList<NganhView>> ListNganhAsync(KbScope scope, CancellationToken ct)`
    - `Task<NganhDetailView?> GetNganhAsync(KbScope scope, string maNganh, CancellationToken ct)`
  - **Kiểu trả về:**
    - `record NganhView(string MaNganh, string Ten, string? DonVi, string? MoTaNgan)`
    - `record NganhDetailView(NganhView Nganh, int? Nam, IReadOnlyList<ToHopView> ToHop, IReadOnlyList<DiemChuanView> DiemChuan, IReadOnlyList<ChiTieuView> ChiTieu, IReadOnlyList<HocPhiView> HocPhi)`. Chi tiết lấy theo năm hiện tại.
    - Các view con là record phẳng, có `NguonTen` và `NguonUrl`.
  - **API công khai** (rate limit fixed window 60 request/phút/IP):
    - `GET /api/public/nganh`: chưa có release thì trả `[]`.
    - `GET /api/public/nganh/{maNganh}`: chưa có release hoặc không tìm thấy thì trả 404.
  - **`DevSampleSeeder`**: chỉ chạy khi `Seed:SampleData = true` và môi trường là Development. Ghi vào **bản nháp** 3 ngành, 3 phương thức (thang 30/30/1200), tổ hợp và điểm chuẩn. Mọi bản ghi có `NguonTen = "DỮ LIỆU MẪU – không phải số liệu chính thức"`.

- [ ] **Step 1: Viết test thất bại**
  - `Public_list_is_empty_without_release`
  - `Public_sees_only_published`: tạo ngành A, xuất bản, sau đó thêm ngành B vào bản nháp → API công khai chỉ trả A.
  - `Draft_scope_sees_draft_changes`: `IKnowledgeQuery.ListNganhAsync(KbScope.Draft)` trả cả A và B.
  - `Nganh_detail_returns_current_year_data_with_sources`
- [ ] **Step 2: Chạy test, kỳ vọng FAIL**
- [ ] **Step 3: Cài đặt**
- [ ] **Step 4: Chạy test, kỳ vọng PASS**
- [ ] **Step 5: Commit** — `feat(kb): knowledge query with scope, public read API, dev sample seed`

---

### Task 9: Nhập dữ liệu từ CSV/Excel

**Files:**
- Create: `Features/KnowledgeBase/Import/{ImportService,ImportEndpoints,ImportModels,NumberParser}.cs`
- Test: `tests/.../NumberParserTests.cs`, `tests/.../ImportTests.cs`, `tests/.../TestData/*.csv|*.xlsx` (sinh trong test hoặc commit file nhỏ)

**Interfaces:**
- Consumes: validator của Task 6, dùng để kiểm tra lại từng dòng.
- Produces:
  - **`static class NumberParser`**: `bool TryParseDecimal(string? raw, out decimal value)`. Chấp nhận `"21,5"`, `"21.5"`, `" 21,50 "`. Từ chối `""`, `"abc"`, `"1.234,5"`.
  - **Loại dữ liệu** `kind` và cột tương ứng (header dòng 1, đúng tên, không phân biệt hoa thường; cột có dấu `?` là tùy chọn):

    | `kind` | Cột |
    |---|---|
    | `diem-chuan` | `ma_nganh, nam, ma_phuong_thuc, diem, ghi_chu?, nguon_ten?, nguon_url?` |
    | `chi-tieu` | `ma_nganh, nam, ma_phuong_thuc?, so_luong, nguon_ten?, nguon_url?` |
    | `hoc-phi` | `ma_nganh?, nam, muc, don_vi (tin_chi/nam/hoc_ky), ghi_chu?, nguon_ten?, nguon_url?` |
    | `nganh-to-hop` | `ma_nganh, nam, ma_to_hop` |

  - **Kiểu dữ liệu:**
    - `record ImportRow(int RowNumber, IReadOnlyDictionary<string,string?> Values, IReadOnlyList<string> Errors)`. `RowNumber` tính theo dòng trong file, dòng dữ liệu đầu tiên là 2.
    - `record ImportPreview(string Kind, IReadOnlyList<ImportRow> Rows, int ValidCount, int ErrorCount)`.
  - **Endpoint:**

    | Endpoint | Trả về |
    |---|---|
    | `GET /api/admin/import/{kind}/template` | file `.xlsx` có header và một dòng ví dụ |
    | `POST /api/admin/import/{kind}/preview` | multipart `file`, nhận `.csv`/`.xlsx`, tối đa 5 MB → `ImportPreview` |
    | `POST /api/admin/import/{kind}/commit` | cùng file. Nếu còn `ErrorCount > 0` thì trả 400 kèm preview. Nếu không, **upsert vào bản nháp** theo khóa nghiệp vụ, rồi trả `{ inserted, updated }` |

  - **Đọc CSV:** bỏ BOM UTF-8; tự nhận dấu phân cách `;` hoặc `,` bằng cách đếm trên dòng header.
  - **Lỗi theo dòng** (tiếng Việt): mã ngành, phương thức hoặc tổ hợp không có trong bản nháp; số không hợp lệ; điểm vượt thang của phương thức; thiếu cột bắt buộc. Thiếu cột bắt buộc là lỗi cấp file, trả 400.
  - **Upsert** đi qua DbContext, nên audit log tự ghi (Task 5).

- [ ] **Step 1: Viết test thất bại**
  - `NumberParserTests`, gồm các trường hợp chấp nhận và từ chối nêu ở trên.
  - `Preview_semicolon_csv_with_bom_and_decimal_comma` *(Review Focus 1)*: file `"﻿ma_nganh;nam;ma_phuong_thuc;diem\n7480201;2026;thi-thpt;21,5"` → `ValidCount == 1`, giá trị `diem` đọc ra 21.5.
  - `Preview_reports_row_errors`: dòng 3 có mã ngành không tồn tại, dòng 4 có `diem` 850 với phương thức thang 30 *(Review Focus 5)* → `ErrorCount == 2`; lỗi gắn đúng `RowNumber` 3 và 4.
  - `Commit_upserts_into_draft`: lần đầu inserted 2; nhập lại cùng file với một điểm đã đổi → `updated == 1`, `inserted == 0`.
  - `Commit_with_errors_returns_400`
  - `Xlsx_template_roundtrip`: tải template, điền một dòng bằng ClosedXML, preview → hợp lệ.
- [ ] **Step 2: Chạy test, kỳ vọng FAIL**
- [ ] **Step 3: Cài đặt**
- [ ] **Step 4: Chạy test, kỳ vọng PASS**
- [ ] **Step 5: Commit** — `feat(kb): CSV/Excel import with preview and upsert into draft`

---

### Task 10: OpenAPI, cấu hình, tài liệu chạy local

**Files:**
- Modify: `Program.cs`: thêm Swashbuckle tại `/swagger` (chỉ ở Development) và cấu hình bảo mật Bearer.
- Create: `backend/README.md`: cài SDK, `docker compose up -d postgres`, khai báo user-secrets (`Auth:JwtKey`, `Auth:SeedAdminEmail`, `Auth:SeedAdminPassword`, `ConnectionStrings:Default`), `dotnet run`, `dotnet test`.
- Modify: `.env.example`: thêm `POSTGRES_PASSWORD=`.
- Test: `tests/.../OpenApiTests.cs`

- [ ] **Step 1: Viết test thất bại `OpenApi_document_lists_admin_and_public_routes`**

Test chạy với môi trường Development, `GET /swagger/v1/swagger.json` → JSON có các path `/api/admin/kb/diem-chuan`, `/api/admin/releases`, `/api/public/nganh`.

- [ ] **Step 2: Chạy test, kỳ vọng FAIL**
- [ ] **Step 3: Cài đặt; viết README**
- [ ] **Step 4: Chạy toàn bộ test, kỳ vọng PASS hết**

Run: `dotnet test backend`

- [ ] **Step 5: Commit** — `docs(backend): OpenAPI and local run guide`

---

## Hoàn thành kế hoạch 1 khi
- `dotnet test backend` xanh, cả ở local lẫn trên CI.
- Dùng Swagger làm được trọn luồng: đăng nhập → thêm phương thức, ngành, điểm chuẩn → xem diff → xuất bản → `GET /api/public/nganh` thấy dữ liệu → sửa bản nháp → khôi phục.
- Spec mục 4.2 đã ghi lại hai thay đổi (`numeric(7,2)` + `thang_diem`, tham chiếu `*_lid`).
- Cập nhật trạng thái trong `docs/superpowers/plans/README.md`.
