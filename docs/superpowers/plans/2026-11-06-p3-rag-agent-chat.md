# Kế hoạch 3: Ingestion + RAG + ToolRegistry + kênh chat + Hỏi thử

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** AI Agent trả lời câu hỏi tuyển sinh bằng văn bản qua `POST /api/chat` (SSE). Agent gọi tool tra số liệu, tìm kiếm FAQ và tài liệu (RAG), và trích nguồn. Cán bộ được tải tài liệu lên để hệ thống xử lý, và được "Hỏi thử" ngay trên bản nháp trong admin.

**Architecture:** Thêm ba module vào modular monolith .NET 8:
- `Features/Ingestion`: `BackgroundService` trích văn bản, chia chunk, tạo embedding.
- `Features/Agent`: tool registry, tìm kiếm, vòng lặp chat.
- `Features/Conversations`: lưu hội thoại, tin nhắn, ticket.

Gemini được gọi qua REST (`generativelanguage.googleapis.com/v1beta`), bọc sau `IGeminiClient`, nên mọi test tự động dùng bản giả. Admin UI có thêm hai trang: "Tài liệu" và "Hỏi thử".

**Tech Stack:**
- Backend:
  - .NET 8, EF Core 8 + Npgsql + pgvector (đã có)
  - `UglyToad.PdfPig` (PDF), `DocumentFormat.OpenXml` (DOCX)
  - `System.Net.Http` + `System.Text.Json` (Gemini REST, SSE)
- Frontend: React TS + antd (đã có), `fetch` + `ReadableStream` để đọc SSE

**Spec:** `docs/superpowers/specs/2026-11-05-tdmu-voice-advisor-design.md`, các mục 4.2 (`tai_lieu`, `tai_lieu_chunk`, `faq`), 4.3 (xử lý tài liệu), 5.1–5.4 (tool, RAG, system prompt, kênh chat), 7 (Hỏi thử, tải tài liệu), 8 (hội thoại, ticket, PII), 9 (xử lý lỗi), 10 (bảo mật, chi phí), 13 (cấu hình). Việc tồn đọng: `docs/superpowers/plans/p1-followups.md`, `p2-followups.md`.

## Global Constraints
- **Gemini:**
  - Chỉ gọi qua `IGeminiClient` (Task 1). Key đọc từ `Gemini:ApiKey` (biến môi trường `Gemini__ApiKey`), không commit.
  - Test tự động **không bao giờ** gọi Gemini thật. `ApiFactory` thay `IGeminiClient` bằng `FakeGeminiClient`.
  - Chỉ có một smoke test gọi Gemini thật (Task 9). Test này bị bỏ qua khi thiếu `GEMINI_API_KEY`.
- **Cấu hình mặc định** (spec mục 13): `Agent:ChatModel = "gemini-3.5-flash"`, `Agent:MaxToolRounds = 5`, `Embedding:Model = "gemini-embedding-001"`, `Embedding:Dimensions = 768`, `Rag:FaqThreshold = 0.85`, `Rag:TopK = 5`.
- **Kết quả tool** (spec 5.1) là JSON snake_case, đúng các key `ket_qua`, `du_lieu`, `cach_doc`, `nam_ap_dung`, `nguon` (`[{ ten, url }]`), `huong_dan`.
  - `ket_qua` thuộc `ok | khong_tim_thay | can_hoi_lai | loi_he_thong`.
  - `khong_tim_thay` luôn kèm `huong_dan` chứa cụm "không có trong cơ sở dữ liệu".
  - Mỗi tool có timeout **3 giây**; hết giờ thì trả `loi_he_thong`.
- **Phạm vi đọc dữ liệu:**
  - Tool và tìm kiếm chỉ đọc theo `KbScope`: `Published` là release đang hoạt động, `Draft` là bản nháp.
  - `scope = draft` chỉ được chấp nhận khi có JWT admin; không có thì trả 403.
  - Agent không có đường ghi vào cơ sở tri thức. Thứ duy nhất Agent ghi được là `ticket` và các bảng hội thoại.
- **Lưu tin nhắn:** trước khi lưu `message.noi_dung`, phải che số điện thoại thành `[SĐT]` và email thành `[EMAIL]`. Thông tin liên hệ chỉ lưu nguyên vẹn trong `ticket`.
- **Giới hạn API chat:**
  - Tin nhắn tối đa **1.000 ký tự**; dài hơn thì trả 400.
  - Rate limit policy `chat`: 20 request/phút/IP. Số này cấu hình được qua `RateLimit:ChatPermitsPerMinute`; test đặt cao.
- **Model Gemini 3 và function call:** Gemini 3 trả `thoughtSignature` kèm function call. Vì vậy khi đưa lượt của model vào lịch sử, phải giữ **nguyên văn JSON các part của model** (dùng `JsonObject`), không dựng lại part.
- **Lỗi và ngôn ngữ:**
  - Mọi thông báo lỗi cho người dùng và admin bằng tiếng Việt.
  - Khi Gemini lỗi thì không được để câu trả lời trống. Câu mặc định: `"Hệ thống đang bận, bạn vui lòng thử lại sau ít phút hoặc để lại thông tin để cán bộ tuyển sinh liên hệ."`
- **Test:** backend dùng xUnit + Testcontainers (đã có). Frontend theo Global Constraints của Kế hoạch 2: MSW, tiếng Việt, `apiFetch`.
  - `dotnet test backend`, `pnpm -C frontend typecheck` và `pnpm -C frontend test` phải xanh sau mỗi task chạm tới phần tương ứng.

## Review Focus
1. **Xuất bản khi tài liệu còn đang xử lý, hoặc FAQ chưa có embedding:** nếu cho xuất bản, release sẽ chứa tài liệu không bao giờ được tìm thấy, vì worker chỉ xử lý bản nháp. Kỳ vọng xuất bản trả 409 `"Còn tài liệu hoặc FAQ đang xử lý, vui lòng chờ hoàn tất rồi xuất bản"`. Test ở Task 3.
2. **Câu hỏi có số điện thoại hoặc email của người hỏi:** nội dung lưu trong `message` phải được che. Còn khi người dùng đồng ý chuyển tiếp, `ticket.lien_he` vẫn giữ đầy đủ. Test ở Task 2 và Task 7.
3. **Gemini lỗi giữa chừng** (lỗi 503 ngay sau khi đã stream một phần chữ): stream phải kết thúc bằng sự kiện `error` có câu mặc định, và tin nhắn của trợ lý vẫn được lưu kèm cờ lỗi. Không được treo kết nối hay trả về câu trống. Test ở Task 7 và Task 8.
4. **Tên ngành người dùng gõ không dấu, viết tắt hoặc sai chính tả** ("cong nghe thong tin", "CNTT", "cong nghe thong tinn"): `tim_nganh` vẫn tìm ra ngành, nhờ alias, unaccent và `pg_trgm`. Nếu có nhiều ngành giống nhau thì trả `can_hoi_lai`. Test ở Task 6.
5. **Sửa câu hỏi FAQ sau khi đã có embedding:** embedding cũ phải bị xóa và được tính lại. Nếu không, câu hỏi mới sẽ khớp nhầm câu trả lời cũ. Test ở Task 3 và Task 4.

---

## Cấu trúc file

