# Việc tồn đọng sau Kế hoạch 1

Danh sách này tổng hợp từ ledger thực thi kế hoạch 1 (review từng task và review cuối). Các kế hoạch sau phải xử lý trước khi dùng tới phần liên quan.

## Phải sửa ở đầu Kế hoạch 2
- `moc-thoi-gian`: gửi `DateTimeOffset` có offset khác UTC (vd. `+07:00`) bị lỗi 500, vì Npgsql chỉ nhận UTC. Sửa bằng cách chuẩn hóa `ToUniversalTime()` trong Apply, kèm test.

## Trước khi triển khai thật (deploy)
- Forwarded headers cho rate limit khi chạy sau reverse proxy. Nếu không có, mọi client dùng chung một bucket 60/phút. Cần thêm test cho trường hợp trả 429.
- Rate limit cho `/api/auth/login`; HTTPS redirect và HSTS.
- Hiện app chỉ tự migrate ở Development/Testing. Ở môi trường khác, CSDL phải được migrate bằng `database update` hoặc migration bundle (xem `backend/README.md`).

## Cho Kế hoạch 3 (RAG/Agent)
- Tool trả về số liệu phải dùng `NganhView.ConTuyen` để không giới thiệu ngành đã ngừng tuyển.
- `TaiLieu.ContentKey` đang gồm cả `TrangThai` và `Loi`, nên mỗi lần trạng thái xử lý thay đổi sẽ hiện thành một thay đổi trong diff. Cần xem lại khi làm phần ingestion.
- Embedding của FAQ chưa được tính lại khi sửa FAQ.

## Minor có thể làm khi tiện
- Import:
  - Nhận diện dấu phân cách đang đếm cả dấu nằm trong ngoặc kép.
  - Dòng trống ở đầu file đang bị báo thành lỗi thiếu cột.
  - Số dòng báo lỗi bị lệch khi ô có xuống dòng.
  - Mỗi dòng gọi một truy vấn kiểm tra tham chiếu; chưa giới hạn số dòng.
  - Chưa chặn file xlsx giải nén quá lớn.
  - CSV dùng `tin_chi` trong khi JSON dùng `TinChi`; nên thống nhất.
  - Mức học phí "450" được nhận là 450 đồng; nên có ngưỡng hợp lý hoặc cảnh báo.
- Không có unique trên `mua_tuyen_sinh.nam`; `Release.SoHieu` không unique (đã được advisory lock tuần tự hóa).
- Audit: kích hoạt một release đang active vẫn ghi audit; bản ghi Modified mà chỉ đổi các trường bị loại trừ vẫn bị audit.
- Thiếu test cho:
  - label của ChiTieu, NganhToHop, HocPhi, MocThoiGian, TaiLieuChunk;
  - restore với id không tồn tại (404);
  - nạp dữ liệu mẫu (seeder);
  - rò dữ liệu khi join chéo release;
  - PUT bỏ qua `logicalId` trong body.
- Cảnh báo build: AD0001 (RouteHandlerAnalyzer bị crash, có từ trước Task 9) và CS0618 (`NpgsqlTsVector.Parse` trong ReleaseTests). Log EF "Failed executing DbCommand" từ các test cố ý gây lỗi 23505 làm output test bị nhiễu.
