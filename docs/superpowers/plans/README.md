# Lộ trình kế hoạch triển khai

Spec: `docs/superpowers/specs/2026-11-05-tdmu-voice-advisor-design.md`

Mỗi mốc trong spec (mục 14) có một kế hoạch riêng. Kế hoạch nào xong cũng phải tạo ra phần mềm chạy được và có test. Kế hoạch của mốc sau chỉ được viết khi mốc trước đã merge, để kế hoạch dựa trên code thật.

| # | Kế hoạch | Kết quả | Hạn | Trạng thái |
|---|---|---|---|---|
| 1 | [Nền tảng + KB CMS backend](2026-11-05-p1-foundation-kb-backend.md) | API .NET 8 + PostgreSQL: auth, CRUD tri thức, audit, release, nhập CSV/Excel, API công khai | 06/12/2026 | Hoàn thành (05/11/2026) |
| 2 | Admin UI (React TS + Ant Design) | Trang quản trị cho toàn bộ API của kế hoạch 1 | 13/12/2026 | Chưa viết |
| 3 | Ingestion + RAG + ToolRegistry + kênh chat + Hỏi thử | `/api/chat` (SSE) trả lời có tool và trích dẫn | 31/12/2026 | Chưa viết |
| 4 | Live gateway + widget | `/ws/live` + `advisor-widget.js` (chat + nói) | 31/01/2027 | Chưa viết |
| 5 | Hội thoại, ticket, dashboard + showcase | Log, ticket, thống kê, trang demo | 15/02/2027 | Chưa viết |
| 6 | Bộ đánh giá `eval/` | Script chấm điểm, kết quả JSON cho Chương 3 | 15/03/2027 | Chưa viết |
