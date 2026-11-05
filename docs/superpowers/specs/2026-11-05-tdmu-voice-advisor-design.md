# Thiết kế hệ thống: Trợ lý tư vấn tuyển sinh bằng giọng nói TDMU

- **Ngày:** 05/11/2026
- **Trạng thái:** chờ duyệt
- **Đề tài:** Nghiên cứu ứng dụng AI Agent tương tác bằng giọng nói trong hỗ trợ tư vấn tuyển sinh trên website Trường Đại học Thủ Dầu Một (NCKH sinh viên 2026–2027, loại hình Ứng dụng)
- **Tài liệu liên quan:**
  - Kế hoạch đề tài: `docs/KE_HOACH_THUC_HIEN.md`
  - Kết quả spike Gemini Live: `prototype/gemini_live/README.md`

---

## 1. Mục tiêu và phạm vi

### 1.1 Mục tiêu
Xây dựng sản phẩm mẫu có thể bàn giao, gồm:
1. **Hệ quản lý cơ sở tri thức (KB CMS):** cán bộ truyền thông tuyển sinh thêm, sửa, xóa và xuất bản thông tin tuyển sinh. Đây là nguồn dữ liệu duy nhất mà AI được dùng.
2. **AI Agent** trả lời câu hỏi qua hai kênh:
   - **chat văn bản**, dùng Gemini Flash với function calling;
   - **giọng nói thời gian thực**, dùng Gemini Live.

   Cả hai kênh dùng chung một bộ tool và một system prompt.
3. **Widget** nhúng vào website, và **trang showcase** để trình bày đề tài.
4. **Bộ đánh giá offline** đo độ chính xác, tỉ lệ gọi tool, WER, độ trễ và SUS.

### 1.2 Tiêu chí thành công

| Chỉ số | Mục tiêu |
|---|---|
| Tỉ lệ trả lời đúng trên bộ test | ≥ 85% |
| Tỉ lệ gọi tool đúng (câu hỏi số liệu) | ≥ 95% |
| Câu trả lời sai số liệu | 0 |
| Từ chối đúng câu ngoài phạm vi | ≥ 90% |
| WER của transcript đầu vào (giọng ba miền) | < 15% |
| Độ trễ từ lúc nói xong đến âm thanh đầu tiên | trung vị < 2 s, p95 < 3,5 s |
| SUS khi thử nghiệm người dùng (≥ 30 người) | ≥ 70 |

### 1.3 Ngoài phạm vi (bản đầu)
- Luồng duyệt nhiều cấp, nhiều vai trò. Bản đầu chỉ có một vai trò cán bộ quản trị.
- Trình soạn thảo bài viết. Nội dung dài được đưa vào bằng tài liệu tải lên.
- Đa ngôn ngữ, ứng dụng di động, tích hợp mạng xã hội.
- Trình duyệt nối thẳng tới Gemini bằng ephemeral token. Chỉ làm như thực nghiệm phụ nếu còn thời gian.
- Lưu trữ audio của người dùng.

## 2. Quyết định đã chốt

| Vấn đề | Quyết định | Lý do |
|---|---|---|
| Kênh giọng nói | Gemini Live, model chính `gemini-3.8-live`, dự phòng `gemini-3.1-flash-live-preview` | Kết quả spike: 5/5 hành vi đúng, độ trễ trung vị khoảng 1,6 s. Dòng 2.5 native audio từng tự trả lời mà không gọi tool |
| Kiến trúc | Modular monolith .NET 8 | Một ngôn ngữ backend, một lần triển khai, hợp nhóm 2 người |
| Stack | ASP.NET Core 8 Web API · React TypeScript (Vite) · PostgreSQL + pgvector | Nhóm đã chọn |
| SDK Gemini | `Google.GenAI` (.NET, bản 1.28.0 trở lên, có Live API) | Đã kiểm tra package có `ConnectAsync`, `SendRealtimeInput`, `SendToolResponse` |
| Nội dung trong KB | (A) dữ liệu có cấu trúc · (B) FAQ · (D) tài liệu tải lên · (E) lịch tuyển sinh | Đã duyệt |
| Quy trình nội dung | Nháp → Xuất bản theo release, một vai trò, có audit log và "Hỏi thử" | Đã duyệt |
| Số liệu | Chỉ trả lời qua tool truy vấn SQL, không qua RAG | Chống bịa số liệu |
| Đánh giá | Script Python offline trong `eval/` | Hệ sinh thái đánh giá của Python tốt hơn; không chạy trên môi trường thật |

