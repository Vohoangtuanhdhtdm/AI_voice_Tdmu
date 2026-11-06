# Tdmu.Advisor – Frontend (Kế hoạch 2: trang quản trị)

Trang quản trị cho cán bộ tuyển sinh, viết bằng React + TypeScript + Ant Design, nằm trong `frontend/apps/admin`. Trang này gọi API .NET ở `backend/` qua đường dẫn `/api`.

## 1. Cài đặt công cụ

- [Node.js 22](https://nodejs.org/) (`node -v` phải ra `v22.x`).
- [pnpm 10](https://pnpm.io/) (`pnpm -v` phải ra `10.x`). Có thể bật qua Corepack: `corepack enable && corepack prepare pnpm@10 --activate`.

## 2. Cài dependency

Từ thư mục gốc repo:

```bash
pnpm -C frontend install
```

## 3. Chạy backend

Frontend cần backend đang chạy. Xem hướng dẫn đầy đủ (PostgreSQL, bí mật, chạy API) trong [`backend/README.md`](../backend/README.md). Mặc định API nghe ở `http://localhost:5110`.

## 4. Chạy giao diện để phát triển

```bash
pnpm -C frontend dev
```

Vite chạy ở `http://localhost:5173`. Mọi request `/api` được proxy tới `VITE_API_TARGET`, mặc định `http://localhost:5110` (cấu hình trong `frontend/apps/admin/vite.config.ts`). Nếu backend nghe ở cổng khác, đặt biến môi trường trước khi chạy:

```bash
VITE_API_TARGET=http://localhost:7476 pnpm -C frontend dev
```

## 5. Kiểm tra và build

```bash
pnpm -C frontend typecheck   # kiểm tra kiểu TypeScript
pnpm -C frontend test        # test đơn vị (Vitest + Testing Library, MSW)
pnpm -C frontend build       # typecheck rồi build production ra apps/admin/dist
pnpm -C frontend/apps/admin e2e   # test end-to-end (Playwright), xem bên dưới
```

Test end-to-end chạy trên backend .NET và PostgreSQL thật, cần Docker để chạy Postgres. Hướng dẫn chi tiết, biến môi trường và cách dùng Chromium có sẵn nằm trong [`frontend/apps/admin/e2e/README.md`](apps/admin/e2e/README.md).

## 6. Triển khai

- `pnpm -C frontend build` tạo thư mục tĩnh `frontend/apps/admin/dist`.
- Phục vụ thư mục `dist` **cùng origin** với API: reverse proxy (nginx, Caddy, v.v.) gửi `/api` tới backend và mọi đường dẫn khác tới `dist`.
- Vì cùng origin nên **không cần CORS**. Không đặt `VITE_API_TARGET` khi build; biến này chỉ dùng cho dev server.
- Máy chủ tĩnh phải trả về `index.html` cho các đường dẫn SPA không tồn tại (SPA fallback), để tải lại trang ở một route con vẫn hoạt động.