```
backend/src/Tdmu.Advisor.Api/
  Infrastructure/Gemini/        IGeminiClient.cs · GeminiRestClient.cs · GeminiOptions.cs · GeminiModels.cs · GeminiExceptions.cs
  Infrastructure/Storage/       IFileStore.cs · LocalFileStore.cs
  Features/Conversations/       Entities (Conversation, Message, Ticket) · ConversationStore.cs · PiiMasker.cs
  Features/KnowledgeBase/Documents/  TaiLieuEndpoints.cs              (upload/list/reprocess/delete)
  Features/Ingestion/           IngestionWorker.cs · TextExtractor.cs · Chunker.cs · IngestionService.cs
  Features/Agent/Search/        IKnowledgeSearch.cs · KnowledgeSearch.cs · Rrf.cs
  Features/Agent/Tools/         IAgentTool.cs · ToolResult.cs · ToolContext.cs · ToolRegistry.cs · VietnameseNumberReader.cs · <8 tool>.cs
  Features/Agent/Chat/          ChatOrchestrator.cs · ChatEvents.cs · SystemPrompt.cs · Prompts/system.v1.md · ChatEndpoints.cs
backend/tests/Tdmu.Advisor.Tests/
  Infrastructure/FakeGeminiClient.cs
  … *Tests.cs theo từng task; GeminiSmokeTests.cs (Task 9)
frontend/apps/admin/src/
  api/chat.ts (đọc SSE) · api/documents.ts · documents/DocumentsPage.tsx · playground/PlaygroundPage.tsx
```

---

### Task 1: `IGeminiClient` (REST) và `FakeGeminiClient`

**Files:**
- Create: `Infrastructure/Gemini/{IGeminiClient,GeminiRestClient,GeminiOptions,GeminiModels,GeminiExceptions}.cs`
- Modify: `Program.cs` (đăng ký `AddHttpClient<IGeminiClient, GeminiRestClient>`, bind `Gemini`, `Embedding`, `Agent`, `Rag`), `appsettings.json` (giá trị mặc định ở Global Constraints, `Gemini:ApiKey` để rỗng)
- Create: `tests/.../Infrastructure/FakeGeminiClient.cs`; Modify `ApiFactory.cs` để thay `IGeminiClient` bằng một singleton `FakeGeminiClient`, truy cập qua `f.Gemini`
- Test: `tests/.../GeminiRestClientTests.cs`

**Interfaces:**
- Produces:
  - `enum EmbeddingTask { RetrievalDocument, RetrievalQuery }`
  - `record GeminiRequest(string SystemInstruction, IReadOnlyList<JsonObject> Contents, JsonArray FunctionDeclarations, string? Model = null)`. `Contents` là các phần tử `{ role: "user"|"model", parts: [...] }` theo đúng JSON của Gemini.
  - `record GeminiChunk(IReadOnlyList<JsonObject> Parts, string? FinishReason)`. Mỗi part được giữ nguyên văn, có thể là `{text}` hoặc `{functionCall:{name,args}, thoughtSignature?}`.
  - **`interface IGeminiClient`:**
    - `Task<IReadOnlyList<float[]>> EmbedAsync(IReadOnlyList<string> texts, EmbeddingTask task, CancellationToken ct)`
    - `IAsyncEnumerable<GeminiChunk> StreamGenerateAsync(GeminiRequest req, CancellationToken ct)`
    - `Task<string> PdfToMarkdownAsync(byte[] pdf, CancellationToken ct)`
  - **Exception:** `GeminiNotConfiguredException` (key rỗng) và `GeminiUnavailableException(int? status)` (429, 5xx, lỗi mạng, timeout).
  - **`GeminiRestClient`**, base URL `https://generativelanguage.googleapis.com/v1beta/`, header `x-goog-api-key`:
    - **Embed:** `POST models/{Embedding:Model}:batchEmbedContents`, body `{ requests: [{ model: "models/{m}", content: { parts: [{ text }] }, taskType: "RETRIEVAL_DOCUMENT"|"RETRIEVAL_QUERY", outputDimensionality: 768 }] }`. Đọc kết quả từ `embeddings[i].values`. Nếu nhiều hơn 100 văn bản thì chia lô 100.
    - **Stream:** `POST models/{model}:streamGenerateContent?alt=sse`, body `{ systemInstruction: { parts: [{ text }] }, contents, tools: [{ functionDeclarations }], generationConfig: { temperature: 0.3 } }`. Phản hồi được đọc theo từng dòng `data: {json}`, mỗi dòng sinh một `GeminiChunk` từ `candidates[0].content.parts` và `finishReason`.
    - **PDF:** gọi `generateContent` với part `{ inlineData: { mimeType: "application/pdf", data: base64 } }` cùng prompt `"Chuyển toàn bộ nội dung tài liệu sau sang Markdown tiếng Việt, giữ nguyên tiêu đề, bảng và số liệu. Không thêm bình luận."`.
  - **`FakeGeminiClient`** (dùng cho test):
    - `EmbedAsync` trả vector 768 chiều **tất định**: băm từ các từ của văn bản, sau đó chuẩn hóa. Vì vậy hai văn bản cùng từ sẽ cho cosine bằng 1. Có thể ghi đè qua `EmbedOverride: Func<string, float[]>?`.
    - `StreamGenerateAsync` phát lần lượt các lượt đã soạn sẵn trong `Script: Queue<IReadOnlyList<GeminiChunk>>`, và ghi lại mọi request vào `Requests`.
    - `PdfToMarkdownAsync` trả `PdfMarkdown`.
    - Có `FailNext: Exception?` để giả lập lỗi.

- [ ] **Step 1: Viết test thất bại** (dùng `HttpMessageHandler` giả, không gọi mạng)
  - `Embed_posts_batch_with_task_type_and_dimensions`: kiểm tra URL `.../models/gemini-embedding-001:batchEmbedContents`, header `x-goog-api-key` bằng key cấu hình, body có `taskType == "RETRIEVAL_QUERY"` và `outputDimensionality == 768`. Phản hồi 2 vector thì đọc ra đúng 2 mảng.
  - `Embed_splits_into_batches_of_100`: 150 văn bản tạo ra 2 request.
  - `Stream_parses_sse_text_and_function_call_parts_verbatim`: một phản hồi SSE có 2 dòng `data:`, dòng 1 là `{text:"Xin "}`, dòng 2 là `{functionCall:{name:"tra_diem_chuan",args:{nganh:"CNTT"}}, thoughtSignature:"abc"}` kèm `finishReason:"STOP"`. Kết quả là 2 `GeminiChunk`, và part thứ hai vẫn còn `thoughtSignature == "abc"`.
  - `Status_429_and_503_throw_unavailable`, `Empty_key_throws_not_configured`.
- [ ] **Step 2: Chạy test, kỳ vọng FAIL**
- [ ] **Step 3: Cài đặt** (đọc stream bằng `HttpCompletionOption.ResponseHeadersRead` và `StreamReader.ReadLineAsync`; timeout `HttpClient` là 60 giây)
- [ ] **Step 4: Chạy toàn bộ `dotnet test backend`, kỳ vọng PASS**
- [ ] **Step 5: Commit** — `feat(agent): Gemini REST client with streaming and fake for tests`

---

### Task 2: Hội thoại, tin nhắn, ticket và che PII

**Files:**
- Create: `Features/Conversations/{Conversation,Message,Ticket}.cs`, `ConversationStore.cs`, `PiiMasker.cs`
- Modify: `AdvisorDbContext.cs` (thêm DbSet và cấu hình), migration mới `Conversations`
- Test: `tests/.../PiiMaskerTests.cs`, `tests/.../ConversationStoreTests.cs`