## 3. Kiến trúc tổng thể

```
               ┌────────────── Website / Showcase ──────────────┐
               │  advisor-widget.js (Shadow DOM)                 │
               │   Chat ⇄ SSE /api/chat     Nói ⇄ WS /ws/live    │
               └───────────────┬─────────────────┬───────────────┘
                               ▼                 ▼
┌──────────────────────── Tdmu.Advisor.Api (.NET 8) ─────────────────────────┐
│ Features/Agent        tool registry · system prompt · RAG · chat loop      │
│ Features/Live         WS gateway ⇄ Gemini Live (Google.GenAI)              │──► Gemini API
│ Features/KnowledgeBase CRUD · release · audit · import Excel/CSV           │
│ Features/Ingestion    BackgroundService: trích văn bản → chunk → embedding │──► Gemini Embedding
│ Features/Conversations transcript · ticket · thống kê                      │
│ Infrastructure        EF Core/Npgsql/pgvector · JWT/Identity · rate limit  │
└──────────────────────────────────┬─────────────────────────────────────────┘
                                   ▼
                     PostgreSQL 16 + pgvector + unaccent + pg_trgm
       Admin (React TS) ──REST + JWT──► Api
```

### 3.1 Cấu trúc repo

```
backend/
  src/Tdmu.Advisor.Api/
    Features/{KnowledgeBase,Ingestion,Agent,Live,Conversations}/
    Infrastructure/
  tests/Tdmu.Advisor.Tests/           xUnit + Testcontainers
frontend/                              pnpm workspace, React TS + Vite
  apps/admin/                          KB CMS (Ant Design)
  apps/showcase/                       trang tuyển sinh mô phỏng + giới thiệu đề tài + kết quả
  packages/widget/                     build thành advisor-widget.js
  packages/api-client/                 type sinh từ OpenAPI của Api
eval/                                  Python: bộ test, chấm điểm, WER, độ trễ
prototype/gemini_live/                 spike (tham khảo, không phát triển tiếp)
docker-compose.yml                     postgres, api, admin, showcase (nginx)
.github/workflows/ci.yml
```

### 3.2 Ranh giới giữa các module
- Chỉ module **KnowledgeBase** được ghi dữ liệu tri thức.
- Agent và Live đọc dữ liệu qua interface `IKnowledgeQuery`, có tham số `KbScope` (`Published` hoặc `Draft`).
- **Agent** chứa định nghĩa tool, system prompt, RAG và vòng lặp chat.
- **Live** chỉ lo vận chuyển: audio, transcript, và chuyển tiếp lệnh gọi tool tới tool registry của Agent.
- **Conversations** nhận sự kiện từ Agent và Live để lưu hội thoại, ticket và chỉ số.
- Widget chỉ giao tiếp qua `/api/chat` và `/ws/live`, không bao giờ giữ API key.

## 4. Cơ sở tri thức

### 4.1 Cơ chế release (nháp/xuất bản)
- Mọi bảng tri thức đều có cột `release_id` (nullable). `NULL` là **bản nháp (bản làm việc)**.
- **Xuất bản:** trong một transaction:
  1. tạo bản ghi `release` (số hiệu, ghi chú, người tạo);
  2. sao chép toàn bộ dòng nháp sang `release_id` mới, sao chép luôn embedding của chunk, không tính lại;
  3. đặt `release.is_active = true` cho release mới và `false` cho release cũ.
