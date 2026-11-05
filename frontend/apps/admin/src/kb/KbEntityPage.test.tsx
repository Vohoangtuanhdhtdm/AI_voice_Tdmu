import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { Modal } from "antd";
import { http, HttpResponse } from "msw";
import { server } from "../test/server";
import { renderWithProviders } from "../test/render";
import { KbEntityPage } from "./KbEntityPage";
import { entityConfigs } from "./entityConfigs";
import { Form } from "antd";
import { KbEntityForm } from "./KbEntityForm";
import { authStore } from "../auth/authStore";

// antd Modal/Table trong jsdom chậm (3-6s/test): nới timeout chỉ cho file này.
vi.setConfig({ testTimeout: 15000 });

beforeEach(() => {
  authStore.setSession({ accessToken: "t", expiresAt: new Date(Date.now() + 3600_000).toISOString() });
});
afterEach(() => {
  Modal.destroyAll();
  authStore.clear();
  sessionStorage.clear();
});

// Render trang trực tiếp (không qua layout) cho nhanh.
const renderAt = (route: string) => {
  const slug = route.replace("/kb/", "") as "nganh" | "phuong-thuc";
  return renderWithProviders(<KbEntityPage config={entityConfigs[slug]!} />, { route });
};

describe("KbEntityPage", () => {
  it("Lists_rows_and_creates_record", async () => {
    let posted: unknown = null;
    server.use(
      http.get("/api/admin/kb/phuong-thuc", () =>
        HttpResponse.json([{ logicalId: "p1", ma: "THPT", ten: "thi THPT", thangDiem: 30 }]),
      ),
      http.post("/api/admin/kb/phuong-thuc", async ({ request }) => {
        posted = await request.json();
        return HttpResponse.json({ logicalId: "p2", ...(posted as object) }, { status: 201 });
      }),
    );
    const user = userEvent.setup({ delay: null });
    renderAt("/kb/phuong-thuc");
    expect(await screen.findByText("thi THPT")).toBeInTheDocument();
    expect(
      screen.getByText("Thay đổi chỉ áp dụng cho bản nháp. Vào mục Xuất bản để AI sử dụng."),
    ).toBeInTheDocument();

    await user.click(screen.getByRole("button", { name: "Thêm mới" }));
    await user.type(await screen.findByLabelText("Mã"), "DGNL");
    await user.type(screen.getByLabelText("Tên"), "đánh giá năng lực");
    await user.type(screen.getByLabelText("Thang điểm"), "1200");
    await user.click(screen.getByRole("button", { name: "Lưu" }));

    expect(await screen.findByText("Đã lưu vào bản nháp")).toBeInTheDocument();
    expect(posted).toEqual({ ma: "DGNL", ten: "đánh giá năng lực", thangDiem: 1200 });
  });

  it("Server_field_error_shows_under_field", async () => {
    const msg = "Thang điểm không được nhỏ hơn điểm chuẩn cao nhất đang có của phương thức này (850)";
    server.use(
      http.get("/api/admin/kb/phuong-thuc", () => HttpResponse.json([])),
      http.post("/api/admin/kb/phuong-thuc", () =>
        HttpResponse.json({ title: "Dữ liệu không hợp lệ", errors: { thangDiem: [msg] } }, { status: 400 }),
      ),
    );
    const user = userEvent.setup({ delay: null });
    renderAt("/kb/phuong-thuc");
    await user.click(await screen.findByRole("button", { name: "Thêm mới" }));
    await user.type(await screen.findByLabelText("Mã"), "X");
    await user.type(screen.getByLabelText("Tên"), "Y");
    await user.type(screen.getByLabelText("Thang điểm"), "10");
    await user.click(screen.getByRole("button", { name: "Lưu" }));

    const err = await screen.findByText(msg);
    expect(err.closest(".ant-form-item")).toHaveTextContent("Thang điểm");
    expect(screen.getByRole("dialog", { hidden: true })).toBeInTheDocument();
  });

  it("Delete_referenced_shows_references", async () => {
    const refs = ["Điểm chuẩn Công nghệ thông tin 2026 – thi THPT", "Chỉ tiêu Công nghệ thông tin 2026"];
    server.use(
      http.get("/api/admin/kb/nganh", () =>
        HttpResponse.json([{ logicalId: "n1", maNganh: "7480201", ten: "Công nghệ thông tin" }]),
      ),
      http.delete("/api/admin/kb/nganh/n1", () =>
        HttpResponse.json({ title: "Không thể xóa vì đang được tham chiếu", thamChieu: refs }, { status: 409 }),
      ),
    );
    const user = userEvent.setup({ delay: null });
    renderAt("/kb/nganh");
    await user.click(await screen.findByRole("button", { name: "Xóa" }));
    const pop = (await screen.findByText("Xóa bản ghi này khỏi bản nháp?")).closest(".ant-popover") as HTMLElement;
    await user.click(within(pop).getByRole("button", { name: "Xóa" }));

    // antd Modal.error render tiêu đề hai lần (nhãn aria + tiêu đề hiển thị).
    expect((await screen.findAllByText("Không thể xóa vì đang được tham chiếu")).length).toBeGreaterThan(0);
    for (const r of refs) expect(screen.getByText(r)).toBeInTheDocument();
    expect(screen.getByText("Công nghệ thông tin")).toBeInTheDocument();
  });

  it("Edit_prefills_form", async () => {
    server.use(
      http.get("/api/admin/kb/nganh", () =>
        HttpResponse.json([
          { logicalId: "n1", maNganh: "7480201", ten: "Công nghệ thông tin", tenGoiKhac: ["CNTT", "IT"] },
        ]),
      ),
    );
    const user = userEvent.setup({ delay: null });
    renderAt("/kb/nganh");
    await user.click(await screen.findByRole("button", { name: "Sửa" }));
    const dialog = await screen.findByRole("dialog", { hidden: true });
    await waitFor(() => expect(within(dialog).getByLabelText("Tên ngành")).toHaveValue("Công nghệ thông tin"));
    const field = within(dialog).getByText("Tên gọi khác").closest(".ant-form-item")!;
    expect(within(field as HTMLElement).getByText("CNTT")).toBeInTheDocument();
    expect(within(field as HTMLElement).getByText("IT")).toBeInTheDocument();
  });
});

