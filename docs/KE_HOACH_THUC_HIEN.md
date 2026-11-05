# Kế hoạch thực hiện đề tài

**Đề tài:** Nghiên cứu ứng dụng AI Agent tương tác bằng giọng nói trong hỗ trợ tư vấn tuyển sinh trên website Trường Đại học Thủ Dầu Một
**Thời gian:** 10/2026 – 03/2027 (6 tháng) · **Kinh phí:** 4.990.000 đ · **GVHD:** ThS. Trần Bá Minh Sơn
**Nhóm:** Nguyễn Đình Khôi Nguyên (nhóm trưởng, D25CNTT03) · Võ Hoàng Tuấn (D22KTPM03)

> Bản đề xuất ngày 04/11/2026. Kế hoạch bám theo mục 12.2 của đề cương nhưng chia nhỏ theo tuần và lập lại mốc từ thời điểm hiện tại.

---

## 1. Tóm tắt đề cương

| Hạng mục | Nội dung |
|---|---|
| Bài toán | Câu hỏi tuyển sinh lặp lại, dồn vào mùa cao điểm, đến từ nhiều kênh → bộ phận tư vấn quá tải, thông tin trên website rời rạc |
| Giải pháp | AI Agent = LLM + RAG + tool calling, có STT/TTS tiếng Việt, nhúng vào website, có trang quản trị |
| 5 mục tiêu cụ thể | (1) Cơ sở tri thức tuyển sinh · (2) Kiến trúc Agent RAG + tool · (3) Luồng hội thoại giọng nói + kênh văn bản · (4) Widget website + trang quản trị · (5) Đánh giá: độ chính xác, WER, thời gian phản hồi, mức hài lòng |
| Thách thức đề cương nêu | Chống “bịa” thông tin · giọng vùng miền · độ trễ · chuyển tiếp cán bộ tư vấn |
| Sản phẩm | Prototype + trang quản trị, bộ dữ liệu Q&A kiểm thử, tài liệu PTTK, HDSD, báo cáo tổng kết, bản thảo bài báo |

## 2. Những điểm cần lưu ý trước khi triển khai

1. **Tháng 10/2026 đã qua.** Cần rà lại sản phẩm tháng 10 (báo cáo khảo sát, bộ FAQ, đặc tả yêu cầu). Phần nào chưa xong thì làm bù trong 2 tuần đầu tháng 11 (Giai đoạn 0).
2. **Tết Nguyên đán 2027 rơi vào khoảng 06/02/2027.** Đề cương xếp toàn bộ phần giọng nói vào tháng 02, đúng lúc nghỉ Tết, nên rất rủi ro. Đề xuất: **làm thử nghiệm kỹ thuật (spike) giọng nói từ tháng 12** và xong phần tích hợp chính **trước 31/01**. Tháng 02 chỉ để hoàn thiện.
3. **Dữ liệu số liệu (điểm chuẩn, chỉ tiêu, học phí) không nên để LLM “nhớ” qua RAG.** Nên lưu thành bảng có cấu trúc và cho Agent gọi qua tool. Đây là cách chống bịa số liệu hiệu quả nhất, và cũng là điểm đóng góp rõ ràng cho mục tiêu (2).
4. **Đề án tuyển sinh 2027 thường công bố khoảng tháng 3–4.** Lúc nghiệm thu, dữ liệu có thể vẫn là của năm 2026. Vì vậy trang quản trị cần cho phép cập nhật dữ liệu theo năm, và mọi câu trả lời phải ghi rõ nguồn và năm áp dụng.
5. **Kinh phí thấp (≈5 triệu).** Ưu tiên mô hình mã nguồn mở hoặc API giá rẻ/miễn phí, cùng một VPS nhỏ (xem mục 6).
6. **Định vị đề tài: nghiên cứu Ứng dụng (loại C), dùng Gemini Live.** Trọng tâm là ứng dụng công nghệ sẵn có để giải quyết bài toán thực tế của trường, không phát triển mô hình mới. Vì vậy kênh giọng nói dùng nền tảng Gemini Live API (speech-to-speech) thay vì tự ghép STT → LLM → TTS. Phần việc của nhóm tập trung vào:
   - Cơ sở tri thức và công cụ tra cứu bám dữ liệu chính thức.
   - Tích hợp vào website và trang quản trị.
   - Thử nghiệm với người dùng thật và đánh giá hiệu quả ứng dụng.

   Phần kết quả cần có sản phẩm chạy được, đánh giá được và có khả năng chuyển giao. Không đặt nặng tính mới về thuật toán.

   Cần làm trên đề cương:
   - Ở mục 2, đánh dấu ô **“Ứng dụng”**.
   - Cập nhật câu chữ cho khớp:
   - Mục tiêu (3) sửa thành “tích hợp mô hình tương tác giọng nói thời gian thực (Gemini Live) và so sánh với kiến trúc nhận dạng – tổng hợp tiếng nói truyền thống”.
   - Chương 1 bổ sung lý thuyết về mô hình speech-to-speech / native audio.