- **Danh sách thay đổi trước khi xuất bản:** so sánh bản nháp với release đang hoạt động theo khóa nghiệp vụ, ví dụ (ngành, năm, phương thức).
- **Khôi phục:** kích hoạt lại một release cũ. Có thể chọn chép release đó ngược về bản nháp.
- **Hỏi thử:** gọi Agent với `KbScope.Draft`. Chỉ người dùng admin đã xác thực mới được dùng.
- **Audit log:** ghi lại mọi thao tác thêm/sửa/xóa trên bản nháp và mọi lần xuất bản/khôi phục. Mỗi dòng gồm `entity, entity_logical_id, action, before_json, after_json, user_id, at` (`entity_logical_id` là logical id của bản ghi tri thức, hoặc id của release với thao tác xuất bản/khôi phục).

### 4.2 Các thực thể
Mọi bản ghi tri thức có `nguon_ten` và `nguon_url` để trích dẫn.

| Bảng | Trường chính | Ràng buộc |
|---|---|---|
| `mua_tuyen_sinh` | `nam`, `la_hien_tai` | chỉ một mùa hiện tại theo release |
| `nganh` | `ma_nganh`, `ten`, `ten_goi_khac text[]`, `don_vi`, `mo_ta_ngan`, `con_tuyen` | unique `ma_nganh` theo release |
| `phuong_thuc` | `ma`, `ten`, `mo_ta`, `thang_diem numeric(7,2)` (mặc định 30; ĐGNL tới 1200) | unique `ma` theo release |
| `to_hop` | `ma`, `mon text[]`, `cach_doc` | unique `ma` theo release |
| `nganh_to_hop` | `nganh_lid`, `nam`, `to_hop_lid` | unique cả ba theo release |
| `diem_chuan` | `nganh_lid`, `nam`, `phuong_thuc_lid`, `diem numeric(7,2)`, `ghi_chu` | unique (ngành, năm, phương thức) theo release |
| `chi_tieu` | `nganh_lid`, `nam`, `phuong_thuc_lid?`, `so_luong` | unique (ngành, năm, phương thức) theo release |
| `hoc_phi` | `nganh_lid?`, `nam`, `muc numeric(14,2)`, `don_vi` (tín chỉ/năm/học kỳ), `ghi_chu` | unique (ngành hoặc chung khi `nganh_lid` null, năm, đơn vị) theo release |
| `moc_thoi_gian` | `nam`, `ten_moc`, `bat_dau`, `ket_thuc?`, `phuong_thuc_lid?`, `mo_ta` | |
| `faq` | `cau_hoi`, `cach_hoi_khac text[]`, `tra_loi`, `chu_de`, `embedding vector(768)` | |
| `tai_lieu` | `tieu_de`, `duong_dan_file`, `nam_ap_dung`, `trang_thai` (cho_xu_ly/dang_xu_ly/xong/that_bai), `loi` | |
| `tai_lieu_chunk` | `tai_lieu_lid`, `thu_tu`, `noi_dung`, `vi_tri`, `embedding vector(768)`, `tsv tsvector` | |
| `release` | `so_hieu`, `ghi_chu`, `tao_boi`, `tao_luc`, `is_active` | chỉ một release active |
| `audit_log` | như mục 4.1 | |
| Identity | `AspNetUsers`… | |

**Quy ước tham chiếu:** mọi bản ghi tri thức có `id` (khóa vật lý của từng dòng) và `logical_id` (định danh bền qua các release), cùng `release_id` (null = bản nháp). Quan hệ giữa các bảng tham chiếu bằng `*_lid` (logical id), không dùng `*_id`, để bản sao trong release vẫn trỏ đúng thực thể. Mọi bảng tri thức có unique `(release_id, logical_id)`; các unique nghiệp vụ ở trên đều có `release_id` và dùng `NULLS NOT DISTINCT` nên bản nháp (`release_id` null) cũng bị ràng buộc.