**Interfaces:**
- **Bảng** (spec mục 8, tên snake_case):

  | Bảng | Cột |
  |---|---|
  | `conversation` | `id` (Guid), `kenh_dau_tien` (`"chat"`\|`"live"`), `bat_dau`, `user_agent?`, `release_id?` (release đã phục vụ; null khi Hỏi thử), `la_hoi_thu` (bool), `dong_y_giong_noi` (bool, mặc định false) |
  | `message` | `id`, `conversation_id` (FK, cascade), `vai_tro` (`"user"`\|`"assistant"`), `kenh`, `noi_dung`, `tool_calls jsonb?`, `latency_ms jsonb?`, `danh_gia` (smallint, mặc định 0), `co_loi` (bool), `tao_luc`. Index trên `(conversation_id, tao_luc)` |
  | `ticket` | `id`, `conversation_id?` (FK set null), `ho_ten`, `lien_he`, `ly_do`, `trang_thai` (`"moi"`\|`"dang_xu_ly"`\|`"xong"`, mặc định `"moi"`), `tao_luc` |

- **`static class PiiMasker`:** `string Mask(string text)`.
  - Email: regex chuẩn → `[EMAIL]`.
  - Số điện thoại Việt Nam: `(?<!\d)(\+?84|0)(?:[\s.\-]?\d){8,10}(?!\d)` → `[SĐT]`.
- **`ConversationStore`** (scoped):
  - `Task<Conversation> GetOrCreateAsync(Guid? id, string kenh, bool laHoiThu, Guid? releaseId, string? userAgent, CancellationToken ct)`. Nếu `id` không tồn tại thì tạo mới; không ném lỗi.
  - `Task<IReadOnlyList<Message>> RecentAsync(Guid conversationId, int turns, CancellationToken ct)`: lấy `turns × 2` tin nhắn mới nhất, trả theo thứ tự thời gian tăng dần.
  - `Task<Message> AddAsync(Guid conversationId, string vaiTro, string kenh, string noiDung, JsonNode? toolCalls, JsonNode? latency, bool coLoi, CancellationToken ct)`: luôn gọi `PiiMasker.Mask(noiDung)` trước khi lưu.
  - `Task<Ticket> CreateTicketAsync(Guid? conversationId, string hoTen, string lienHe, string lyDo, CancellationToken ct)`: không che dữ liệu.

- [ ] **Step 1: Viết test thất bại**
  - `PiiMaskerTests` với `[Theory]`:
    - `"gọi em 0912 345 678 nhé"` → `"gọi em [SĐT] nhé"`
    - `"+84912345678"` → `"[SĐT]"`
    - `"mail a.b@tdmu.edu.vn"` → `"mail [EMAIL]"`
    - `"điểm 21,5 năm 2026"` giữ nguyên
    - `"mã ngành 7480201"` giữ nguyên (chỉ 7 chữ số)
  - `ConversationStoreTests`:
    - `Add_masks_pii_but_ticket_keeps_contact` *(Review Focus 2)*
    - `Recent_returns_last_n_turns_in_order`: thêm 12 lượt hỏi/đáp; `RecentAsync(…, 10)` trả 20 tin nhắn, tin đầu là lượt thứ 3.
    - `GetOrCreate_with_unknown_id_creates_new`
- [ ] **Step 2: Chạy test, kỳ vọng FAIL**
- [ ] **Step 3: Cài đặt** (tạo migration theo lệnh trong `backend/README.md`)
- [ ] **Step 4: Chạy toàn bộ `dotnet test backend`, kỳ vọng PASS**
- [ ] **Step 5: Commit** — `feat(conversations): conversation, message and ticket storage with PII masking`

---

### Task 3: API tài liệu, chặn xuất bản khi còn đang xử lý, reset embedding FAQ

**Files:**
- Create: `Infrastructure/Storage/{IFileStore,LocalFileStore}.cs`, `Features/KnowledgeBase/Documents/TaiLieuEndpoints.cs`
- Modify:
  - `Entities/TaiLieu.cs`: bỏ `TrangThai` và `Loi` khỏi `ContentKey` (việc tồn đọng từ P1); thêm `TenFileGoc`, `LoaiNoiDung`, `KichThuoc` vào entity và migration.
  - `Crud/KbEntityDefinitions.cs`: khi `cauHoi` hoặc `cachHoiKhac` của FAQ thay đổi thì đặt `Embedding = null`.
  - `Releases/ReleaseService.cs`: thêm điều kiện chặn xuất bản.
- Test: `tests/.../TaiLieuEndpointsTests.cs`, bổ sung test vào `ReleaseTests.cs` và `KbCrudTests.cs`

**Interfaces:**
- **`IFileStore`:**
  - `Task<string> SaveAsync(Guid logicalId, string ext, Stream content, CancellationToken ct)`: trả đường dẫn tương đối `"{logicalId}{ext}"`.
  - `Task<byte[]> ReadAsync(string path, CancellationToken ct)`; `Task DeleteAsync(string path, CancellationToken ct)`.
  - `LocalFileStore` đặt file dưới `Storage:Root` (mặc định `"data/uploads"`). Test trỏ vào thư mục tạm.
- **Endpoint** (đều nằm trong group `/api/admin`, chỉ thao tác trên bản nháp):
  - `POST /api/admin/tai-lieu`:
    - Nhận multipart gồm `file`, `tieuDe`, `namApDung?`, `nguonTen?`, `nguonUrl?`.
    - Chỉ nhận `.pdf`, `.docx`, `.md`, `.txt`; tối đa **20 MB**. Sai định dạng trả 400 `"Chỉ nhận file PDF, DOCX, MD hoặc TXT"`; quá cỡ trả 413 `"File vượt quá 20 MB"`.
    - Lưu xong trả 201 kèm `TaiLieuDto`; trạng thái ban đầu là `ChoXuLy`.
  - `GET /api/admin/tai-lieu`: danh sách `TaiLieuDto { logicalId, tieuDe, tenFileGoc, namApDung, trangThai, loi, soDoan, capNhatLuc, nguonTen, nguonUrl }`, trong đó `soDoan` là số chunk của bản nháp.
  - `POST /api/admin/tai-lieu/{logicalId}/xu-ly-lai`: chuyển `trangThai` về `ChoXuLy`, `loi` về null, rồi trả 204.
  - `DELETE /api/admin/tai-lieu/{logicalId}`: xóa bản ghi tài liệu nháp và các chunk nháp, trả 204. File vật lý chỉ bị xóa khi không còn release nào tham chiếu tới cùng `duong_dan_file`.
- **Chặn xuất bản:** `ReleaseService.PublishAsync` ném `PendingProcessingException` nếu bản nháp còn `TaiLieu` ở `ChoXuLy` hoặc `DangXuLy`, hoặc còn FAQ có `Embedding == null`. Endpoint trả 409 với title đúng như Review Focus 1.

- [ ] **Step 1: Viết test thất bại**
  - `Upload_pdf_creates_pending_document`; `Upload_exe_returns_400`; `Upload_over_20mb_returns_413`.
  - `Reprocess_resets_status`; `Delete_removes_draft_document_and_chunks`.
  - `Publish_blocked_while_document_pending` *(Review Focus 1)*: kiểm tra đúng mã 409 và title.
  - `Publish_blocked_while_faq_has_no_embedding`.
  - `Editing_faq_question_clears_embedding` *(Review Focus 5)*: FAQ có embedding, PUT với `cauHoi` mới thì embedding thành null; PUT chỉ sửa `traLoi` thì embedding vẫn giữ.
  - `TaiLieu_status_change_is_not_a_content_diff`
