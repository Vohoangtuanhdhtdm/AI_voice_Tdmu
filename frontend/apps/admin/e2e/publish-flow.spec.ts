import { expect, test, type Page } from "@playwright/test";

const API = "http://localhost:5110";

async function openMenu(page: Page, name: string, heading = name) {
  await page.getByRole("menuitem", { name, exact: true }).click();
  await expect(page.getByRole("heading", { name: heading, exact: true }).first()).toBeVisible();
}

async function saveModal(page: Page) {
  await page.getByRole("dialog").getByRole("button", { name: "Lưu" }).click();
  await expect(page.getByText("Đã lưu vào bản nháp").last()).toBeVisible();
  await expect(page.getByRole("dialog")).toBeHidden();
}

async function pickOption(page: Page, label: string, optionText: string) {
  const dialog = page.getByRole("dialog");
  await dialog.getByLabel(label, { exact: true }).click();
  await page.locator(".ant-select-dropdown:visible").getByTitle(optionText).click();
}

test("đăng nhập, nhập dữ liệu, xuất bản và API công khai phục vụ dữ liệu", async ({ page, request }) => {
  const rand = Math.random().toString(36).slice(2, 8);
  const maPhuongThuc = `thi-thpt-${rand}`;
  const maNganh = rand;
  const tenNganh = `Ngành thử nghiệm ${rand}`;
  const tenPhuongThuc = `Thi THPT ${rand}`;

  // 1. Đăng nhập
  await page.goto("/login");
  await expect(page.getByRole("heading", { name: "Đăng nhập" })).toBeVisible();
  await page.getByLabel("Email").fill("admin@e2e.local");
  await page.getByLabel("Mật khẩu").fill("E2e!Passw0rd");
  await page.getByRole("button", { name: "Đăng nhập" }).click();
  await expect(page).not.toHaveURL(/\/login/);

  // 2. Phương thức
  await openMenu(page, "Phương thức");
  await page.getByRole("button", { name: "Thêm mới" }).click();
  await page.getByLabel("Mã", { exact: true }).fill(maPhuongThuc);
  await page.getByLabel("Tên", { exact: true }).fill(tenPhuongThuc);
  await page.getByLabel("Thang điểm").fill("30");
  await saveModal(page);

  // 3. Mùa tuyển sinh 2026, mùa hiện tại
  await openMenu(page, "Mùa tuyển sinh");
  await page.getByRole("button", { name: "Thêm mới" }).click();
  await page.getByLabel("Năm", { exact: true }).fill("2026");
  await page.getByLabel("Mùa hiện tại").click();
  await saveModal(page);

  // 4. Ngành
  await openMenu(page, "Ngành");
  await page.getByRole("button", { name: "Thêm mới" }).click();
  await page.getByLabel("Mã ngành").fill(maNganh);
  await page.getByLabel("Tên ngành").fill(tenNganh);
  await saveModal(page);

  // 5. Điểm chuẩn 21,5
  await openMenu(page, "Điểm chuẩn");
  await page.getByRole("button", { name: "Thêm mới" }).click();
  await pickOption(page, "Ngành", `${maNganh} – ${tenNganh}`);
  await page.getByRole("dialog").getByLabel("Năm", { exact: true }).fill("2026");
  await pickOption(page, "Phương thức", tenPhuongThuc);
  await page.getByRole("dialog").getByLabel("Điểm", { exact: true }).fill("21,5");
  await saveModal(page);

  // 6. Xuất bản
  await openMenu(page, "Xuất bản", "Thay đổi chưa xuất bản");
  await expect(page.getByText("Thêm", { exact: true }).first()).toBeVisible();
  expect(await page.locator(".ant-tag", { hasText: /^Thêm$/ }).count()).toBeGreaterThanOrEqual(4);
  await page.getByRole("button", { name: "Xuất bản" }).click();
  await page.getByRole("dialog").getByLabel("Ghi chú").fill(`e2e ${rand}`);
  await page.getByRole("dialog").getByRole("button", { name: "Đồng ý" }).click();
  await expect(page.getByText(/Đã xuất bản phiên bản #/).first()).toBeVisible();

  // 7. API công khai
  const res = await request.get(`${API}/api/public/nganh/${maNganh}`);
  expect(res.status()).toBe(200);
  const body = await res.json();
  expect(body.diemChuan[0].diem).toBe(21.5);
});