**Chỉ mục:**
- HNSW (`vector_cosine_ops`) trên các cột `embedding`.
- GIN trên `tsv`.
- GIN `pg_trgm` (`gin_trgm_ops`) trên `nganh.ten`. Chỉ mục trên `unaccent(ten)` và `ten_goi_khac` để sau.

### 4.3 Nhập liệu và xử lý tài liệu
- **Nhập từ Excel/CSV** cho điểm chuẩn, chỉ tiêu, học phí, tổ hợp:
  - có file mẫu tải về;
  - hiển thị xem trước và báo lỗi theo từng dòng trước khi ghi vào bản nháp.
- **Xử lý tài liệu** (`BackgroundService` lấy việc từ bảng `tai_lieu` có `trang_thai = cho_xu_ly`):
  1. **Trích văn bản.** PDF có lớp chữ dùng PdfPig, DOCX dùng OpenXML. PDF scan, hoặc PDF có bảng bị vỡ cấu trúc, thì nhờ Gemini chuyển sang Markdown.
  2. **Chunk theo tiêu đề/mục**, khoảng 500–800 token, chồng lấn khoảng 15%. Giữ `vi_tri` (trang/mục) để trích dẫn.
  3. **Embedding** bằng `gemini-embedding-001` (768 chiều, theo lô), và tạo `tsv` với `to_tsvector('simple', unaccent(noi_dung))`.
  4. Lỗi ở bất kỳ bước nào: đặt `that_bai`, ghi `loi`. Admin có nút "Xử lý lại".

## 5. Agent

### 5.1 Bộ tool
Đăng ký trong `ToolRegistry`, chuyển được sang `FunctionDeclaration` của Gemini. Dùng chung cho chat và Live.

| Tool | Tham số | Ghi chú |
|---|---|---|
| `tim_nganh` | `tu_khoa` | Khớp tên, tên gọi khác, tên không dấu, kèm so khớp gần đúng `pg_trgm`. Nhiều kết quả thì trả `can_hoi_lai` + danh sách |
| `tra_diem_chuan` | `nganh`, `nam?`, `phuong_thuc?` (enum từ CSDL) | `nam` mặc định là mùa hiện tại |
| `tra_chi_tieu` | `nganh`, `nam?`, `phuong_thuc?` | |
| `tra_to_hop` | `nganh?`, `to_hop?` | Theo chiều ngành → tổ hợp, hoặc tổ hợp → ngành |
| `tra_hoc_phi` | `nganh?`, `nam?` | |
| `tra_lich_tuyen_sinh` | `nam?`, `phuong_thuc?` | |
| `tim_kiem_tri_thuc` | `cau_hoi` | RAG, mục 5.2 |
| `chuyen_tiep_can_bo` | `ly_do`, `ho_ten`, `lien_he` | Chỉ gọi khi người dùng đồng ý; ghi vào `ticket` |

**Cấu trúc kết quả chung:**

```json
{
  "ket_qua": "ok | khong_tim_thay | can_hoi_lai | loi_he_thong",
  "du_lieu": { },
  "cach_doc": "hai mươi mốt phẩy năm điểm",
  "nam_ap_dung": 2026,
  "nguon": [{ "ten": "Thông báo điểm chuẩn 2026", "url": "https://..." }],
  "huong_dan": "chỉ dẫn cho mô hình khi không phải 'ok'"
}
```

- `khong_tim_thay` luôn kèm `huong_dan` nói rõ: "không có trong cơ sở dữ liệu, không được suy ra là trường không có".
- Tool có timeout 3 giây. Hết giờ thì trả `loi_he_thong`.