- [ ] **Step 2: Chạy test, kỳ vọng FAIL**
- [ ] **Step 3: Cài đặt** (endpoint dùng `.DisableAntiforgery()`; giới hạn 20 MB qua `RequestFormLimits` hoặc kiểm tra `IFormFile.Length`)
- [ ] **Step 4: Chạy toàn bộ `dotnet test backend`, kỳ vọng PASS**
- [ ] **Step 5: Commit** — `feat(kb): document upload API, publish guard for pending processing, FAQ embedding reset`

---

### Task 4: Worker xử lý tài liệu: trích văn bản, chia chunk, tạo embedding

**Files:**
- Create: `Features/Ingestion/{TextExtractor,Chunker,IngestionService,IngestionWorker}.cs`
- Modify: `Program.cs` (đăng ký `AddHostedService<IngestionWorker>()`; tắt được bằng `Ingestion:Enabled`, mặc định true, còn `ApiFactory` đặt false). Thêm package `UglyToad.PdfPig` và `DocumentFormat.OpenXml`.
- Test: `tests/.../ChunkerTests.cs`, `tests/.../TextExtractorTests.cs`, `tests/.../IngestionServiceTests.cs` (gọi trực tiếp `IngestionService`, không qua worker)

**Interfaces:**
- **`record ExtractedBlock(string Text, int? Trang, string? TieuDe)`**
- **`TextExtractor.ExtractAsync(byte[] data, string ext, CancellationToken ct) → Task<IReadOnlyList<ExtractedBlock>>`:**
  - `.pdf`: trích bằng PdfPig theo từng trang. Nếu trung bình mỗi trang dưới **200 ký tự** (PDF scan) thì gọi `IGeminiClient.PdfToMarkdownAsync` và xử lý kết quả như file Markdown.
  - `.docx`: dùng OpenXml, đọc các đoạn văn; đoạn có style `Heading*` được dùng làm `TieuDe`.
  - `.md` và `.txt`: đọc UTF-8. Dòng bắt đầu bằng `#` được coi là tiêu đề.
- **`Chunker.Split(IReadOnlyList<ExtractedBlock> blocks) → IReadOnlyList<ChunkDraft>`**, với `record ChunkDraft(int ThuTu, string NoiDung, string ViTri)`:
  - Gom các đoạn liền nhau trong cùng một tiêu đề tới khi đạt **2.400 ký tự** (khoảng 600 token), tối đa **3.200 ký tự**. Đoạn dài hơn 3.200 ký tự thì cắt ở ranh giới câu.
  - Mỗi chunk sau mở đầu bằng **15% cuối** của chunk trước (chồng lấn), cắt ở ranh giới từ.
  - Khi đổi tiêu đề thì bắt đầu chunk mới.
  - `ViTri` là `"Trang {n}"`, `"{TieuDe}"`, hoặc `"{TieuDe} – trang {n}"` nếu có cả hai.
- **`IngestionService`** (scoped):
  - `Task<int> ProcessPendingDocumentsAsync(CancellationToken ct)`: lấy các tài liệu **nháp** ở `ChoXuLy` và đặt `DangXuLy`, rồi xử lý từng tài liệu:
    1. đọc file, trích văn bản, chia chunk;
    2. xóa chunk cũ của tài liệu đó trong bản nháp;
    3. tạo embedding theo lô bằng `RetrievalDocument`;
    4. thêm `TaiLieuChunk` mới (`ReleaseId = null`, cùng `TaiLieuLid`);
    5. chạy SQL `UPDATE tai_lieu_chunk SET tsv = to_tsvector('simple', unaccent(noi_dung)) WHERE release_id IS NULL AND tai_lieu_lid = @lid`;
    6. đặt `Xong`.

    Lỗi ở bất kỳ bước nào thì đặt `ThatBai` với `Loi` tiếng Việt: `GeminiNotConfiguredException` → `"Chưa cấu hình Gemini API key"`, không trích được chữ → `"Không trích được nội dung từ file"`, lỗi khác → `"Lỗi xử lý: {message}"`. Trả về số tài liệu đã xử lý.
  - `Task<int> EmbedPendingFaqsAsync(CancellationToken ct)`: FAQ nháp có `Embedding == null` được embed theo văn bản `cauHoi + "\n" + join(cachHoiKhac)`.
  - Việc xử lý đi qua DbContext, nên audit chạy như bình thường. Việc đổi `TrangThai` không sinh diff, nhờ Task 3.
- **`IngestionWorker`:** cứ mỗi `Ingestion:PollSeconds` (mặc định 5) gọi cả hai hàm trên trong một scope mới; nuốt lỗi và ghi log, không để worker chết.

- [ ] **Step 1: Viết test thất bại**
  - `ChunkerTests`:
    - 10 đoạn, mỗi đoạn 500 ký tự, cùng một tiêu đề → mọi chunk dài không quá 3.200 ký tự, chunk sau bắt đầu bằng phần cuối của chunk trước.
    - Đổi tiêu đề → bắt đầu chunk mới, và `ViTri` chứa tiêu đề.
    - Một đoạn 7.000 ký tự → cắt ở dấu chấm câu.
  - `TextExtractorTests`:
    - DOCX tạo trong test (OpenXml) có `Heading1` "Học phí" → khối có `TieuDe == "Học phí"`.
    - File `.md` có `# Ký túc xá` → có tiêu đề.
    - PDF hầu như không có chữ → gọi `PdfToMarkdownAsync` của bản giả. PDF tạo bằng PdfPig `PdfDocumentBuilder` với 1 trang trống.
  - `IngestionServiceTests`:
    - `Processes_document_into_chunks_with_tsv_and_embeddings`: tải lên `.md` có 2 mục → trạng thái `Xong`, số chunk ≥ 2, `tsv` không null, embedding có 768 chiều.
    - `Reprocess_replaces_chunks`
    - `Missing_api_key_marks_failed_with_vietnamese_reason`: bản giả ném `GeminiNotConfiguredException`.
    - `Embeds_pending_faqs` *(Review Focus 5)*: sau Task 3, FAQ đã sửa có embedding null → `EmbedPendingFaqsAsync` gán embedding lại.
- [ ] **Step 2: Chạy test, kỳ vọng FAIL**
- [ ] **Step 3: Cài đặt**
- [ ] **Step 4: Chạy toàn bộ `dotnet test backend`, kỳ vọng PASS**
- [ ] **Step 5: Commit** — `feat(ingestion): extract, chunk and embed documents and FAQs in background`

---

### Task 5: Tìm kiếm tri thức (FAQ vector + hybrid RRF trên chunk)

**Files:**
- Create: `Features/Agent/Search/{IKnowledgeSearch,KnowledgeSearch,Rrf}.cs`
- Test: `tests/.../RrfTests.cs`, `tests/.../KnowledgeSearchTests.cs`

**Interfaces:**
- **Kiểu dữ liệu:**
  - `record SearchHit(string NoiDung, string NguonTen, string? NguonUrl, string? ViTri, double Score)`
  - `record SearchResult(string Kind, string? FaqAnswer, IReadOnlyList<SearchHit> Hits)`, với `Kind` là `"faq"`, `"tai_lieu"` hoặc `"khong_co"`
