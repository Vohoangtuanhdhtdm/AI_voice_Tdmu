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
6. Một số lỗi nhỏ trong đề cương nên sửa trước khi nộp: SĐT nhà riêng và di động của GVHD trùng với số tổng đài trường; dòng ký tên ghi “năm 201…”; mục “Chương trình đào tạo” còn bỏ trống; nên kiểm tra lại các ô chọn ở mục 2–3.

## 3. Kiến trúc đề xuất

```
[Trình duyệt / Widget]
   │  mic → VAD (phát hiện tiếng nói) ─┐            ┌─ phát audio (stream theo câu)
   │  ô nhập văn bản ──────────────────┤            │
   ▼                                   ▼            │
[Backend FastAPI + WebSocket] ── STT ──► AI Agent ──► TTS
                                          │
                     ┌────────────────────┼──────────────────────┐
                     ▼                    ▼                      ▼
              RAG (văn bản)        Tools (dữ liệu cấu trúc)   Chuyển tiếp cán bộ
        Vector DB + BM25 + rerank   PostgreSQL: điểm chuẩn,   (tạo ticket / để lại
        Đề án, thông báo, quy chế,  chỉ tiêu, tổ hợp, học phí  SĐT, email, Zalo OA)
        mô tả ngành, KTX, học bổng
                     └──────────── Trang quản trị: cập nhật KB, xem log, thống kê ────┘
```

**Công cụ (tool) dự kiến cho Agent**
- `tra_diem_chuan(nganh, nam, phuong_thuc)`
- `tra_chi_tieu(nganh, nam)`
- `tra_to_hop(nganh)` / `tim_nganh_theo_to_hop(to_hop)`
- `tra_hoc_phi(nganh, nam)`
- `tra_lich_tuyen_sinh(moc)`
- `tim_kiem_tai_lieu(cau_hoi)`: RAG
- `chuyen_tiep_can_bo(ly_do, lien_he)`

**Nguyên tắc trả lời:** chỉ trả lời dựa trên kết quả tool hoặc tài liệu truy xuất được, kèm trích nguồn. Nếu không đủ căn cứ thì nói rõ là chưa có thông tin và đề nghị chuyển tiếp cán bộ. Câu trả lời giọng nói phải ngắn (2–4 câu), còn bản văn bản hiển thị đầy đủ hơn và có liên kết nguồn.

## 4. Lựa chọn công nghệ (các phương án để thực nghiệm so sánh)

| Thành phần | Phương án chính | Phương án so sánh |
|---|---|---|
| LLM | Một API giá rẻ có tool calling tốt (dòng “mini/flash/haiku”) | Mô hình mở chạy local (vd. Qwen 7–8B qua Ollama/vLLM) |
| Embedding | `bge-m3` (đa ngữ, hỗ trợ tiếng Việt tốt) | `multilingual-e5`, bi-encoder tiếng Việt (bkai) |
| Truy xuất | Hybrid BM25 + dense + reranker (`bge-reranker-v2-m3`) | Chỉ dense; chunk cố định vs. chunk theo tiêu đề/mục |
| Vector DB | Qdrant hoặc pgvector (dùng chung PostgreSQL) | Chroma |
| STT | PhoWhisper (VinAI, Whisper fine-tune tiếng Việt) qua faster-whisper | Whisper large-v3/turbo · API thương mại (FPT.AI / Viettel AI / Google) · Web Speech API làm baseline |
| TTS | edge-tts (giọng vi-VN neural, miễn phí) để làm prototype | VietTTS/F5-TTS tiếng Việt (tự host) · FPT.AI/Viettel TTS |
| VAD | Silero VAD | WebRTC VAD |
| Agent | Tự viết vòng lặp function-calling hoặc LangGraph | — |
| Backend / Frontend | FastAPI, WebSocket · Widget JS nhúng 1 dòng `<script>` · Admin: React/Next.js | — |
| Triển khai | Docker Compose trên VPS | Máy chủ của Viện CNS (nếu được cấp) |

**Mục tiêu hiệu năng (đề xuất để đưa vào yêu cầu phi chức năng)**
- Độ trễ từ lúc ngừng nói đến lúc nghe tiếng trả lời đầu tiên: p50 ≤ 2,5 s, p95 ≤ 4 s.
- Tỉ lệ trả lời đúng ≥ 85% trên bộ test, sai số liệu ≈ 0% đối với câu hỏi dùng tool.
- Từ chối đúng ≥ 90% với câu ngoài phạm vi.
- WER < 15% với giọng ba miền trong môi trường yên tĩnh.

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
- **Song song (Tuấn hoặc người phụ trách giọng nói):** spike STT/TTS. Đo WER và độ trễ của 2–3 phương án STT trên 30 file thu thử, chọn phương án chính.
- ✅ Mốc M2 (≈ 10/01): Agent văn bản chạy được, đạt ≥ 75% đúng trên bộ test v1, trang quản trị bản cơ bản.

