import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { delay, http, HttpResponse } from "msw";
import { server } from "../test/server";
import { renderWithProviders } from "../test/render";
import { authStore } from "../auth/authStore";
import { ReleasesPage } from "./ReleasesPage";

vi.setConfig({ testTimeout: 15000 });

beforeEach(() => {
  authStore.setSession({ accessToken: "t", expiresAt: new Date(Date.now() + 3600_000).toISOString() });
});
afterEach(() => {
  authStore.clear();
  sessionStorage.clear();
});

const releases = [
  { id: "r3", soHieu: 3, ghiChu: "Bản mới", taoBoi: "admin", taoLuc: "2026-10-01T03:00:00Z", isActive: true },
  { id: "r2", soHieu: 2, ghiChu: "Bản cũ", taoBoi: "admin", taoLuc: "2026-09-01T03:00:00Z", isActive: false },
];

function mockGets(items: unknown[] = []) {
  server.use(
    http.get("/api/admin/releases/diff", () => HttpResponse.json({ items })),
    http.get("/api/admin/releases", () => HttpResponse.json(releases)),
  );
}

const added = {
  entity: "DiemChuan",
  logicalId: "x",
  kind: "Added",
  label: "Điểm chuẩn Công nghệ thông tin 2026 – thi THPT",
};

describe("ReleasesPage", () => {
  it("Shows_diff_with_kind_tags", async () => {
    mockGets([added]);
    renderWithProviders(<ReleasesPage />);
    expect(await screen.findByText("Điểm chuẩn Công nghệ thông tin 2026 – thi THPT")).toBeInTheDocument();
    expect(screen.getByText("Thêm")).toBeInTheDocument();
    expect(screen.getAllByText("Điểm chuẩn").length).toBeGreaterThan(0);
  });

  it("Empty_diff_message", async () => {
    mockGets([]);
    renderWithProviders(<ReleasesPage />);
    expect(await screen.findByText("Bản nháp trùng với bản đang phát hành")).toBeInTheDocument();
  });

  it("Publish_disables_button_while_pending_and_shows_version", async () => {
    mockGets([added]);
    let posts = 0;
    server.use(
      http.post("/api/admin/releases", async () => {
        posts++;
        await delay(200);
        return HttpResponse.json({ ...releases[0], soHieu: 3 }, { status: 201 });
      }),
    );
    const user = userEvent.setup({ delay: null });
    renderWithProviders(<ReleasesPage />);
    await screen.findByText(added.label);
    await user.click(screen.getByRole("button", { name: "Xuất bản" }));
    const dialog = await screen.findByRole("dialog");
    await user.type(within(dialog).getByLabelText("Ghi chú"), "Cập nhật điểm");
    const ok = within(dialog).getByRole("button", { name: "Đồng ý" });
    await user.click(ok);
    await user.click(ok);
    expect(await screen.findByText("Đã xuất bản phiên bản #3")).toBeInTheDocument();
    expect(posts).toBe(1);
  });

  it("Publish_without_changes_shows_info", async () => {
    let diffCalls = 0;
    server.use(
      http.get("/api/admin/releases/diff", () =>
        HttpResponse.json({ items: diffCalls++ === 0 ? [added] : [] }),
      ),
      http.get("/api/admin/releases", () => HttpResponse.json(releases)),
    );
    server.use(
      http.post("/api/admin/releases", () =>
        HttpResponse.json({ title: "Không có thay đổi để xuất bản" }, { status: 409 }),
      ),
    );
    const user = userEvent.setup({ delay: null });
    renderWithProviders(<ReleasesPage />);
    await screen.findByText(added.label);
    await user.click(screen.getByRole("button", { name: "Xuất bản" }));
    const dialog = await screen.findByRole("dialog");
    await user.type(within(dialog).getByLabelText("Ghi chú"), "x");
    await user.click(within(dialog).getByRole("button", { name: "Đồng ý" }));
    expect(await screen.findByText("Không có thay đổi để xuất bản")).toBeInTheDocument();
    expect(await screen.findByText("Bản nháp trùng với bản đang phát hành")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Xuất bản" })).toBeDisabled();
    expect(screen.getByText("Lịch sử phát hành")).toBeInTheDocument();
    expect(screen.getByText("Bản cũ")).toBeInTheDocument();
  });

  it("Restore_requires_confirmation", async () => {
    mockGets([]);
    let restored = 0;
    server.use(
      http.post("/api/admin/releases/r2/restore-to-draft", () => {
        restored++;
        return new HttpResponse(null, { status: 204 });
      }),
    );
    const user = userEvent.setup({ delay: null });
    renderWithProviders(<ReleasesPage />);
    await screen.findByText("Bản cũ");
    const row = screen.getByText("Bản cũ").closest("tr")!;
    await user.click(within(row).getByRole("button", { name: "Khôi phục về bản nháp #2" }));
    expect(
      await screen.findByText("Toàn bộ bản nháp hiện tại sẽ bị thay bằng nội dung phiên bản #2. Tiếp tục?"),
    ).toBeInTheDocument();
    expect(restored).toBe(0);
    await user.click(screen.getByRole("button", { name: "Đồng ý" }));
    await waitFor(() => expect(restored).toBe(1));
  });

  it("Row_action_buttons_name_the_version", async () => {
    mockGets([]);
    renderWithProviders(<ReleasesPage />);
    expect(await screen.findByRole("button", { name: "Kích hoạt #2" })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Khôi phục về bản nháp #3" })).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Kích hoạt #3" })).not.toBeInTheDocument();
  });

  it("Unauthorized_action_shows_no_error_toast", async () => {
    mockGets([]);
    let called = false;
    server.use(
      http.post("/api/admin/releases/r2/restore-to-draft", () => {
        called = true;
        return HttpResponse.json({ title: "Phiên hết hạn" }, { status: 401 });
      }),
    );
    const user = userEvent.setup({ delay: null });
    renderWithProviders(<ReleasesPage />);
    await user.click(await screen.findByRole("button", { name: "Khôi phục về bản nháp #2" }));
    await user.click(await screen.findByRole("button", { name: "Đồng ý" }));
    await waitFor(() => expect(called).toBe(true));
    await new Promise((r) => setTimeout(r, 100));
    expect(screen.queryByText("Phiên hết hạn")).not.toBeInTheDocument();
  });
});