- **`IKnowledgeSearch.SearchAsync(string cauHoi, KbScope scope, CancellationToken ct) → Task<SearchResult>`:**
  1. Phân giải release qua `IKnowledgeQuery.ResolveReleaseAsync`, rồi embed câu hỏi bằng `RetrievalQuery`.
  2. **FAQ:** `ORDER BY embedding <=> @q LIMIT 1`, có lọc release. Nếu `1 - distance ≥ Rag:FaqThreshold` thì trả `Kind = "faq"`, `FaqAnswer = tra_loi`, và một hit duy nhất mang nguồn của FAQ (`NguonTen` mặc định `"Câu hỏi thường gặp"`).
  3. **Chunk:**
     - vector top 20 (`embedding <=> @q`);
     - full-text top 20: `tsv @@ plainto_tsquery('simple', unaccent(@q))`, sắp theo `ts_rank`;
     - gộp bằng `Rrf.Fuse(lists, k: 60)` theo id chunk;
     - nhân điểm với **1,1** nếu `tai_lieu.nam_ap_dung` bằng năm hiện tại của scope;
     - lấy `Rag:TopK` hit. Hit mang `NguonTen = tai_lieu.tieu_de` (hoặc `nguon_ten` nếu có), `NguonUrl = tai_lieu.nguon_url` và `ViTri = chunk.vi_tri`.
  4. Không có hit nào thì trả `Kind = "khong_co"`.

  Câu truy vấn dùng SQL thô (`FromSql` hoặc `SqlQuery`) với tham số `Pgvector.Vector`.
- **`static class Rrf.Fuse(IEnumerable<IReadOnlyList<Guid>> rankedLists, int k) → IReadOnlyList<(Guid Id, double Score)>`**: điểm của một id là tổng `1/(k + rank)`, với `rank` bắt đầu từ 1. Kết quả xếp giảm dần theo điểm; hòa điểm thì giữ thứ tự xuất hiện đầu tiên.

- [ ] **Step 1: Viết test thất bại** (dùng `f.Gemini.EmbedOverride` để kiểm soát vector)
  - `RrfTests`: các danh sách `[a,b,c]` và `[c,a]` cho thứ tự `a, c, b`; điểm của `a` bằng `1/61 + 1/62`.
  - `Faq_hit_above_threshold_returns_answer`
  - `Faq_below_threshold_falls_through_to_chunks`
  - `Fulltext_matches_unaccented_query`: chunk "Ký túc xá có 2.000 chỗ", query "ky tuc xa" → tìm thấy, kể cả khi vector không giống.
  - `Draft_scope_does_not_see_published_only_and_vice_versa`
  - `Current_year_document_ranks_first_on_tie`
  - `No_hits_returns_khong_co`
- [ ] **Step 2: Chạy test, kỳ vọng FAIL**
- [ ] **Step 3: Cài đặt**
- [ ] **Step 4: Chạy toàn bộ `dotnet test backend`, kỳ vọng PASS**
- [ ] **Step 5: Commit** — `feat(agent): hybrid knowledge search with FAQ shortcut and RRF`

---

### Task 6: ToolRegistry và 8 tool

**Files:**
- Create: `Features/Agent/Tools/{IAgentTool,ToolResult,ToolContext,ToolRegistry,VietnameseNumberReader}.cs`, cùng `TimNganhTool`, `TraDiemChuanTool`, `TraChiTieuTool`, `TraToHopTool`, `TraHocPhiTool`, `TraLichTuyenSinhTool`, `TimKiemTriThucTool`, `ChuyenTiepCanBoTool` (mỗi tool một file)
- Modify: `Query/IKnowledgeQuery.cs` + `KnowledgeQuery.cs` (thêm các truy vấn tool cần, tất cả lọc theo `KbScope`)
- Test: `tests/.../VietnameseNumberReaderTests.cs`, `tests/.../AgentToolsTests.cs`

**Interfaces:**
- **Ngữ cảnh và kết quả:**
  - `record ToolContext(KbScope Scope, Guid ConversationId)`
  - `record NguonRef(string Ten, string? Url)`
  - `record ToolResult(string KetQua, JsonNode? DuLieu, string? CachDoc, int? NamApDung, IReadOnlyList<NguonRef> Nguon, string? HuongDan)`. Có hàm `JsonObject ToJson()`, xuất các key snake_case đúng như Global Constraints.
  - Hàm dựng: `ToolResult.Ok(...)`, `NotFound(string what)` (với `huong_dan` = `"Không có {what} trong cơ sở dữ liệu. Không được suy ra là trường không có; hãy nói mình chưa có thông tin và hỏi người dùng có muốn để lại liên hệ để cán bộ tư vấn không."`), `AskBack(JsonNode options, string huongDan)`, `Error()`.
- **`interface IAgentTool`:** `string Name { get; }`, `JsonObject Declaration { get; }` (đúng cấu trúc `{ name, description, parameters }` của Gemini), `Task<ToolResult> InvokeAsync(JsonObject args, ToolContext ctx, CancellationToken ct)`.
- **`ToolRegistry`** (scoped):
  - `JsonArray Declarations()`.
  - `Task<ToolResult> InvokeAsync(string name, JsonObject args, ToolContext ctx, CancellationToken ct)`: bọc timeout **3 giây** bằng `CancellationTokenSource`. Hết giờ, gặp exception, hoặc tên tool lạ thì trả `ToolResult.Error()` và ghi log.
- **`VietnameseNumberReader`:**
  - `string Read(decimal n)` đọc phần nguyên theo tiếng Việt, phần thập phân đọc sau chữ "phẩy".
  - `string ReadMoney(decimal vnd)` = `Read` + `" đồng"`.
- **Tool** (tên và tham số đúng spec 5.1; `nam` mặc định là năm hiện tại của scope):
  - `tim_nganh(tu_khoa)`:
    - Khớp theo `ma_nganh` bằng nhau, hoặc `unaccent(lower(ten))` hay alias (`ten_goi_khac`, so không phân biệt hoa thường, bỏ dấu) chứa `unaccent(lower(tu_khoa))`.
    - Nếu không khớp trực tiếp thì dùng `similarity(unaccent(lower(ten)), unaccent(lower(@q))) ≥ 0.3`, lấy tối đa 5 kết quả.
    - Đúng 1 kết quả → `ok` với `du_lieu = { ma_nganh, ten, con_tuyen }`. Nhiều kết quả → `can_hoi_lai` với `du_lieu = { lua_chon: [{ ma_nganh, ten }] }`. Không có → `khong_tim_thay("ngành này")`.
  - `tra_diem_chuan(nganh, nam?, phuong_thuc?)`:
    - Phân giải ngành theo cách của `tim_nganh`. Kết quả mơ hồ thì trả `can_hoi_lai`.
    - `phuong_thuc` khớp `ma` hoặc `ten` (bỏ dấu).
    - `du_lieu = [{ phuong_thuc, diem, thang_diem, ghi_chu }]`, `cach_doc` ghép các mục `"{Read(diem)} điểm ({phuong_thuc})"`, `nguon` lấy từ các bản ghi.
  - `tra_chi_tieu`, `tra_hoc_phi` (`cach_doc` dùng `ReadMoney`), `tra_lich_tuyen_sinh`: cùng khuôn với `tra_diem_chuan`. Riêng ngày giờ của `tra_lich_tuyen_sinh` đổi sang `Asia/Ho_Chi_Minh`, định dạng `dd/MM/yyyy HH:mm`.
  - `tra_to_hop(nganh?, to_hop?)`: tra theo chiều ngành → tổ hợp hoặc tổ hợp → ngành. `cach_doc` dùng `ToHop.CachDoc`. Thiếu cả hai tham số thì trả `can_hoi_lai`.
  - `tim_kiem_tri_thuc(cau_hoi)` gọi `IKnowledgeSearch`:
    - `faq` → `ok` với `du_lieu = { tra_loi }`;
    - `tai_lieu` → `ok` với `du_lieu = { doan: [{ noi_dung, vi_tri }] }`;
    - `khong_co` → `khong_tim_thay("thông tin này")`.
  - `chuyen_tiep_can_bo(ly_do, ho_ten, lien_he)`: thiếu `ho_ten` hoặc `lien_he` thì trả `can_hoi_lai` với `huong_dan = "Hãy hỏi họ tên và số điện thoại hoặc email của người dùng trước."`. Đủ thì gọi `ConversationStore.CreateTicketAsync` và trả `ok` với `du_lieu = { ma_yeu_cau, thong_bao: "Cán bộ tuyển sinh sẽ liên hệ lại trong giờ hành chính." }`.