7. Một số lỗi nhỏ trong đề cương nên sửa trước khi nộp: SĐT nhà riêng và di động của GVHD trùng với số tổng đài trường; dòng ký tên ghi “năm 201…”; mục “Chương trình đào tạo” còn bỏ trống; nên kiểm tra lại các ô chọn ở mục 2–3.

## 3. Kiến trúc đề xuất: lai Gemini Live + Agent có công cụ

Hệ thống dùng chung **một bộ công cụ (tools) và một cơ sở tri thức** cho hai kênh:

- **Kênh giọng nói dùng Gemini Live API (mô hình native audio).** Mô hình nghe và nói trực tiếp. Phát hiện tiếng nói (VAD), ngắt lời (barge-in) và giọng đọc đã có sẵn. Mô hình gọi tool của nhóm qua function calling.
- **Kênh văn bản dùng Gemini API thường** (dòng Flash), với cùng system prompt và cùng tools, để hai kênh trả lời nhất quán.
- *(Tùy chọn, chỉ làm nếu dư thời gian)* Pipeline cascade STT → LLM → TTS làm mốc tham chiếu. Với đề tài ứng dụng, phần này **không bắt buộc**.

```
[Widget trên website]
  mic (PCM 16 kHz) ⇄ loa (PCM 24 kHz) · ô chat · phụ đề · nguồn trích dẫn
        │ WebSocket
        ▼
[Backend FastAPI: proxy + điều phối]  (giữ API key, ghi log, thực thi tool)
  ├── Phiên Live: google-genai SDK ⇄ Gemini Live API
  │      cấu hình: system_instruction, tools, voice, input/output_audio_transcription,
  │                context_window_compression, session_resumption
  ├── Kênh chat: Gemini API (generateContent, có stream) với cùng tools
  └── Thực thi tool khi mô hình gửi toolCall:
        ├── tim_kiem_tai_lieu → RAG: hybrid BM25 + vector + rerank (đề án, quy chế, KTX, học bổng…)
        ├── tra_diem_chuan / tra_chi_tieu / tra_to_hop / tra_hoc_phi / tra_lich_tuyen_sinh → PostgreSQL
        └── chuyen_tiep_can_bo → tạo ticket để cán bộ gọi lại
[Trang quản trị] cập nhật dữ liệu theo năm · xem transcript hội thoại · ticket · thống kê chủ đề
```

**Vì sao backend làm proxy, không cho trình duyệt nối thẳng tới Gemini bằng ephemeral token?**
- Tool, cơ sở dữ liệu và log đều nằm ở server, nên không lộ logic hay dữ liệu ra trình duyệt.
- Backend nhận được transcript cả hai chiều để lưu lịch sử cho trang quản trị, đúng yêu cầu ở mục tiêu (4).
- Đổi lại độ trễ tăng thêm một chặng mạng. Nếu cần tối ưu, có thể chuyển sang cách trình duyệt kết nối trực tiếp bằng ephemeral token, còn tool gọi về backend qua HTTP. Đây có thể là một thực nghiệm phụ.

**Nguyên tắc chống bịa thông tin (đưa vào system instruction và kiểm thử)**
- Bắt buộc gọi tool trước khi nêu bất kỳ số liệu nào (điểm, chỉ tiêu, học phí, ngày tháng). Không có kết quả tool thì không được nêu số.
- **Tắt Google Search grounding.** Chỉ dùng nguồn chính thức của trường.
- Câu ngoài phạm vi thì từ chối lịch sự và đề nghị chuyển cán bộ.
- Câu nói ngắn (2–4 câu). Chi tiết và link nguồn hiển thị dưới dạng văn bản trên widget.
- Với tool chậm như RAG, cho mô hình nói câu đệm kiểu “Mình đang tra cứu…”. Nếu phiên bản API hỗ trợ, dùng function calling bất đồng bộ (non-blocking).
- Bộ test đo thêm chỉ số **“tỉ lệ gọi tool đúng”**: với câu hỏi số liệu, mô hình có gọi tool hay tự trả lời.

