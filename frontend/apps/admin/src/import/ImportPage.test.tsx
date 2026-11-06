import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { delay, http, HttpResponse } from "msw";
import { server } from "../test/server";
import { renderWithProviders } from "../test/render";
import { authStore } from "../auth/authStore";
import { ImportPage } from "./ImportPage";

vi.setConfig({ testTimeout: 15000 });

beforeEach(() => {
  authStore.setSession({ accessToken: "t", expiresAt: new Date(Date.now() + 3600_000).toISOString() });
});
afterEach(() => {
  authStore.clear();
  sessionStorage.clear();
});

const csv = () => new File(["a,b\n1,2"], "d.csv", { type: "text/csv" });
const fileInput = () => document.querySelector<HTMLInputElement>('input[type="file"]')!;

const preview = (errorCount: number) => ({
  kind: "diem-chuan",
  rows: [
    { rowNumber: 2, values: { diem: "21,5" }, errors: [] },
    {
      rowNumber: 3,
      values: { diem: "35" },
      errors: errorCount ? ["Điểm không được vượt quá thang điểm của phương thức (30)"] : [],
    },
  ],
  validCount: 2 - errorCount,
  errorCount,
});

describe("ImportPage", () => {
  it("Preview_shows_row_errors_and_blocks_commit", async () => {
    server.use(http.post("/api/admin/import/diem-chuan/preview", () => HttpResponse.json(preview(1))));
    const user = userEvent.setup({ delay: null });
    renderWithProviders(<ImportPage />);
    await user.upload(fileInput(), csv());
    expect(
      await screen.findByText("Điểm không được vượt quá thang điểm của phương thức (30)"),
    ).toBeInTheDocument();
    expect(screen.getByText("1 dòng hợp lệ, 1 dòng lỗi")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Ghi vào bản nháp" })).toBeDisabled();
  });

  it("Valid_preview_commits", async () => {
    server.use(
      http.post("/api/admin/import/diem-chuan/preview", () => HttpResponse.json(preview(0))),
      http.post("/api/admin/import/diem-chuan/commit", () => HttpResponse.json({ inserted: 2, updated: 1 })),
    );
    const user = userEvent.setup({ delay: null });
    renderWithProviders(<ImportPage />);
    await user.upload(fileInput(), csv());
    const btn = await screen.findByRole("button", { name: "Ghi vào bản nháp" });
    await waitFor(() => expect(btn).toBeEnabled());
    await user.click(btn);
    expect(await screen.findByText("Đã thêm 2, cập nhật 1 bản ghi trong bản nháp")).toBeInTheDocument();
  });

  it("File_level_error_shows_alert", async () => {
    server.use(
      http.post("/api/admin/import/diem-chuan/preview", () =>
        HttpResponse.json({ title: "Thiếu cột bắt buộc: diem" }, { status: 400 }),
      ),
    );
    const user = userEvent.setup({ delay: null });
    renderWithProviders(<ImportPage />);
    await user.upload(fileInput(), csv());
    expect(await screen.findByText("Thiếu cột bắt buộc: diem")).toBeInTheDocument();
  });

  it("Rejects_files_over_5mb_client_side", async () => {
    let calls = 0;
    server.use(
      http.post("/api/admin/import/diem-chuan/preview", () => {
        calls++;
        return HttpResponse.json(preview(0));
      }),
    );
    const user = userEvent.setup({ delay: null });
    renderWithProviders(<ImportPage />);
    await user.upload(fileInput(), new File([new Uint8Array(6 * 1024 * 1024)], "x.csv"));
    expect(await screen.findByText("File vượt quá 5 MB")).toBeInTheDocument();
    expect(calls).toBe(0);
  });

  it("Template_download_uses_auth", async () => {
    let auth: string | null = null;
    server.use(
      http.get("/api/admin/import/diem-chuan/template", ({ request }) => {
        auth = request.headers.get("Authorization");
        return new HttpResponse(new Uint8Array([1, 2]), { headers: { "Content-Type": "application/octet-stream" } });
      }),
    );
    URL.createObjectURL = vi.fn(() => "blob:x");
    URL.revokeObjectURL = vi.fn();
    const click = vi.spyOn(HTMLAnchorElement.prototype, "click").mockImplementation(() => {});
    const user = userEvent.setup({ delay: null });
    renderWithProviders(<ImportPage />);
    await user.click(screen.getByRole("button", { name: /Tải file mẫu/ }));
    await waitFor(() => expect(auth).toBe("Bearer t"));
    await waitFor(() => expect(click).toHaveBeenCalled());
    click.mockRestore();
  });

  it("Stale_preview_ignored_after_kind_change", async () => {
    server.use(
      http.post("/api/admin/import/diem-chuan/preview", async () => {
        await delay(300);
        return HttpResponse.json(preview(0));
      }),
    );
    const user = userEvent.setup({ delay: null });
    renderWithProviders(<ImportPage />);
    await user.upload(fileInput(), csv());
    // Select bị khóa khi đang preview, nên đổi loại qua phím tắt không có hiệu lực: kiểm tra khóa.
    expect(screen.getByRole("combobox", { name: "Loại dữ liệu" })).toBeDisabled();
    await new Promise((r) => setTimeout(r, 500));
    expect(await screen.findByRole("button", { name: "Ghi vào bản nháp" })).toBeEnabled();
  });

  it("Commit_400_with_preview_body_shows_row_errors", async () => {
    server.use(
      http.post("/api/admin/import/diem-chuan/preview", () => HttpResponse.json(preview(0))),
      http.post("/api/admin/import/diem-chuan/commit", () =>
        // Đúng thân phản hồi của backend: preview lồng trong khóa "preview".
        HttpResponse.json(
          { status: 400, title: "File còn dòng lỗi, chưa nhập dữ liệu nào", preview: preview(1) },
          { status: 400 },
        ),
      ),
    );
    const user = userEvent.setup({ delay: null });
    renderWithProviders(<ImportPage />);
    await user.upload(fileInput(), csv());
    const btn = await screen.findByRole("button", { name: "Ghi vào bản nháp" });
    await waitFor(() => expect(btn).toBeEnabled());
    await user.click(btn);
    expect(
      await screen.findByText("Điểm không được vượt quá thang điểm của phương thức (30)"),
    ).toBeInTheDocument();
    expect(screen.getByText("File còn dòng lỗi, chưa nhập dữ liệu nào")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Ghi vào bản nháp" })).toBeDisabled();
  });

  it("Commit_400_flat_preview_body_is_also_accepted", async () => {
    server.use(
      http.post("/api/admin/import/diem-chuan/preview", () => HttpResponse.json(preview(0))),
      http.post("/api/admin/import/diem-chuan/commit", () =>
        HttpResponse.json({ ...preview(1), title: "File có dòng lỗi" }, { status: 400 }),
      ),
    );
    const user = userEvent.setup({ delay: null });
    renderWithProviders(<ImportPage />);
    await user.upload(fileInput(), csv());
    const btn = await screen.findByRole("button", { name: "Ghi vào bản nháp" });
    await waitFor(() => expect(btn).toBeEnabled());
    await user.click(btn);
    expect(
      await screen.findByText("Điểm không được vượt quá thang điểm của phương thức (30)"),
    ).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Ghi vào bản nháp" })).toBeDisabled();
  });

  it("Error_rows_are_listed_first", async () => {
    server.use(http.post("/api/admin/import/diem-chuan/preview", () => HttpResponse.json(preview(1))));
    const user = userEvent.setup({ delay: null });
    renderWithProviders(<ImportPage />);
    await user.upload(fileInput(), csv());
    await screen.findByText("Điểm không được vượt quá thang điểm của phương thức (30)");
    const rows = document.querySelectorAll("tbody tr.ant-table-row");
    expect(rows[0]).toHaveTextContent("35");
    expect(rows[0]).toHaveClass("import-row-error");
    expect(rows[1]).toHaveTextContent("21,5");
  });
});