- [ ] **Step 1: Viết test thất bại**
  - `VietnameseNumberReaderTests` với `[Theory]`:

    | Đầu vào | Kết quả |
    |---|---|
    | 21.5 | `"hai mươi mốt phẩy năm"` |
    | 25 | `"hai mươi lăm"` |
    | 14 | `"mười bốn"` |
    | 105 | `"một trăm lẻ năm"` |
    | 1200 | `"một nghìn hai trăm"` |
    | 2026 | `"hai nghìn không trăm hai mươi sáu"` |
    | 24.75 | `"hai mươi bốn phẩy bảy mươi lăm"` |
    | 0 | `"không"` |

    Thêm `ReadMoney(1200000) == "một triệu hai trăm nghìn đồng"`.
  - `AgentToolsTests` (dữ liệu tạo bằng CRUD rồi xuất bản; scope `Published`):
    - `Tim_nganh_matches_unaccented_alias_and_typo` *(Review Focus 4)*: "cong nghe thong tin", "CNTT" (alias) và "cong nghe thong tinn" đều cho `ok`.
    - `Tim_nganh_ambiguous_returns_can_hoi_lai`: có hai ngành "Kỹ thuật phần mềm" và "Kỹ thuật điện", tìm "ky thuat" → `can_hoi_lai` với 2 lựa chọn.
    - `Tra_diem_chuan_returns_reading_and_sources`: 21.5 → `cach_doc` chứa "hai mươi mốt phẩy năm điểm", `nguon[0].ten` đúng.
    - `Not_found_has_huong_dan`
    - `Draft_scope_reads_draft`
    - `Tool_timeout_returns_loi_he_thong`: đăng ký một tool giả chờ 5 giây → kết quả `loi_he_thong` trong khoảng 3,5 giây.
    - `Chuyen_tiep_requires_contact_then_creates_ticket`
    - `Declarations_have_all_8_tools_with_object_parameters`
- [ ] **Step 2: Chạy test, kỳ vọng FAIL**
- [ ] **Step 3: Cài đặt** (`pg_trgm` và `unaccent` đã được bật; gọi `similarity` và `unaccent` bằng SQL thô có tham số)
- [ ] **Step 4: Chạy toàn bộ `dotnet test backend`, kỳ vọng PASS**
- [ ] **Step 5: Commit** — `feat(agent): tool registry with eight admissions tools and Vietnamese number reading`

---

### Task 7: System prompt và vòng lặp chat (`ChatOrchestrator`)

**Files:**
- Create: `Features/Agent/Chat/{SystemPrompt,ChatEvents,ChatOrchestrator}.cs`, `Features/Agent/Chat/Prompts/system.v1.md` (`CopyToOutputDirectory`)
- Test: `tests/.../SystemPromptTests.cs`, `tests/.../ChatOrchestratorTests.cs`

**Interfaces:**
- **`system.v1.md`:**
  - Chứa đủ các quy tắc bắt buộc của spec 5.3, viết bằng tiếng Việt, mỗi quy tắc một gạch đầu dòng.
  - Có thêm hai quy tắc rút ra từ spike: tham số phương thức xét tuyển phải lấy đúng giá trị của tool; với `khong_tim_thay` không được suy ra "trường không có".
  - Có placeholder `{{TRUONG}}` (thay bằng `"Trường Đại học Thủ Dầu Một"`), `{{NAM}}` (năm hiện tại của scope, hoặc "chưa xác định") và `{{KENH}}` (`"chat"` thì trả lời đầy đủ; `"live"` thì trả lời 2–4 câu).
- **`SystemPrompt.Build(string kenh, int? nam) → string`**: đọc file một lần rồi cache.
- **Sự kiện chat** (`ChatEvents.cs`), tất cả là record implement `ChatEvent`:
  - `Delta(string Text)`
  - `ToolStatus(string Name, string State)` với `State` là `"start"` hoặc `"end"`
  - `Citations(IReadOnlyList<NguonRef> Items)`
  - `Done(Guid ConversationId, Guid MessageId)`
  - `ChatError(string Title)`
- **`ChatOrchestrator.RunAsync(Guid? conversationId, string message, KbScope scope, string? userAgent, CancellationToken ct) → IAsyncEnumerable<ChatEvent>`:**
  1. `GetOrCreateAsync` với kênh `"chat"`, `laHoiThu = scope == Draft`, và release đang hoạt động (null khi scope là draft).
  2. Lưu tin nhắn của người dùng.
  3. Dựng `contents` gồm 10 lượt gần nhất (`RecentAsync`, mỗi tin nhắn thành một part `{text}`) và tin nhắn hiện tại.
  4. Lặp tối đa `Agent:MaxToolRounds` vòng:
     - stream qua `IGeminiClient`;
     - part `text` thì phát `Delta`;
     - nếu lượt có `functionCall`: thêm nguyên văn lượt của model vào `contents` (role `"model"`, parts giữ nguyên, kể cả `thoughtSignature`); với mỗi lệnh gọi, phát `ToolStatus start`, chạy `ToolRegistry.InvokeAsync`, rồi phát `ToolStatus end`; sau đó thêm role `"user"` với các part `{ functionResponse: { name, response: result.ToJson() } }`, rồi sang vòng tiếp;
     - không có `functionCall` thì dừng.
  5. Hết số vòng mà vẫn chưa có chữ, hoặc model không trả chữ nào: phát `Delta` với câu dự phòng `"Xin lỗi, mình chưa tìm được câu trả lời phù hợp. Bạn có muốn để lại thông tin để cán bộ tuyển sinh liên hệ không?"`.
  6. Gom nguồn từ mọi `ToolResult.Nguon`, bỏ trùng theo (Ten, Url); nếu có thì phát `Citations`.
  7. Lưu tin nhắn của trợ lý: nội dung là toàn bộ chữ đã phát; `tool_calls` là mảng `{ name, args, ket_qua }`; `latency_ms` gồm `{ first_token, total }`.
  8. Phát `Done`.
  9. **Lỗi** `GeminiUnavailableException` hoặc `GeminiNotConfiguredException` ở bất kỳ vòng nào: phát `ChatError` với câu mặc định ở Global Constraints, và lưu tin nhắn của trợ lý gồm chữ đã có cộng câu đó với `co_loi = true`. Sau đó vẫn phát `Done`.