**Vấn đề riêng của Gemini Live cần xử lý**

| Vấn đề | Cách xử lý |
|---|---|
| Giới hạn thời lượng phiên (audio thường ~15 phút) và context window | Bật context_window_compression + session_resumption; tư vấn tuyển sinh thường ngắn |
| Đọc sai mã tổ hợp “A00”, “D01”, tên ngành viết tắt, điểm “24,5” | Tool trả về cả trường `cach_doc` (vd. “A không không”); hướng dẫn trong prompt; đưa vào bộ test phát âm |
| Nhận dạng tên ngành sai | Tool có fuzzy match tên ngành / từ đồng nghĩa (“IT”, “công nghệ thông tin”, “CNTT”) |
| Âm thanh gửi lên máy chủ Google | Thông báo và xin đồng ý trên widget; dùng gói trả phí (điều khoản dữ liệu chặt hơn gói miễn phí); không lưu audio, chỉ lưu transcript đã ẩn danh |
| Tên model và tính năng Live API thay đổi nhanh (preview) | Gom cấu hình model vào biến môi trường; kiểm tra lại tài liệu chính thức trước mỗi giai đoạn |
| Phụ thuộc một nhà cung cấp | Gom cấu hình model vào một chỗ; nếu Live API gặp sự cố thì widget tự chuyển sang kênh văn bản |

**Công cụ (tool) dự kiến:**
- `tra_diem_chuan(nganh, nam, phuong_thuc)`
- `tra_chi_tieu(nganh, nam)`
- `tra_to_hop(nganh)` / `tim_nganh_theo_to_hop(to_hop)`
- `tra_hoc_phi(nganh, nam)`
- `tra_lich_tuyen_sinh(moc)`
- `tim_kiem_tai_lieu(cau_hoi)`
- `chuyen_tiep_can_bo(ly_do, lien_he)`

## 4. Lựa chọn công nghệ

| Thành phần | Triển khai chính | Dùng để so sánh trong thực nghiệm |
|---|---|---|
| Kênh giọng nói | **Gemini Live API**, model native audio (vd. dòng `gemini-2.5-flash-native-audio-*`, cần kiểm tra tên model mới nhất) | Các cấu hình trong Gemini Live: model, giọng đọc, độ nhạy VAD · *(tùy chọn)* cascade PhoWhisper → LLM → TTS |
| Kênh văn bản | Gemini Flash (generateContent + function calling) | Một LLM khác hoặc mô hình mở (Qwen) |
| SDK | `google-genai` (Python) phía backend | — |
| Embedding | `bge-m3` hoặc Gemini embedding | `multilingual-e5`, bi-encoder tiếng Việt |
| Truy xuất | Hybrid BM25 + dense + `bge-reranker-v2-m3` | Chỉ dense; chunk cố định vs. theo cấu trúc |
| CSDL | PostgreSQL + pgvector | Qdrant |
| Backend / Frontend | FastAPI + WebSocket · widget JS (AudioWorklet thu/phát PCM) · Admin React/Next.js | — |
| Triển khai | Docker Compose trên VPS gần Google (Singapore) | — |

**Mục tiêu hiệu năng (đề xuất)**
- Độ trễ từ lúc ngừng nói đến lúc nghe âm thanh đầu tiên: p50 ≤ 1,5 s, p95 ≤ 3 s.
- Tỉ lệ trả lời đúng ≥ 85%. Sai số liệu ≈ 0% với câu hỏi cần tool, và tỉ lệ gọi tool đúng ≥ 95%.
- WER của transcript đầu vào < 15% với giọng ba miền.

## 5. Kế hoạch theo giai đoạn (04/11/2026 → 31/03/2027)

