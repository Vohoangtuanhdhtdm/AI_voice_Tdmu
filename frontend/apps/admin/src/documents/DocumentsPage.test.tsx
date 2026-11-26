import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { http, HttpResponse } from "msw";
import { server } from "../test/server";
import { renderWithProviders } from "../test/render";
import { authStore } from "../auth/authStore";
import type { TaiLieuDto } from "../api/types";
import { DocumentsPage } from "./DocumentsPage";

vi.setConfig({ testTimeout: 15000 });

beforeEach(() => {
  authStore.setSession({ accessToken: "t", expiresAt: new Date(Date.now() + 3600_000).toISOString() });
});
afterEach(() => {
  vi.restoreAllMocks();
  authStore.clear();
  sessionStorage.clear();
});

const doc = (over: Partial<TaiLieuDto>): TaiLieuDto => ({
  logicalId: "d1",
  tieuDe: "Đề án tuyển sinh 2026",
  tenFileGoc: "de-an.pdf",
  namApDung: 2026,
  trangThai: "Xong",
  loi: null,
  soDoan: 12,
  capNhatLuc: "2026-03-01T03:00:00Z",
  nguonTen: null,
  nguonUrl: null,
  ...over,
});

const fileInput = () => document.querySelector<HTMLInputElement>('input[type="file"]')!;

describe("DocumentsPage", () => {
  it("Lists_documents_with_status_tags", async () => {
    server.use(
      http.get("/api/admin/tai-lieu", () =>
        HttpResponse.json([
          doc({ logicalId: "a", tieuDe: "Tài liệu A", trangThai: "ChoXuLy" }),
          doc({ logicalId: "b", tieuDe: "Tài liệu B", trangThai: "DangXuLy" }),
          doc({ logicalId: "c", tieuDe: "Tài liệu C", trangThai: "Xong" }),
          doc({ logicalId: "d", tieuDe: "Tài liệu D", trangThai: "ThatBai", loi: "x" }),
        ]),
      ),
    );
    renderWithProviders(<DocumentsPage pollMs={0} />);
    expect(await screen.findByText("Tài liệu A")).toBeInTheDocument();
    for (const t of ["Chờ xử lý", "Đang xử lý", "Đã xử lý", "Lỗi"]) {
      expect(screen.getByText(t)).toBeInTheDocument();
    }
    expect(
      screen.getByText("Tài liệu được xử lý tự động. Cần chờ xử lý xong trước khi xuất bản."),
    ).toBeInTheDocument();
  });

  it("Upload_posts_multipart_and_refreshes", async () => {
    let lists = 0;
    let posted: FormData | null = null;
    server.use(
      http.get("/api/admin/tai-lieu", () => {
        lists++;
        return HttpResponse.json([]);
      }),
      http.post("/api/admin/tai-lieu", () => {
        return HttpResponse.json(doc({ trangThai: "ChoXuLy" }), { status: 201 });
      }),
    );
    // request.formData() treo với FormData của jsdom, nên bắt body ngay tại fetch.
    const realFetch = globalThis.fetch;
    vi.spyOn(globalThis, "fetch").mockImplementation((input, init) => {
      if (init?.method === "POST" && init.body instanceof FormData) posted = init.body;
      return realFetch(input, init);
    });
    const user = userEvent.setup({ delay: null });
    renderWithProviders(<DocumentsPage pollMs={0} />);
    await waitFor(() => expect(lists).toBe(1));
    await user.type(screen.getByLabelText("Tiêu đề"), "Đề án mới");
    await user.type(screen.getByLabelText("Năm áp dụng"), "2026");
    await user.upload(fileInput(), new File(["x"], "a.pdf", { type: "application/pdf" }));
    await user.click(screen.getByRole("button", { name: "Tải lên" }));
    await waitFor(() => expect(posted).not.toBeNull());
    expect(posted!.get("tieuDe")).toBe("Đề án mới");
    expect(posted!.get("namApDung")).toBe("2026");
    expect((posted!.get("file") as File).name).toBe("a.pdf");
    await waitFor(() => expect(lists).toBe(2));
  });

  it("Failed_shows_reason_tooltip_and_reprocess", async () => {
    let reprocessed = "";
    server.use(
      http.get("/api/admin/tai-lieu", () =>
        HttpResponse.json([doc({ logicalId: "bad", trangThai: "ThatBai", loi: "Không đọc được PDF" })]),
      ),
      http.post("/api/admin/tai-lieu/:id/xu-ly-lai", ({ params }) => {
        reprocessed = String(params.id);
        return new HttpResponse(null, { status: 204 });
      }),
    );
    const user = userEvent.setup({ delay: null });
    renderWithProviders(<DocumentsPage pollMs={0} />);
    await user.hover(await screen.findByText("Lỗi"));
    expect(await screen.findByText("Không đọc được PDF")).toBeInTheDocument();
    await user.click(screen.getByRole("button", { name: "Xử lý lại" }));
    await waitFor(() => expect(reprocessed).toBe("bad"));
  });

  it("Delete_confirms_then_deletes", async () => {
    let deleted = "";
    server.use(
      http.get("/api/admin/tai-lieu", () => HttpResponse.json([doc({ logicalId: "z" })])),
      http.delete("/api/admin/tai-lieu/:id", ({ params }) => {
        deleted = String(params.id);
        return new HttpResponse(null, { status: 204 });
      }),
    );
    const user = userEvent.setup({ delay: null });
    renderWithProviders(<DocumentsPage pollMs={0} />);
    await user.click(await screen.findByRole("button", { name: "Xóa" }));
    const pop = await screen.findByRole("tooltip");
    await user.click(within(pop).getByRole("button", { name: /Xóa/ }));
    await waitFor(() => expect(deleted).toBe("z"));
  });

  it("Rejects_file_over_20mb", async () => {
    let posts = 0;
    server.use(
      http.get("/api/admin/tai-lieu", () => HttpResponse.json([])),
      http.post("/api/admin/tai-lieu", () => {
        posts++;
        return HttpResponse.json(doc({}), { status: 201 });
      }),
    );
    const user = userEvent.setup({ delay: null });
    renderWithProviders(<DocumentsPage pollMs={0} />);
    const big = new File(["x"], "big.pdf", { type: "application/pdf" });
    Object.defineProperty(big, "size", { value: 20 * 1024 * 1024 + 1 });
    await user.upload(fileInput(), big);
    expect(await screen.findByText("File vượt quá 20 MB")).toBeInTheDocument();
    await user.type(screen.getByLabelText("Tiêu đề"), "X");
    expect(screen.getByRole("button", { name: "Tải lên" })).toBeDisabled();
    expect(posts).toBe(0);
  });

  it("Polls_while_processing", async () => {
    let n = 0;
    server.use(
      http.get("/api/admin/tai-lieu", () => {
        n++;
        return HttpResponse.json([doc({ trangThai: n < 3 ? "DangXuLy" : "Xong" })]);
      }),
    );
    renderWithProviders(<DocumentsPage pollMs={30} />);
    await waitFor(() => expect(n).toBeGreaterThanOrEqual(3));
    expect(await screen.findByText("Đã xử lý")).toBeInTheDocument();
    const settled = n;
    await new Promise((r) => setTimeout(r, 150));
    expect(n).toBe(settled); // hết tài liệu đang xử lý thì dừng poll
  });
});
