import { afterEach, beforeEach, describe, expect, it } from "vitest";
import { screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { Modal } from "antd";
import { http, HttpResponse } from "msw";
import { server } from "../test/server";
import { renderWithProviders } from "../test/render";
import { AppRoutes } from "../App";
import { authStore } from "../auth/authStore";

beforeEach(() => {
  authStore.setSession({ accessToken: "t", expiresAt: new Date(Date.now() + 3600_000).toISOString() });
});
afterEach(() => {
  Modal.destroyAll();
  authStore.clear();
  sessionStorage.clear();
});

const renderAt = (route: string) => renderWithProviders(<AppRoutes />, { route });

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
    const user = userEvent.setup();
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
    const user = userEvent.setup();
    renderAt("/kb/phuong-thuc");
    await user.click(await screen.findByRole("button", { name: "Thêm mới" }));
    await user.type(await screen.findByLabelText("Mã"), "X");
    await user.type(screen.getByLabelText("Tên"), "Y");
    await user.type(screen.getByLabelText("Thang điểm"), "10");
    await user.click(screen.getByRole("button", { name: "Lưu" }));

    const err = await screen.findByText(msg);
    expect(err.closest(".ant-form-item")).toHaveTextContent("Thang điểm");
    expect(screen.getByRole("dialog")).toBeInTheDocument();
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
    const user = userEvent.setup();
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
    const user = userEvent.setup();
    renderAt("/kb/nganh");
    await user.click(await screen.findByRole("button", { name: "Sửa" }));
    const dialog = await screen.findByRole("dialog");
    await waitFor(() => expect(within(dialog).getByLabelText("Tên ngành")).toHaveValue("Công nghệ thông tin"));
    const field = within(dialog).getByText("Tên gọi khác").closest(".ant-form-item")!;
    expect(within(field as HTMLElement).getByText("CNTT")).toBeInTheDocument();
    expect(within(field as HTMLElement).getByText("IT")).toBeInTheDocument();
  });
});