### Giai đoạn 0: Hoàn tất phần việc tháng 10 (04/11 – 15/11)
- Phát phiếu khảo sát (Google Form) cho học sinh lớp 12, sinh viên năm nhất và phụ huynh, mục tiêu ≥ 100 phiếu. Phỏng vấn 2–3 cán bộ tuyển sinh.
- Thu thập nguồn chính thức: đề án tuyển sinh 2024–2026, thông báo, danh mục ngành, học phí, học bổng, KTX, điểm chuẩn nhiều năm, fanpage/FAQ.
- Tổng hợp **bộ FAQ v1** (≥ 150 câu, gắn nhãn theo 9 nhóm chủ đề trong phạm vi đề tài) và **SRS v1**.
- ✅ Mốc M0: báo cáo khảo sát, FAQ v1, SRS v1. Họp GVHD.

### Giai đoạn 1: Phân tích, thiết kế, cơ sở tri thức (16/11 – 06/12)
- Use case, sơ đồ kiến trúc, sơ đồ tuần tự cho luồng giọng nói, ERD (ngành, điểm chuẩn, chỉ tiêu, tổ hợp, học phí, hội thoại, ticket, tài liệu).
- Làm sạch tài liệu (PDF → Markdown), chuẩn hóa bảng số liệu sang CSV và nhập vào PostgreSQL.
- Viết kịch bản hội thoại: chào hỏi, hỏi lại khi thiếu thông tin (ví dụ thiếu tên ngành), từ chối câu ngoài phạm vi, chuyển tiếp cán bộ.
- **Bộ test v1:** ≥ 300 câu hỏi văn bản có đáp án chuẩn và nguồn, gồm cả câu khẩu ngữ, viết tắt, không dấu và câu ngoài phạm vi.
- Dựng repo: cấu trúc thư mục, Docker Compose, CI chạy lint/test.
- ✅ Mốc M1: tài liệu PTTK, cơ sở tri thức v1, bộ test v1.

### Giai đoạn 2: AI Agent văn bản + quản trị + spike giọng nói (07/12/2026 – 10/01/2027)
- Tuần 1–2: pipeline RAG (ingest, chunk, embed, hybrid search, rerank), các tool SQL, vòng lặp Agent, prompt hệ thống và trích nguồn.
- Tuần 3: API chat có stream, lưu lịch sử hội thoại, script đánh giá tự động trên bộ test (chạy được nhiều cấu hình).
- Tuần 4–5: trang quản trị (đăng nhập, CRUD dữ liệu cấu trúc, tải tài liệu lên rồi tự re-index, xem log hội thoại, danh sách ticket chuyển tiếp, thống kê chủ đề).
- **Song song, từ tuần 1: prototype Gemini Live.**
  - Script Python kết nối Live API, nói tiếng Việt và gọi được 1–2 tool giả lập.
  - Đánh giá chất lượng giọng, các giọng có sẵn, độ trễ, transcript.
  - Thử các câu khó: mã tổ hợp, số thập phân, tên ngành.
  - Ước tính chi phí cho mỗi phút hội thoại.
- ✅ Mốc M2 (≈ 10/01):
  - Agent văn bản đạt ≥ 75% đúng trên bộ test v1.
  - Trang quản trị bản cơ bản.
  - Báo cáo spike Gemini Live: kết luận khả thi, chọn model và giọng.

### Giai đoạn 3: Tích hợp giọng nói + widget website (11/01 – 31/01/2027, hoàn thiện sau Tết)
- Backend proxy Live:
  - Chuyển tiếp audio hai chiều qua WebSocket.
  - Xử lý `toolCall` / `toolResponse`, xử lý ngắt lời (`interrupted`).
  - Lưu transcript input/output vào lịch sử hội thoại.
  - Session resumption khi rớt mạng.
- Widget nhúng:
  - Thu mic bằng AudioWorklet (PCM 16 kHz), phát PCM 24 kHz.
  - Chế độ chat + voice, phụ đề theo thời gian thực, hiển thị thẻ nguồn trích dẫn.
  - Nút “gặp cán bộ tư vấn”, giao diện tốt trên di động (iOS Safari cần kiểm tra kỹ quyền mic và autoplay).