### 5.2 RAG (`tim_kiem_tri_thuc`)
1. **Tìm FAQ** bằng vector trên câu hỏi và các cách hỏi khác. Nếu cosine ≥ ngưỡng `Rag:FaqThreshold` (mặc định 0,85, sẽ hiệu chỉnh bằng bộ test), trả về câu trả lời chuẩn kèm nguồn.
2. Nếu không, **tìm trong chunk theo hai cách**: vector top 20 và full-text top 20. Gộp bằng **RRF** (k = 60), ưu tiên `nam_ap_dung` bằng mùa hiện tại, lấy top 5.
3. Trả về các đoạn văn kèm `nguon` (tên tài liệu, vị trí).
4. Không dùng reranker. Xếp hạng lại bằng Gemini là thực nghiệm tùy chọn.

### 5.3 System prompt
Một tệp duy nhất, có phiên bản (`prompts/system.v{n}.md`). Năm hiện tại và tên trường được chèn vào lúc chạy. Các quy tắc bắt buộc:
- Mọi con số đều phải lấy từ kết quả tool. Không có kết quả tool thì không nêu số.
- Chỉ khẳng định điều có trong kết quả tool. Với `khong_tim_thay`, nói "mình chưa có thông tin".
- Đọc số và mã tổ hợp theo `cach_doc`.
- Câu không liên quan tuyển sinh: từ chối lịch sự, không chuyển cán bộ.
- Câu tuyển sinh mà không có dữ liệu: hỏi người dùng có muốn để lại liên hệ không; chỉ gọi `chuyen_tiep_can_bo` khi họ đồng ý.
- Kênh giọng nói trả lời 2–4 câu. Kênh chat trả lời đầy đủ hơn. Không tự viết link; nguồn do hệ thống hiển thị.

### 5.4 Kênh chat: `POST /api/chat` (SSE)
- **Request:** `{ conversation_id?, message, scope? }`. `scope=draft` bắt buộc có JWT admin.
- **Vòng lặp:**
  1. gọi `generateContent` (model `Agent:ChatModel`, mặc định Gemini Flash) với lịch sử 10 lượt gần nhất và bộ tool;
  2. nếu có lệnh gọi tool thì chạy tool và lặp lại, tối đa 5 vòng;
  3. stream câu trả lời.
- **Sự kiện SSE:** `delta` (văn bản), `tool_status`, `citations` (server gom từ `nguon` của các tool đã gọi), `done` (kèm `message_id`), `error`.

### 5.5 Kênh Live: `WS /ws/live`
- **Bắt tay:** client gửi JSON `start { conversation_id?, consent: true, scope? }`. Server mở phiên Gemini Live với:
  - model `Live:Model`, dự phòng `Live:FallbackModel`;
  - system prompt và bộ tool dùng chung;
  - `input/output_audio_transcription`, `context_window_compression`, `session_resumption`;
  - giọng `Live:Voice`.

  Server nạp tối đa 6 lượt hội thoại gần nhất làm ngữ cảnh.
- **Client → server:** frame nhị phân PCM16 16 kHz mono (20–100 ms mỗi frame); JSON `stop`.
- **Server → client:**
  - frame nhị phân PCM16 24 kHz;
  - JSON `transcript_in`, `transcript_out`, `tool_status`, `citations`, `interrupted`, `turn_end`, `fallback_to_chat`, `error`.
- **Lệnh gọi tool:** gateway chuyển cho `ToolRegistry` rồi gửi `SendToolResponse`. Sau khi gửi kết quả tool, phải chờ lượt nói tiếp theo, không coi `turn_complete` của lượt gọi tool là kết thúc (bài học từ spike).
- **Giới hạn:**
  - mỗi phiên tối đa `Live:MaxSessionMinutes` (mặc định 10);
  - giới hạn số phiên đồng thời;
  - ngân sách số phút Live mỗi ngày `Live:DailyMinuteBudget`; vượt ngưỡng thì từ chối phiên mới bằng `fallback_to_chat`.
- **Độ trễ:** gateway ghi mốc thời gian (hết tiếng nói → lệnh gọi tool → âm thanh đầu tiên) vào `message.latency_ms`.

