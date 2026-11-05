import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { Modal } from "antd";
import { http, HttpResponse, type JsonBodyType } from "msw";
import { server } from "../test/server";
import { renderWithProviders } from "../test/render";
import { KbEntityPage } from "./KbEntityPage";
import { entityConfigs } from "./entityConfigs";
import { authStore } from "../auth/authStore";
import { KB_SLUGS, KB_TITLES, type KbSlug } from "../layout/navigation";

// antd Modal/Select/DatePicker trong jsdom chậm: nới timeout chỉ cho file này.
vi.setConfig({ testTimeout: 20000 });

const NGANH = { logicalId: "n1", maNganh: "7480201", ten: "Công nghệ thông tin" };
const PT = { logicalId: "p1", ten: "thi THPT" };

// rc-trigger cảnh báo giả khi popup DatePicker còn đang gắn vào DOM trong jsdom; chỉ bỏ qua đúng câu này.
const realError = console.error.bind(console);
beforeEach(() => {
  vi.spyOn(console, "error").mockImplementation((...args: unknown[]) => {
    if (String(args[0]).includes("should in same shadow root")) return;
    realError(...args);
  });
  authStore.setSession({ accessToken: "t", expiresAt: new Date(Date.now() + 3600_000).toISOString() });
});
afterEach(() => {
  vi.restoreAllMocks();
  Modal.destroyAll();
  authStore.clear();
  sessionStorage.clear();
});

const renderSlug = (slug: KbSlug) =>
  renderWithProviders(<KbEntityPage config={entityConfigs[slug]!} />, { route: `/kb/${slug}` });

const json = (url: string, body: JsonBodyType) => http.get(url, () => HttpResponse.json(body));

/** Mở ô chọn theo nhãn rồi chọn một lựa chọn theo chữ hiển thị. */
async function pick(user: ReturnType<typeof userEvent.setup>, dialog: HTMLElement, label: string, option: string) {
  await user.click(within(dialog).getByLabelText(label));
  await user.click(await screen.findByText(option, { selector: ".ant-select-item-option-content" }));
}