- Tinh chỉnh system instruction cho phong cách nói và cách đọc số, mã tổ hợp, viết tắt (“CNTT”, “KTX”).
- Thu **bộ test giọng nói:** ≥ 20 người nói thuộc ba miền, ≥ 200 câu, có cả trường hợp yên tĩnh và có tạp âm.
- 01/02 – 15/02: **nghỉ Tết / dự phòng**.
- 16/02 – 28/02: sửa lỗi, tối ưu độ trễ, triển khai lên VPS/staging, nhúng thử vào một trang demo mô phỏng website tuyển sinh.
- Làm việc với phòng tuyển sinh / Viện CNS về phương án nhúng thử lên website thật (hoặc subdomain), quy trình cập nhật dữ liệu và người tiếp nhận ticket.
- ✅ Mốc M3 (≈ 28/02): website có trợ lý giọng nói chạy end-to-end trên môi trường thật.

### Giai đoạn 4: Thử nghiệm ứng dụng, đánh giá, báo cáo (01/03 – 31/03/2027)
Với đề tài ứng dụng, trọng tâm của giai đoạn này là **chứng minh hệ thống dùng được và có ích**. So sánh cấu hình chỉ làm ở mức vừa đủ, đúng như mục 11 của đề cương.
- **Thử nghiệm triển khai (pilot), 2–3 tuần:**
  - Mở cho nhóm người dùng thật dùng thử: học sinh THPT ở 1–2 trường, sinh viên năm nhất, phụ huynh.
  - Nhờ cán bộ tuyển sinh dùng trang quản trị để cập nhật dữ liệu và xử lý ticket.
  - Thu log thật để phân tích: chủ đề hay hỏi, tỉ lệ chuyển tiếp, câu chưa trả lời được.
- **So sánh cấu hình ở mức vừa đủ** (bảng kết quả cho Chương 3):
  1. Truy xuất dữ liệu: dense vs. hybrid + rerank
  2. RAG thuần vs. RAG + tools (cho câu hỏi số liệu)
  3. Hai model Gemini cho kênh văn bản, hoặc hai cấu hình của Live (model, giọng đọc)
  4. Độ chính xác nhận dạng giọng nói theo miền và độ ồn, đo trên transcript của Gemini Live
  5. *(Tùy chọn)* So với pipeline cascade tự ghép
- **Chỉ số:**
  - Tỉ lệ trả lời đúng (chấm tay kết hợp LLM-as-judge, có kiểm tra độ đồng thuận)
  - Faithfulness / context precision (RAGAS)
  - Tỉ lệ từ chối đúng
  - Tỉ lệ gọi tool đúng
  - WER/CER
  - Độ trễ p50/p95 của từng chặng
- **Thử nghiệm người dùng:** ≥ 30 người (học sinh, sinh viên năm nhất, phụ huynh), dùng phiếu SUS + CSAT và câu hỏi mở.
- **Đánh giá hiệu quả ứng dụng:** ước tính số câu hỏi hệ thống tự giải quyết được, so với số câu cần chuyển cán bộ. Phỏng vấn cán bộ tuyển sinh về mức độ giảm tải và tính dễ cập nhật dữ liệu.
- Viết báo cáo tổng kết theo cấu trúc Mở đầu → Chương 1–3 → Kết luận. Kèm HDSD và tài liệu triển khai/bàn giao cho nhà trường.
- Bản thảo bài báo dạng ứng dụng / báo cáo kinh nghiệm triển khai, có thể gửi hội nghị sinh viên NCKH hoặc kỷ yếu trong nước.
- ✅ Mốc M4 (31/03): nộp báo cáo, sản phẩm, mã nguồn, bộ dữ liệu.

### Tóm tắt dạng Gantt

| Việc | T11 | T12 | T1 | T2 | T3 |
|---|---|---|---|---|---|
| Khảo sát, FAQ, SRS (bù T10) | ██ | | | | |
| PTTK + cơ sở tri thức + bộ test | ████ | █ | | | |
| Agent văn bản (RAG + tools) | | ████ | █ | | |
| Trang quản trị | | ██ | ██ | | |
| Prototype Gemini Live | | ██ | | | |
| Proxy Live + widget giọng nói | | | ████ | ░██ | |
| Thu bộ test giọng nói | | | ██ | | |
| Pilot với người dùng + đánh giá | | | | █ | ████ |
| Viết báo cáo / bài báo | (viết dần Ch.1) | | (Ch.2) | | ████ |

(░ = nghỉ Tết)

## 6. Phân công đề xuất (nhóm điều chỉnh theo thế mạnh)