- [ ] **Step 1: Viết test thất bại** (dùng `FakeGeminiClient.Script`)
  - `SystemPromptTests`: có "Trường Đại học Thủ Dầu Một" và "2026"; không còn sót `{{`; có cụm "Mọi con số".
  - `Text_only_answer_streams_deltas_and_persists`
  - `Tool_round_trip_appends_model_parts_verbatim_and_function_response`: lượt 1 là `functionCall tra_diem_chuan` có `thoughtSignature: "sig1"`, lượt 2 là chữ. Kiểm tra request thứ hai trong `f.Gemini.Requests` có part model chứa `thoughtSignature == "sig1"` và một part `functionResponse` với `response.ket_qua == "ok"`. Thứ tự sự kiện: `ToolStatus start`, `ToolStatus end`, `Delta…`, `Citations`, `Done`.
  - `Max_rounds_exceeded_emits_fallback_text`: script 6 lượt toàn `functionCall`.
  - `Gemini_failure_mid_stream_emits_error_and_saves_flagged_message` *(Review Focus 3)*: lượt 1 phát `Delta("Điểm chuẩn ")` rồi ném `GeminiUnavailableException`. Kỳ vọng các sự kiện `Delta`, `ChatError` (câu mặc định) và `Done`; tin nhắn của trợ lý có `co_loi = true` và nội dung bắt đầu bằng "Điểm chuẩn ".
  - `History_includes_last_10_turns`
  - `User_message_pii_masked_in_db` *(Review Focus 2)*
  - `Draft_scope_conversation_marked_hoi_thu`
- [ ] **Step 2: Chạy test, kỳ vọng FAIL**
- [ ] **Step 3: Cài đặt**
- [ ] **Step 4: Chạy toàn bộ `dotnet test backend`, kỳ vọng PASS**
- [ ] **Step 5: Commit** — `feat(agent): system prompt and chat orchestrator with tool loop`

---

### Task 8: `POST /api/chat` (SSE), quyền theo scope, giới hạn

**Files:**
- Create: `Features/Agent/Chat/ChatEndpoints.cs`
- Modify: `Program.cs` (thêm rate limit policy `chat`)
- Test: `tests/.../ChatEndpointTests.cs` (có helper đọc SSE: `ReadSseAsync(HttpResponseMessage) → List<(string Event, JsonElement Data)>`)

**Interfaces:**
- **Request:** `POST /api/chat`, body `{ conversationId?: Guid, message: string, scope?: "published" | "draft" }`. Endpoint cho phép gọi ẩn danh.
  - `scope = "draft"` mà không có JWT admin hợp lệ (role `Admin`) → 403 `"Chỉ cán bộ quản trị được hỏi thử trên bản nháp"`.
  - `message` rỗng hoặc dài hơn 1.000 ký tự → 400 `"Câu hỏi phải có từ 1 đến 1.000 ký tự"`.
  - Không có release đang hoạt động khi scope là published → stream một `ChatError` với `"Hệ thống chưa có dữ liệu tuyển sinh được phát hành"`, rồi `Done`.
- **Response:** `Content-Type: text/event-stream`, `Cache-Control: no-cache`, header `X-Accel-Buffering: no`. Mỗi sự kiện có dạng `event: <tên>\ndata: <json camelCase>\n\n`, gửi xong thì flush ngay.

  | Event | Data |
  |---|---|
  | `delta` | `{ text }` |
  | `tool_status` | `{ name, state }` |
  | `citations` | `{ items: [{ ten, url }] }` |
  | `done` | `{ conversationId, messageId }` |
  | `error` | `{ title }` |

- **Rate limit:** policy `chat` theo IP, `RateLimit:ChatPermitsPerMinute` (mặc định 20), trả 429.
- Client ngắt kết nối thì hủy qua `HttpContext.RequestAborted`.

- [ ] **Step 1: Viết test thất bại**
  - `Streams_events_in_order_for_published_scope`
  - `Draft_scope_without_admin_returns_403`; `Draft_scope_with_admin_streams`
  - `Message_over_1000_chars_returns_400`
  - `No_active_release_streams_error_then_done`
  - `Gemini_error_streams_error_event_not_500` *(Review Focus 3)*: mã HTTP 200, các event là `delta`, `error`, `done`.
  - `Conversation_id_continues_history`
- [ ] **Step 2: Chạy test, kỳ vọng FAIL**
- [ ] **Step 3: Cài đặt**
- [ ] **Step 4: Chạy toàn bộ `dotnet test backend`, kỳ vọng PASS**
- [ ] **Step 5: Commit** — `feat(agent): SSE chat endpoint with draft-scope authorization and limits`

---

### Task 9: Smoke test Gemini thật và tài liệu cấu hình

**Files:**
- Create: `tests/.../GeminiSmokeTests.cs`, gắn `[Trait("Category", "GeminiLive")]`
- Modify:
  - `backend/README.md`: thêm mục "Cấu hình Gemini": `Gemini__ApiKey` qua user-secrets hoặc biến môi trường; các khóa `Agent`, `Embedding`, `Rag`, `Ingestion`, `Storage`; cách chạy smoke test.
  - `.github/workflows/ci.yml`: thêm `--filter "Category!=GeminiLive"` cho `dotnet test`.
  - `docs/superpowers/plans/p1-followups.md`: đánh dấu mục `TaiLieu.ContentKey` và mục embedding FAQ là đã xử lý.

**Interfaces:**
- **Smoke test:**
  - Dùng `ApiFactory` nhưng **không** thay Gemini bằng bản giả (factory có cờ `UseRealGemini`), và lấy key từ biến môi trường `GEMINI_API_KEY`.
  - Thiếu key thì gọi `Skip` (xUnit `[SkippableFact]` hoặc thoát sớm và ghi rõ là đã bỏ qua).
  - Kịch bản: tạo và xuất bản ngành CNTT với điểm chuẩn 21,5 cho "thi THPT"; tải lên một file `.md` về ký túc xá rồi chạy `IngestionService` thật.
  - Hỏi qua `/api/chat` hai câu:
    1. `"Điểm chuẩn ngành công nghệ thông tin năm 2026 bao nhiêu?"` → có event `tool_status` với `name == "tra_diem_chuan"`, và chữ trả lời chứa "21,5" hoặc "21.5".
    2. `"Ký túc xá có bao nhiêu chỗ?"` → có `tim_kiem_tri_thuc` và có `citations`.

- [ ] **Step 1: Viết smoke test**
- [ ] **Step 2: Chạy không có key** (`dotnet test backend --filter Category=GeminiLive`), kỳ vọng được báo là skip, không fail
- [ ] **Step 3: Chạy với key thật**: lấy key từ file `.env` mà controller cung cấp, **không in key ra**. Kỳ vọng PASS. Ghi tên model thực tế đã dùng vào báo cáo.
- [ ] **Step 4: Cập nhật README, CI và followups; chạy toàn bộ `dotnet test backend --filter "Category!=GeminiLive"`, kỳ vọng PASS**
- [ ] **Step 5: Commit** — `test(agent): gated live Gemini smoke test and configuration docs`

---

### Task 10: Admin: trang "Tài liệu"