## 6. Widget và showcase

### 6.1 Widget (`packages/widget`)
- **Nhúng:** `<script src=".../advisor-widget.js" data-api="https://api..." defer></script>`. Render trong Shadow DOM.
- **Giao diện:**
  - nút nổi mở khung trợ lý;
  - hai tab **Chat** và **Nói**, dùng chung `conversation_id` (lưu ở `sessionStorage`);
  - câu hỏi gợi ý;
  - thẻ nguồn trích dẫn;
  - nút 👍/👎;
  - nút "Gặp cán bộ tư vấn", mở form họ tên và liên hệ, rồi gọi `POST /api/tickets`.
- **Âm thanh:**
  - `getUserMedia` → AudioWorklet → lấy mẫu lại xuống 16 kHz → gửi theo frame;
  - phát qua hàng đợi `AudioBufferSourceNode` ở 24 kHz;
  - khi có `interrupted` thì xóa hàng đợi.
- Lần đầu dùng giọng nói, widget hiện thông báo đồng ý: âm thanh được xử lý bởi Google Gemini, hệ thống không lưu bản ghi âm.
- Ưu tiên di động. Kiểm thử riêng trên iOS Safari: quyền micro, autoplay, và khi chuyển tab.

### 6.2 Showcase (`apps/showcase`)
- **Trang tuyển sinh mô phỏng:** trang chủ, danh sách ngành, chi tiết ngành. Dữ liệu lấy từ API công khai của release đang hoạt động: `GET /api/public/nganh`… Có nhúng widget. Chân trang ghi "Bản demo đề tài NCKH sinh viên, không phải website chính thức".
- **Trang giới thiệu đề tài:** mục tiêu, sơ đồ kiến trúc, công nghệ, nhóm thực hiện, GVHD.
- **Trang kết quả đánh giá:** biểu đồ đọc từ `eval/results/*.json`, được chép vào lúc build.

## 7. Admin (`apps/admin`)
- Đăng nhập.
- **Cơ sở tri thức:** các trang thêm/sửa/xóa dạng bảng kèm form cho từng thực thể ở mục 4.2; nhập Excel/CSV; tải tài liệu lên và xem trạng thái xử lý.
- **Xuất bản:** danh sách thay đổi → nhập ghi chú → xuất bản. Có lịch sử release và nút khôi phục.
- **Hỏi thử:** khung chat chạy với `scope=draft`, hiển thị cả các tool đã gọi.
- **Hội thoại:** danh sách có lọc (ngày, kênh, 👎, có `khong_tim_thay`); xem chi tiết gồm tool, kết quả và độ trễ; nút **"Tạo FAQ từ câu này"**.
- **Ticket:** danh sách, đổi trạng thái.
- **Dashboard:** hội thoại theo ngày, chủ đề hay hỏi, tỉ lệ chuyển cán bộ, tỉ lệ 👎, câu chưa trả lời được, số phút Live đã dùng.

## 8. Hội thoại, ticket, quyền riêng tư

| Bảng | Trường chính |
|---|---|
| `conversation` | `id`, `kenh_dau_tien`, `bat_dau`, `user_agent`, `release_id`, `dong_y_giong_noi` |
| `message` | `conversation_id`, `vai_tro`, `kenh`, `noi_dung`, `tool_calls jsonb`, `latency_ms jsonb`, `danh_gia` (-1/0/1), `tao_luc` |
| `ticket` | `conversation_id`, `ho_ten`, `lien_he`, `ly_do`, `trang_thai` (moi/dang_xu_ly/xong), `tao_luc` |

- Không lưu audio.
- Trước khi lưu `message.noi_dung`, số điện thoại và email bị che bằng regex. Thông tin liên hệ chỉ được lưu trong `ticket`.
- Hội thoại được giữ 12 tháng. Có job xóa hội thoại cũ hơn thời hạn này, thời hạn cấu hình được.