| Mảng | Phụ trách chính | Hỗ trợ |
|---|---|---|
| Quản lý tiến độ, làm việc với GVHD và phòng tuyển sinh | Nguyên (nhóm trưởng) | Tuấn |
| Khảo sát, thu thập và chuẩn hóa dữ liệu, bộ test | Nguyên | Tuấn |
| Kiến trúc, backend, Agent, RAG, tools, triển khai | Tuấn | Nguyên |
| Tích hợp Gemini Live, streaming audio | Tuấn | Nguyên |
| Widget website, trang quản trị (frontend) | Nguyên | Tuấn |
| Thực nghiệm, đánh giá, thử nghiệm người dùng | Cả nhóm | |
| Báo cáo: Ch.1 + khảo sát (Nguyên) · Ch.2–3 kỹ thuật (Tuấn) · bài báo (cả nhóm) | | |

**Nhịp làm việc:** sprint 1 tuần, họp nhóm 15 phút vào thứ Hai, demo cho GVHD 2 tuần/lần. Quản lý việc bằng GitHub Issues/Projects, mỗi tính năng làm trên một nhánh và merge qua PR.

## 7. Dự toán kinh phí (tham khảo, cần đối chiếu quy định chi NCKH SV của trường)

| Khoản | Dự kiến (đ) |
|---|---|
| Gemini API: Live (audio) + Flash (text) + embedding, giai đoạn thử nghiệm/user test dùng gói trả phí, có đặt hạn mức | 1.500.000 |
| VPS để triển khai thử nghiệm (~3 tháng) | 900.000 |
| Khảo sát, thu âm, thử nghiệm người dùng (quà/hỗ trợ người tham gia) | 1.300.000 |
| Tên miền, chứng chỉ, linh tinh kỹ thuật | 290.000 |
| In ấn, đóng quyển báo cáo, văn phòng phẩm | 1.000.000 |
| **Tổng** | **4.990.000** |

## 8. Rủi ro và phương án xử lý

| Rủi ro | Mức | Phương án |
|---|---|---|
| LLM bịa số liệu | Cao | Số liệu chỉ lấy qua tool SQL; bắt buộc trích nguồn; có kiểm thử hồi quy trên bộ test |
| Gemini Live tự trả lời mà không gọi tool | Cao | Prompt bắt buộc; đo tỉ lệ gọi tool; câu hỏi số liệu chỉ hiển thị con số lấy từ tool trên widget |
| Live API là bản preview, đổi tên model hoặc tính năng | TB | Cấu hình qua biến môi trường; lớp `VoiceProvider`; theo dõi changelog |
| Chi phí audio Live vượt dự kiến | TB | Đặt hạn mức trong Google Cloud; giới hạn thời lượng phiên trên widget; dùng gói miễn phí cho dev |
| Nhận dạng sai giọng địa phương hoặc tên ngành | TB | Fuzzy match tên ngành trong tool; hỏi lại xác nhận; dùng bộ test ba miền |
| Không lấy được dữ liệu nội bộ hay không được nhúng vào website thật | TB | Chỉ dùng dữ liệu công khai; demo trên trang mô phỏng; xin văn bản hỗ trợ qua GVHD |
| Tiến độ trễ do Tết và lịch thi học kỳ | Cao | Dồn việc nặng trước 31/01; tháng 02 để dự phòng |
| Vượt kinh phí API | Thấp | Đặt hạn mức chi, cache câu trả lời cho FAQ phổ biến, chạy thực nghiệm theo batch |
| Bảo mật / dữ liệu cá nhân (log giọng nói, SĐT) | TB | Có thông báo đồng ý; không lưu audio mặc định; ẩn danh hóa log |

## 9. Danh mục sản phẩm bàn giao

1. Mã nguồn (GitHub) + Docker Compose + README triển khai
2. Cơ sở tri thức tuyển sinh (tài liệu đã chuẩn hóa + CSDL số liệu)
3. Bộ dữ liệu kiểm thử: ≥ 300 Q&A văn bản, ≥ 200 mẫu giọng nói, kèm script đánh giá
4. Tài liệu SRS, PTTK, HDSD (người dùng và quản trị)
5. Báo cáo tổng kết + bản thảo bài báo + video demo
6. Tài liệu triển khai và bàn giao (cài đặt, cập nhật dữ liệu theo mùa tuyển sinh, vận hành, chi phí API dự kiến)