**Files:**
- Create: `frontend/apps/admin/src/api/documents.ts`, `src/documents/DocumentsPage.tsx`
- Modify: `src/layout/navigation.ts` + `AdminLayout.tsx` (mục menu "Tài liệu" đặt sau "Câu hỏi thường gặp", route `/tai-lieu`), `src/api/types.ts` (thêm `TaiLieuDto`)
- Test: `src/documents/DocumentsPage.test.tsx`

**Interfaces:**
- **API:** `useDocuments()` (GET `/api/admin/tai-lieu`, key `["kb","tai-lieu"]`, có `refetchInterval` 3 giây khi còn tài liệu `ChoXuLy` hoặc `DangXuLy`), `uploadDocument(form)`, `reprocessDocument(lid)`, `deleteDocument(lid)`.
- **Giao diện:**
  - Form tải lên gồm Tiêu đề*, Năm áp dụng, Tên nguồn, Link nguồn, và file (`.pdf,.docx,.md,.txt`). File trên 20 MB bị chặn ngay ở client với thông báo `"File vượt quá 20 MB"`.
  - Bảng gồm các cột Tiêu đề, File, Năm, Trạng thái, Số đoạn, Cập nhật, và cột hành động "Xử lý lại" / "Xóa" (có Popconfirm).
  - Trạng thái hiển thị bằng Tag: `ChoXuLy` "Chờ xử lý", `DangXuLy` "Đang xử lý" (xanh dương, kèm spin), `Xong` "Đã xử lý" (xanh lá), `ThatBai` "Lỗi" (đỏ, tooltip hiện `loi`).
  - Phía trên bảng có chú thích: `"Tài liệu được xử lý tự động. Cần chờ xử lý xong trước khi xuất bản."`.
  - Mọi mutation đều invalidate `["kb"]`.

- [ ] **Step 1: Viết test thất bại**
  - `Lists_documents_with_status_tags`
  - `Upload_posts_multipart_and_refreshes`
  - `Failed_shows_reason_tooltip_and_reprocess`
  - `Rejects_file_over_20mb`
  - `Polls_while_processing`: dùng fake timers hoặc đếm số request.
- [ ] **Step 2: Chạy test, kỳ vọng FAIL**
- [ ] **Step 3: Cài đặt**
- [ ] **Step 4: Chạy `pnpm -C frontend typecheck && pnpm -C frontend test`, kỳ vọng PASS**
- [ ] **Step 5: Commit** — `feat(admin): documents page with upload, status and reprocess`

---

### Task 11: Admin: trang "Hỏi thử" (đọc SSE)

**Files:**
- Create: `src/api/chat.ts`, `src/playground/PlaygroundPage.tsx`
- Modify: menu (mục "Hỏi thử" đặt trước "Xuất bản", route `/hoi-thu`)
- Test: `src/api/chat.test.ts`, `src/playground/PlaygroundPage.test.tsx`

**Interfaces:**
- **`streamChat(body: { conversationId?: string; message: string; scope: "draft" | "published" }, onEvent: (e: ChatStreamEvent) => void, signal?: AbortSignal): Promise<void>`:**
  - Gọi `fetch("/api/chat")`, gắn header `Authorization` qua authStore, giống cách của `apiFetch`.
  - Phản hồi không phải 2xx thì đi qua đường xử lý lỗi của `apiFetch` (`ApiError`, 401 → handler hết phiên).
  - Đọc `ReadableStream`, tách theo dòng trống, và parse cặp `event:` / `data:`.
  - `type ChatStreamEvent = { type: "delta"; text } | { type: "tool_status"; name; state } | { type: "citations"; items: { ten: string; url?: string | null }[] } | { type: "done"; conversationId; messageId } | { type: "error"; title }`.
- **Giao diện `PlaygroundPage`:**
  - Chọn phạm vi bằng `Segmented`: "Bản nháp" (mặc định) / "Bản đang phát hành".
  - Khung hội thoại gồm bong bóng người hỏi và trợ lý. Câu trả lời hiện dần theo `delta`. Dưới câu trả lời có thẻ nguồn (tên và link) và danh sách "Công cụ đã gọi" (tên tool, trạng thái).
  - Ô nhập có giới hạn 1.000 ký tự và bộ đếm. Nút "Gửi" bị khóa trong lúc stream; nút "Dừng" gọi `abort`.
  - Nút "Hội thoại mới" xóa `conversationId` và nội dung đang hiển thị. Đổi phạm vi cũng bắt đầu hội thoại mới.
  - Sự kiện `error` → hiện `Alert` với `title`, vẫn giữ phần chữ đã nhận.

- [ ] **Step 1: Viết test thất bại**
  - `chat.test.ts`: một body SSE bị cắt thành các mảnh tùy ý (kể cả cắt giữa một dòng) vẫn parse ra đúng chuỗi sự kiện; 403 ném `ApiError` có title của server.
  - `PlaygroundPage.test.tsx`:
    - `Streams_answer_tools_and_citations`
    - `Sends_draft_scope_by_default_and_reuses_conversation_id`
    - `Error_event_shows_alert_and_keeps_partial_text`
    - `Stop_aborts_stream`
    - `Message_limit_1000`
- [ ] **Step 2: Chạy test, kỳ vọng FAIL**
- [ ] **Step 3: Cài đặt**
- [ ] **Step 4: Chạy `pnpm -C frontend typecheck && pnpm -C frontend test`, kỳ vọng PASS**
- [ ] **Step 5: Commit** — `feat(admin): Hỏi thử playground with streaming answers, tools and citations`

---

### Task 12: Tài liệu và hoàn tất

**Files:**
- Modify:
  - `frontend/README.md`: thêm mô tả trang Tài liệu và Hỏi thử.
  - `docs/superpowers/plans/README.md`: dòng 3 chuyển thành "Hoàn thành (ngày)", kèm link tới file kế hoạch này.
  - Spec mục 2: ghi lý do dùng Gemini REST cho kênh chat; SDK `Google.GenAI` vẫn để dành cho Live ở Kế hoạch 4.
- Test: không có test mới. Chạy lại toàn bộ:
  - `dotnet test backend --filter "Category!=GeminiLive"`
  - `pnpm -C frontend typecheck && pnpm -C frontend test && pnpm -C frontend build`
  - e2e của Kế hoạch 2, để chắc chắn không hỏng luồng cũ

- [ ] **Step 1: Cập nhật tài liệu**
- [ ] **Step 2: Chạy toàn bộ kiểm tra, kỳ vọng PASS**
- [ ] **Step 3: Commit** — `docs: plan 3 status, chat and documents admin docs`

---

## Hoàn thành kế hoạch 3 khi
- Các bộ test đều xanh. Smoke test với Gemini thật cho thấy câu hỏi điểm chuẩn gọi `tra_diem_chuan` và trả về số đúng, còn câu hỏi ký túc xá gọi `tim_kiem_tri_thuc` và có trích nguồn.
- Cán bộ làm trọn được luồng: tải tài liệu → chờ "Đã xử lý" → Hỏi thử trên bản nháp → xuất bản → `/api/chat` ở phạm vi published trả lời bằng dữ liệu mới.
- Ngoài phạm vi, để kế hoạch sau:
  - Live/giọng nói và widget: Kế hoạch 4.
  - Trang hội thoại, ticket, dashboard và nút "Tạo FAQ từ câu này": Kế hoạch 5.
  - Reranker và bộ đánh giá: Kế hoạch 6.