## 9. Xử lý lỗi

| Tình huống | Hành vi |
|---|---|
| Live: 429/503 hoặc không kết nối được | Thử lại một lần với model dự phòng. Vẫn lỗi thì gửi `fallback_to_chat`; widget chuyển sang tab Chat và giữ nguyên hội thoại |
| Chat: lỗi Gemini | Gửi sự kiện `error` kèm câu mặc định "Hệ thống đang bận…", gợi ý tạo ticket. Không bao giờ để trống câu trả lời |
| Tool timeout hoặc lỗi | `loi_he_thong`. Prompt yêu cầu xin lỗi và không đoán |
| Rớt WebSocket | Client kết nối lại với resumption handle, tối đa 3 lần, backoff 1/2/4 giây |
| Xử lý tài liệu lỗi | `that_bai` kèm `loi`; nút "Xử lý lại" |
| Release mới gây trả lời sai | Khôi phục release cũ; chạy bộ test hồi quy |
| Vượt ngân sách Live trong ngày | Từ chối phiên mới bằng `fallback_to_chat` |

## 10. Bảo mật và chi phí
- **Admin:** ASP.NET Identity + JWT (access token 60 phút). Khóa tài khoản sau 5 lần đăng nhập sai. Chỉ dùng HTTPS.
- **API công khai:**
  - rate limit (`System.Threading.RateLimiting`) theo IP và theo `conversation_id`;
  - CORS theo danh sách domain cho phép;
  - tin nhắn tối đa 1.000 ký tự;
  - giới hạn số phiên Live đồng thời.
- **Bí mật:** `GEMINI_API_KEY` và chuỗi kết nối CSDL lấy từ biến môi trường hoặc user-secrets. `.env` nằm trong `.gitignore`. Không commit bí mật.
- **Quyền của tool:** tool chỉ đọc. Riêng `chuyen_tiep_can_bo` được ghi vào `ticket`. Agent không có đường nào để ghi vào cơ sở tri thức, nên prompt injection không sửa được dữ liệu.
- **Chi phí:** đặt hạn mức chi tiêu trên Google Cloud, kết hợp `Live:DailyMinuteBudget`. Dev dùng gói miễn phí; thử nghiệm người dùng dùng gói trả phí.

## 11. Kiểm thử
- **Unit (xUnit):**
  - chuẩn hóa tên ngành và phương thức;
  - sinh `cach_doc` cho số và mã tổ hợp;
  - RRF;
  - so sánh danh sách thay đổi giữa bản nháp và release;
  - xuất bản và khôi phục;
  - che PII;
  - tạo `FunctionDeclaration` từ `ToolRegistry`.
- **Integration (Testcontainers `pgvector/pgvector:pg16`):**
  - CRUD và ràng buộc unique;
  - Agent chỉ thấy release đang hoạt động, còn `scope=draft` thấy bản nháp;
  - mọi tool chạy đúng trên dữ liệu seed mẫu;
  - nhập CSV;
  - pipeline xử lý tài liệu, dùng embedding giả.
- **Gemini:** test tự động dùng `IGeminiClient` giả. Một bộ smoke test nhỏ gọi Gemini thật, chạy thủ công hoặc theo lịch.
- **Frontend:**
  - Vitest cho logic: lấy mẫu lại âm thanh, parse sự kiện;
  - Playwright cho luồng admin (đăng nhập → sửa điểm chuẩn → hỏi thử → xuất bản) và widget chat.
- **CI:** GitHub Actions chạy build và test backend và frontend trên mỗi PR.

## 12. Đánh giá đề tài (`eval/`)
- **Bộ test:**
  - `questions.jsonl`: ≥ 300 câu, mỗi câu có chủ đề, loại, đáp án chuẩn, tool mong đợi, nguồn;
  - `audio/`: ≥ 200 mẫu giọng nói, ≥ 20 người, ba miền, yên tĩnh và có tạp âm, kèm transcript chuẩn.