describe("KbEntityPage fix round 1", () => {
  it("Decimal_accepts_vi_comma", async () => {
    let posted: unknown = null;
    server.use(
      http.get("/api/admin/kb/phuong-thuc", () => HttpResponse.json([])),
      http.post("/api/admin/kb/phuong-thuc", async ({ request }) => {
        posted = await request.json();
        return HttpResponse.json({ logicalId: "p2" }, { status: 201 });
      }),
    );
    const user = userEvent.setup({ delay: null });
    renderAt("/kb/phuong-thuc");
    await user.click(await screen.findByRole("button", { name: "Thêm mới" }));
    await user.type(await screen.findByLabelText("Mã"), "A");
    await user.type(screen.getByLabelText("Tên"), "B");
    await user.type(screen.getByLabelText("Thang điểm"), "21,5");
    await user.click(screen.getByRole("button", { name: "Lưu" }));
    expect(await screen.findByText("Đã lưu vào bản nháp")).toBeInTheDocument();
    expect(posted).toEqual({ ma: "A", ten: "B", thangDiem: 21.5 });
  });

  it("Unmatched_field_error_is_visible", async () => {
    server.use(
      http.get("/api/admin/kb/phuong-thuc", () => HttpResponse.json([])),
      http.post("/api/admin/kb/phuong-thuc", () =>
        HttpResponse.json({ title: "Dữ liệu không hợp lệ", errors: { nguonUrl: ["URL không hợp lệ"] } }, { status: 400 }),
      ),
    );
    const user = userEvent.setup({ delay: null });
    renderAt("/kb/phuong-thuc");
    await user.click(await screen.findByRole("button", { name: "Thêm mới" }));
    await user.type(await screen.findByLabelText("Mã"), "A");
    await user.type(screen.getByLabelText("Tên"), "B");
    await user.type(screen.getByLabelText("Thang điểm"), "10");
    await user.click(screen.getByRole("button", { name: "Lưu" }));
    expect(await screen.findByText(/URL không hợp lệ/)).toBeInTheDocument();
    expect(screen.getByRole("dialog", { hidden: true })).toBeInTheDocument();
  });

  it("Money_cleared_gives_null_not_zero", async () => {
    let values: Record<string, unknown> | null = null;
    function Harness() {
      const [form] = Form.useForm();
      return (
        <>
          <KbEntityForm
            form={form}
            fields={[{ name: "hocPhi", label: "Học phí", kind: "money" }]}
            initialValues={{ hocPhi: 1200000 }}
            onFinish={(v) => {
              values = v;
            }}
          />
          <button onClick={() => form.submit()}>go</button>
        </>
      );
    }
    const user = userEvent.setup({ delay: null });
    renderWithProviders(<Harness />);
    const input = screen.getByLabelText("Học phí");
    expect(input).toHaveValue("1.200.000");
    await user.clear(input);
    await user.click(screen.getByRole("button", { name: "go" }));
    await waitFor(() => expect(values).not.toBeNull());
    expect(values!.hocPhi ?? null).toBeNull();
  });

  function numberHarness(field: Parameters<typeof KbEntityForm>[0]["fields"][number], initial?: unknown) {
    const onFinish = vi.fn();
    function Harness() {
      const [form] = Form.useForm();
      return (
        <>
          <KbEntityForm
            form={form}
            fields={[field]}
            initialValues={initial === undefined ? undefined : { [field.name]: initial }}
            onFinish={onFinish}
          />
          <button onClick={() => form.submit()}>go</button>
        </>
      );
    }
    renderWithProviders(<Harness />);
    return onFinish;
  }

  it("Int_with_comma_is_rejected_not_truncated", async () => {
    const onFinish = numberHarness({ name: "soLuong", label: "Số lượng", kind: "int" });
    const user = userEvent.setup({ delay: null });
    await user.type(screen.getByLabelText("Số lượng"), "1,2");
    await user.click(screen.getByRole("button", { name: "go" }));
    expect(await screen.findByText("Số lượng không hợp lệ")).toBeInTheDocument();
    expect(onFinish).not.toHaveBeenCalled();
  });

  it("Money_with_decimal_part_is_rejected", async () => {
    const onFinish = numberHarness({ name: "muc", label: "Mức", kind: "money" });
    const user = userEvent.setup({ delay: null });
    await user.type(screen.getByLabelText("Mức"), "1.200.000,50");
    await user.click(screen.getByRole("button", { name: "go" }));
    expect(await screen.findByText("Mức không hợp lệ")).toBeInTheDocument();
    expect(onFinish).not.toHaveBeenCalled();
  });

  it("Money_accepts_dot_thousands", async () => {
    const onFinish = numberHarness({ name: "muc", label: "Mức", kind: "money" });
    const user = userEvent.setup({ delay: null });
    await user.type(screen.getByLabelText("Mức"), "1.200.000");
    await user.click(screen.getByRole("button", { name: "go" }));
    await waitFor(() => expect(onFinish).toHaveBeenCalledWith({ muc: 1200000 }));
  });

  it("Decimal_reads_grouped_dots_as_thousands", async () => {
    const onFinish = numberHarness({ name: "thangDiem", label: "Thang điểm", kind: "decimal", precision: 2 });
    const user = userEvent.setup({ delay: null });
    await user.type(screen.getByLabelText("Thang điểm"), "1.200");
    await user.click(screen.getByRole("button", { name: "go" }));
    await waitFor(() => expect(onFinish).toHaveBeenCalledWith({ thangDiem: 1200 }));
  });

  it("Int_display_groups_quantity_but_not_year", () => {
    numberHarness({ name: "soLuong", label: "Số lượng", kind: "int" }, 1200);
    expect(screen.getByLabelText("Số lượng")).toHaveValue("1.200");
    cleanup();
    numberHarness({ name: "nam", label: "Năm", kind: "int", grouping: false }, 2026);
    expect(screen.getByLabelText("Năm")).toHaveValue("2026");
  });
});