describe("entity configs", () => {
  it("Diem_chuan_form_selects_by_name_and_posts_lids", async () => {
    let posted: unknown = null;
    server.use(
      json("/api/admin/kb/diem-chuan", [
        { logicalId: "d1", nganhLid: "n1", nam: 2026, phuongThucLid: "p1", diem: 21.5 },
      ]),
      json("/api/admin/kb/nganh", [NGANH]),
      json("/api/admin/kb/phuong-thuc", [PT]),
      http.post("/api/admin/kb/diem-chuan", async ({ request }) => {
        posted = await request.json();
        return HttpResponse.json({ logicalId: "d2" }, { status: 201 });
      }),
    );
    const user = userEvent.setup({ delay: null });
    renderSlug("diem-chuan");

    // Bảng hiện tên, không phải GUID.
    expect(await screen.findByText("Công nghệ thông tin")).toBeInTheDocument();
    expect(screen.getByText("thi THPT")).toBeInTheDocument();
    expect(screen.getByText("21,5")).toBeInTheDocument();
    expect(screen.queryByText("n1")).not.toBeInTheDocument();

    await user.click(screen.getByRole("button", { name: "Thêm mới" }));
    const dialog = await screen.findByRole("dialog", { hidden: true });
    await pick(user, dialog, "Ngành", "7480201 – Công nghệ thông tin");
    await pick(user, dialog, "Phương thức", "thi THPT");
    await user.type(within(dialog).getByLabelText("Năm"), "2026");
    await user.type(within(dialog).getByLabelText("Điểm"), "21,5");
    await user.click(screen.getByRole("button", { name: "Lưu" }));

    expect(await screen.findByText("Đã lưu vào bản nháp")).toBeInTheDocument();
    expect(posted).toEqual({ nganhLid: "n1", phuongThucLid: "p1", diem: 21.5, nam: 2026 });
  });

  it("Diem_cleared_gives_required_error_and_no_post", async () => {
    let posts = 0;
    server.use(
      json("/api/admin/kb/diem-chuan", [
        { logicalId: "d1", nganhLid: "n1", nam: 2026, phuongThucLid: "p1", diem: 21.5 },
      ]),
      json("/api/admin/kb/nganh", [NGANH]),
      json("/api/admin/kb/phuong-thuc", [PT]),
      http.put("/api/admin/kb/diem-chuan/d1", () => {
        posts++;
        return HttpResponse.json({});
      }),
    );
    const user = userEvent.setup({ delay: null });
    renderSlug("diem-chuan");
    await user.click(await screen.findByRole("button", { name: /^Sửa / }));
    const dialog = await screen.findByRole("dialog", { hidden: true });
    const diem = within(dialog).getByLabelText("Điểm");
    await waitFor(() => expect(diem).toHaveValue("21,5"));
    await user.clear(diem);
    await user.click(screen.getByRole("button", { name: "Lưu" }));
    expect(await screen.findByText("Vui lòng nhập điểm")).toBeInTheDocument();
    expect(posts).toBe(0);
  });

  it("Hoc_phi_enum_and_money", async () => {
    let posted: Record<string, unknown> | null = null;
    server.use(
      json("/api/admin/kb/hoc-phi", [
        { logicalId: "h1", nganhLid: null, nam: 2026, muc: 1200000, donVi: "TinChi" },
      ]),
      json("/api/admin/kb/nganh", [NGANH]),
      http.post("/api/admin/kb/hoc-phi", async ({ request }) => {
        posted = (await request.json()) as Record<string, unknown>;
        return HttpResponse.json({ logicalId: "h2" }, { status: 201 });
      }),
    );
    const user = userEvent.setup({ delay: null });
    renderSlug("hoc-phi");
    expect(await screen.findByText("1.200.000")).toBeInTheDocument();
    expect(screen.getByText("Áp dụng chung")).toBeInTheDocument();

    await user.click(screen.getByRole("button", { name: "Thêm mới" }));
    const dialog = await screen.findByRole("dialog", { hidden: true });
    await user.type(within(dialog).getByLabelText("Năm"), "2026");
    await user.type(within(dialog).getByLabelText("Mức"), "1500000");
    await pick(user, dialog, "Đơn vị", "Tín chỉ");
    await user.click(screen.getByRole("button", { name: "Lưu" }));
    expect(await screen.findByText("Đã lưu vào bản nháp")).toBeInTheDocument();
    expect(posted).toMatchObject({ nam: 2026, muc: 1500000, donVi: "TinChi" });
  });

  it("Moc_thoi_gian_round_trips_vietnam_time", async () => {
    let posted: Record<string, unknown> | null = null;
    server.use(
      json("/api/admin/kb/moc-thoi-gian", [
        { logicalId: "m1", nam: 2026, tenMoc: "Mở đăng ký", batDau: "2026-07-01T01:00:00Z" },
      ]),
      json("/api/admin/kb/phuong-thuc", [PT]),
      http.post("/api/admin/kb/moc-thoi-gian", async ({ request }) => {
        posted = (await request.json()) as Record<string, unknown>;
        return HttpResponse.json({ logicalId: "m2" }, { status: 201 });
      }),
    );
    const user = userEvent.setup({ delay: null });
    renderSlug("moc-thoi-gian");
    expect(await screen.findByText("01/07/2026 08:00")).toBeInTheDocument();

    // Sửa: ô ngày giờ hiện 08:00 (giờ Việt Nam), không phải 01:00 UTC.
    await user.click(screen.getByRole("button", { name: /^Sửa / }));
    let dialog = await screen.findByRole("dialog", { hidden: true });
    await waitFor(() => expect(within(dialog).getByLabelText("Bắt đầu")).toHaveValue("01/07/2026 08:00"));
    await user.click(screen.getByRole("button", { name: "Hủy" }));
    await waitFor(() => expect(screen.queryByRole("dialog")).not.toBeInTheDocument());

    // Thêm: nhập 01/07/2026 08:00 -> gửi +07:00.
    await user.click(screen.getByRole("button", { name: "Thêm mới" }));
    dialog = await screen.findByRole("dialog", { hidden: true });
    await user.type(within(dialog).getByLabelText("Năm"), "2026");
    await user.type(within(dialog).getByLabelText("Tên mốc"), "Mở đăng ký");
    await user.type(within(dialog).getByLabelText("Bắt đầu"), "01/07/2026 08:00{Enter}");
    // Đóng popup chọn ngày trước khi lưu.
    await user.click(within(dialog).getByText("Thêm mới: Lịch tuyển sinh"));
    await user.click(screen.getByRole("button", { name: "Lưu" }));
    expect(await screen.findByText("Đã lưu vào bản nháp")).toBeInTheDocument();
    expect(posted!.batDau).toMatch(/^2026-07-01T08:00:00\+07:00$/);
  });

  it("Ref_list_load_failure_shows_alert", async () => {
    server.use(
      json("/api/admin/kb/diem-chuan", []),
      http.get("/api/admin/kb/nganh", () => HttpResponse.json({ title: "Lỗi" }, { status: 500 })),
      json("/api/admin/kb/phuong-thuc", [PT]),
    );
    renderSlug("diem-chuan");
    expect(
      await screen.findByText("Không tải được danh sách tham chiếu (ngành/phương thức/tổ hợp). Vui lòng tải lại trang."),
    ).toBeInTheDocument();
  });

  it("Cleared_optional_ref_is_sent_as_null", async () => {
    let put: Record<string, unknown> | null = null;
    server.use(
      json("/api/admin/kb/hoc-phi", [
        { logicalId: "h1", nganhLid: "n1", nam: 2026, muc: 1200000, donVi: "TinChi" },
      ]),
      json("/api/admin/kb/nganh", [NGANH]),
      http.put("/api/admin/kb/hoc-phi/h1", async ({ request }) => {
        put = (await request.json()) as Record<string, unknown>;
        return HttpResponse.json({});
      }),
    );
    const user = userEvent.setup({ delay: null });
    renderSlug("hoc-phi");
    await user.click(await screen.findByRole("button", { name: /^Sửa / }));
    const dialog = await screen.findByRole("dialog", { hidden: true });
    await within(dialog).findByText("7480201 – Công nghệ thông tin");
    const field = within(dialog).getByText("Ngành").closest(".ant-form-item") as HTMLElement;
    const sel = field.querySelector(".ant-select") as HTMLElement;
    await user.hover(sel);
    await user.click(field.querySelector(".ant-select-clear") as HTMLElement);
    await user.click(screen.getByRole("button", { name: "Lưu" }));
    expect(await screen.findByText("Đã lưu vào bản nháp")).toBeInTheDocument();
    expect(put).toHaveProperty("nganhLid", null);
  });

  it("Chi_tieu_soLuong_accepts_vi_thousands", async () => {
    let posted: unknown = null;
    server.use(
      json("/api/admin/kb/chi-tieu", [{ logicalId: "c1", nganhLid: "n1", nam: 2026, soLuong: 1500 }]),
      json("/api/admin/kb/nganh", [NGANH]),
      json("/api/admin/kb/phuong-thuc", [PT]),
      http.post("/api/admin/kb/chi-tieu", async ({ request }) => {
        posted = await request.json();
        return HttpResponse.json({ logicalId: "c2" }, { status: 201 });
      }),
    );
    const user = userEvent.setup({ delay: null });
    renderSlug("chi-tieu");
    // Bảng: số lượng có dấu chấm nghìn, năm thì không.
    expect(await screen.findByText("1.500")).toBeInTheDocument();
    expect(screen.getByRole("cell", { name: "2026" })).toBeInTheDocument();

    await user.click(screen.getByRole("button", { name: "Thêm mới" }));
    const dialog = await screen.findByRole("dialog", { hidden: true });
    await pick(user, dialog, "Ngành", "7480201 – Công nghệ thông tin");
    await user.type(within(dialog).getByLabelText("Năm"), "2026");
    await user.type(within(dialog).getByLabelText("Số lượng"), "1.200");
    await user.click(screen.getByRole("button", { name: "Lưu" }));
    expect(await screen.findByText("Đã lưu vào bản nháp")).toBeInTheDocument();
    expect(posted).toEqual({ nganhLid: "n1", nam: 2026, soLuong: 1200, phuongThucLid: null });
  });

  it("Year_fields_are_not_grouped", () => {
    for (const slug of KB_SLUGS) {
      for (const f of entityConfigs[slug]!.fields) {
        if (f.name === "nam") expect(f).toMatchObject({ kind: "int", grouping: false });
      }
    }
  });

  it.each(KB_SLUGS)("All_ten_slugs_render: %s", async (slug) => {
    server.use(
      http.get("/api/admin/kb/:slug", () => HttpResponse.json([])),
    );
    renderSlug(slug);
    expect(await screen.findByRole("heading", { name: KB_TITLES[slug] })).toBeInTheDocument();
    expect(entityConfigs[slug]!.title).toBe(KB_TITLES[slug]);
  });
});