### Giai đoạn 3: Tích hợp giọng nói + widget website (11/01 – 31/01/2027, hoàn thiện sau Tết)
- Luồng streaming đầy đủ: mic, VAD, STT, Agent (stream), tách câu, TTS, phát audio. Hỗ trợ ngắt lời (barge-in) và nút “nhấn để nói” làm phương án dự phòng.
- Widget nhúng: chế độ chat + voice, giao diện tốt trên di động, hiển thị phụ đề và nguồn, nút “gặp cán bộ tư vấn”.
- Chuẩn hóa văn bản trước khi đưa vào TTS: đọc số, điểm “24,5”, mã tổ hợp “A00”, viết tắt “CNTT”, “KTX”.
- Thu **bộ test giọng nói:** ≥ 20 người nói thuộc ba miền, ≥ 200 câu, có cả trường hợp yên tĩnh và có tạp âm.
- 01/02 – 15/02: **nghỉ Tết / dự phòng**.
- 16/02 – 28/02: sửa lỗi, tối ưu độ trễ, triển khai lên VPS/staging, nhúng thử vào một trang demo mô phỏng website tuyển sinh.
- ✅ Mốc M3 (≈ 28/02): website có trợ lý giọng nói chạy end-to-end trên môi trường thật.

### Giai đoạn 4: Thực nghiệm, đánh giá, báo cáo (01/03 – 31/03/2027)
- **Thực nghiệm so sánh** (bảng kết quả cho Chương 3):
  1. LLM: A vs. B
  2. Chunking: cố định vs. theo cấu trúc
  3. Retrieval: dense vs. hybrid vs. hybrid + rerank
  4. RAG thuần vs. RAG + tools (cho câu hỏi số liệu)
  5. STT: PhoWhisper vs. Whisper vs. API, phân theo miền và độ ồn
- **Chỉ số:**
  - Tỉ lệ trả lời đúng (chấm tay kết hợp LLM-as-judge, có kiểm tra độ đồng thuận)
  - Faithfulness / context precision (RAGAS)
  - Tỉ lệ từ chối đúng
  - WER/CER
  - Độ trễ p50/p95 của từng chặng
- **Thử nghiệm người dùng:** ≥ 30 người (học sinh, sinh viên năm nhất, phụ huynh), dùng phiếu SUS + CSAT và câu hỏi mở.
- Viết báo cáo tổng kết theo cấu trúc Mở đầu → Chương 1–3 → Kết luận, HDSD, bản thảo bài báo (có thể nhắm hội nghị hoặc tạp chí trong nước).
- ✅ Mốc M4 (31/03): nộp báo cáo, sản phẩm, mã nguồn, bộ dữ liệu.

### Tóm tắt dạng Gantt

| Việc | T11 | T12 | T1 | T2 | T3 |
|---|---|---|---|---|---|
| Khảo sát, FAQ, SRS (bù T10) | ██ | | | | |
| PTTK + cơ sở tri thức + bộ test | ████ | █ | | | |
| Agent văn bản (RAG + tools) | | ████ | █ | | |
| Trang quản trị | | ██ | ██ | | |
| Spike STT/TTS | | ██ | | | |
| Tích hợp giọng nói + widget | | | ████ | ░██ | |
| Thu bộ test giọng nói | | | ██ | | |
| Thực nghiệm + user test | | | | █ | ████ |
| Viết báo cáo / bài báo | (viết dần Ch.1) | | (Ch.2) | | ████ |

(░ = nghỉ Tết)

## 6. Phân công đề xuất (nhóm điều chỉnh theo thế mạnh)

| Mảng | Phụ trách chính | Hỗ trợ |
|---|---|---|
| Quản lý tiến độ, làm việc với GVHD và phòng tuyển sinh | Nguyên (nhóm trưởng) | Tuấn |
| Khảo sát, thu thập và chuẩn hóa dữ liệu, bộ test | Nguyên | Tuấn |
| Kiến trúc, backend, Agent, RAG, tools, triển khai | Tuấn | Nguyên |
| STT/TTS, streaming giọng nói | Tuấn | Nguyên |
| Widget website, trang quản trị (frontend) | Nguyên | Tuấn |
| Thực nghiệm, đánh giá, thử nghiệm người dùng | Cả nhóm | |
| Báo cáo: Ch.1 + khảo sát (Nguyên) · Ch.2–3 kỹ thuật (Tuấn) · bài báo (cả nhóm) | | |

**Nhịp làm việc:** sprint 1 tuần, họp nhóm 15 phút vào thứ Hai, demo cho GVHD 2 tuần/lần. Quản lý việc bằng GitHub Issues/Projects, mỗi tính năng làm trên một nhánh và merge qua PR.

## 7. Dự toán kinh phí (tham khảo, cần đối chiếu quy định chi NCKH SV của trường)

| Khoản | Dự kiến (đ) |
|---|---|
| API LLM / STT / TTS cho phát triển và thực nghiệm | 1.200.000 |
| VPS/cloud để triển khai thử nghiệm (~3 tháng) | 1.200.000 |
| Khảo sát, thu âm, thử nghiệm người dùng (quà/hỗ trợ người tham gia) | 1.300.000 |
| Tên miền, chứng chỉ, linh tinh kỹ thuật | 290.000 |
| In ấn, đóng quyển báo cáo, văn phòng phẩm | 1.000.000 |
| **Tổng** | **4.990.000** |

## 8. Rủi ro và phương án xử lý

| Rủi ro | Mức | Phương án |
|---|---|---|
| LLM bịa số liệu | Cao | Số liệu chỉ lấy qua tool SQL; bắt buộc trích nguồn; có kiểm thử hồi quy trên bộ test |
| Độ trễ giọng nói cao | Cao | Stream mọi chặng, TTS theo câu, câu trả lời ngắn, ưu tiên model nhỏ; có chế độ “nhấn để nói” |
| WER cao với giọng địa phương hoặc tạp âm | TB | Dùng PhoWhisper, gửi prompt/hotword gồm tên ngành và mã tổ hợp, hậu xử lý chuẩn hóa tên ngành (fuzzy match) |
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