- **Script:**
  - `run_text.py` gọi `/api/chat`;
  - `run_voice.py` phát audio qua `/ws/live`, tham khảo `prototype/gemini_live/live_probe.py`;
  - `score.py` tính các chỉ số: tỉ lệ đúng (chấm tay kết hợp LLM-as-judge, kiểm tra độ đồng thuận trên mẫu 20%), tỉ lệ gọi tool đúng, số câu sai số liệu, tỉ lệ từ chối đúng, WER/CER (`jiwer`), độ trễ p50/p95.
- **Đầu ra:** `eval/results/<ngày>_<cấu hình>.json`, để showcase vẽ biểu đồ và để đưa vào Chương 3.
- **So sánh cấu hình** (theo đề cương mục 11):
  - truy xuất: chỉ dense, hoặc hybrid RRF;
  - RAG thuần, hoặc RAG + tool;
  - hai model Gemini cho chat;
  - hai model Live;
  - WER theo vùng miền và độ ồn.
- **Hồi quy:** bộ con khoảng 50 câu, chạy mỗi lần sửa prompt hoặc trước khi xuất bản release lớn.
- **Thử nghiệm người dùng:** ≥ 30 người, phiếu SUS + CSAT, và phỏng vấn cán bộ tuyển sinh.

## 13. Cấu hình (`appsettings` / biến môi trường)

| Khóa | Mặc định |
|---|---|
| `Gemini:ApiKey` | (bí mật) |
| `Agent:ChatModel` | `gemini-3.5-flash` |
| `Agent:MaxToolRounds` | 5 |
| `Live:Model` / `Live:FallbackModel` | `gemini-3.8-live` / `gemini-3.1-flash-live-preview` |
| `Live:Voice` | `Kore` |
| `Live:MaxSessionMinutes` / `Live:DailyMinuteBudget` | 10 / 300 |
| `Embedding:Model` / `Embedding:Dimensions` | `gemini-embedding-001` / 768 |
| `Rag:FaqThreshold` / `Rag:TopK` | 0.85 / 5 |
| `Cors:AllowedOrigins` | domain showcase |

Tên model phải được kiểm tra lại với danh sách model của API trước mỗi giai đoạn, vì Live API thay đổi nhanh.

## 14. Thứ tự thực hiện (sẽ chi tiết hóa trong kế hoạch triển khai)

| Mốc | Nội dung | Hạn |
|---|---|---|
| 1 | Nền tảng: repo, docker-compose, CSDL, migration, auth, CI | giữa 11/2026 |
| 2 | KB CMS: CRUD, release, audit, nhập CSV + admin UI | đầu 12/2026 |
| 3 | Ingestion + RAG + ToolRegistry + kênh chat + Hỏi thử | cuối 12/2026 |
| 4 | Live gateway + widget (chat + nói) | cuối 01/2027 (trước Tết) |
| 5 | Hội thoại, ticket, dashboard; showcase | giữa 02/2027 |
| 6 | Bộ eval, thực nghiệm, thử nghiệm người dùng | 03/2027 |

## 15. Rủi ro còn mở
- Gemini Live hiện là bản preview, tên model hoặc tính năng có thể đổi. **Giảm thiểu:** cấu hình qua `appsettings`, có model dự phòng, có `fallback_to_chat`.
- Chất lượng nhận dạng giọng thật thuộc ba miền chưa được kiểm chứng; spike chỉ dùng audio do TTS tạo. **Giảm thiểu:** thu bộ audio thật sớm, ngay trong mốc 4.
- Chưa có xác nhận cho phép nhúng vào website thật của trường. **Giảm thiểu:** dùng showcase làm môi trường demo.
- Dữ liệu ban đầu do cán bộ nhập. Nếu cán bộ chậm nhập, nhóm tự nhập từ nguồn công khai qua admin và ghi rõ nguồn.
