# Việc tồn đọng sau Kế hoạch 2 (Admin UI)

Danh sách này tổng hợp từ review từng task và review cuối của Kế hoạch 2. Những việc còn tồn từ Kế hoạch 1 xem trong `p1-followups.md`.

## Nên làm sớm (trải nghiệm cán bộ)
- **Truy cập (a11y):** `ViNumberInput` (`src/kb/KbEntityForm.tsx`) chưa truyền tiếp `aria-describedby`, `aria-invalid` và `aria-required` từ `Form.Item`. Vì vậy trình đọc màn hình không đọc được lỗi của ô số. Cách sửa: spread các props còn lại vào `Input`.
- **Báo lỗi khi đang gõ:** lỗi "… không hợp lệ" hiện ra ngay khi đang gõ dở một số hợp lệ (ví dụ đang gõ "1.2" để thành "1.200"). Nên chỉ kiểm tra khi rời ô (blur) và khi lưu.
- **Cột "Người tạo"** ở trang Xuất bản đang hiện GUID. Backend nên trả về email người tạo.
- **Bảng xem trước khi nhập file** đang dùng tên cột gốc (`ma_nganh`, `so_luong`). Nên đổi sang nhãn tiếng Việt.
- **Ô học phí** vẫn nhận các dấu phân cách đặt lung tung, ví dụ "1.2.3" thành 123. Nên bắt buộc đúng nhóm 3 chữ số như ô số nguyên.

## Kỹ thuật
- **Bundle 1,29 MB:** nên lazy-load các trang Nhập dữ liệu và Xuất bản.
- **Năm trong bộ lọc:** bộ lọc năm đang lọc ở client, tham số `nam` của `useKbList` chưa dùng tới. Khi xóa bản ghi, năm đang chọn có thể không còn tồn tại.
- **Ô text tùy chọn để trống** đang gửi `""` thay vì `null`.
- **Đăng xuất:** có race giữa thao tác đăng xuất và redirect của `RequireAuth`, có thể để lại `returnTo` thừa.
- **Menu:** mục menu chưa phải link thật, nên không mở được bằng tab mới hoặc chuột giữa.
- **Đường dẫn `/login`:** đang so khớp phân biệt hoa thường (`/LOGIN`).
- **Thiếu test cho:**
  - nhập file bị trả về 413;
  - reset ghi chú ở trang Xuất bản;
  - rowLabel;
  - refetch sau khi thêm/sửa/xóa.
- **Mock trong test:** mock diff ở `ReleasesPage.test` phụ thuộc vào thứ tự gọi API.
- **CI:** chưa cache trình duyệt của Playwright.
