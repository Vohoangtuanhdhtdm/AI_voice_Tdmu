# E2E: luồng xuất bản của Admin

Kiểm thử Playwright chạy trên backend .NET thật và PostgreSQL thật:
đăng nhập → nhập dữ liệu → xuất bản → API công khai trả dữ liệu.

## Chạy local

1. Chạy Postgres (cổng 5432 phải trống):

   ```bash
   docker run -d --rm --name tdmu-e2e-pg -e POSTGRES_PASSWORD=postgres \
     -e POSTGRES_DB=tdmu_e2e -p 5432:5432 pgvector/pgvector:pg16
   ```

   (hoặc `docker compose up -d postgres` với DB `tdmu_e2e`; hoặc đặt `E2E_DB` trỏ tới DB khác).

2. Chạy test (Playwright tự khởi động backend ở :5110 và Vite ở :5173; lần build .NET đầu có thể mất 1–2 phút):

   ```bash
   pnpm -C frontend/apps/admin exec playwright install chromium   # lần đầu
   pnpm -C frontend/apps/admin e2e
   ```

   Môi trường đã có sẵn Chromium (ví dụ cloud) thì bỏ qua `playwright install` và đặt:

   ```bash
   PLAYWRIGHT_CHROMIUM_PATH=/opt/pw-browsers/chromium pnpm -C frontend/apps/admin e2e
   ```

   `/opt/pw-browsers/chromium` là symlink tới `/opt/pw-browsers/chromium-1243/chrome-linux64/chrome`.
   `@playwright/test` được ghim đúng `1.63.0` để khớp bản trình duyệt cài sẵn.

3. Dừng Postgres: `docker stop tdmu-e2e-pg`.

Biến môi trường: `E2E_DB` (chuỗi kết nối), `PLAYWRIGHT_CHROMIUM_PATH`. Mỗi lần chạy dùng mã ngẫu nhiên nên có thể chạy lại trên cùng DB.
Vitest bỏ qua thư mục `e2e/` (`test.exclude`).
