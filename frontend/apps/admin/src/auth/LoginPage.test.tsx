import { afterEach, describe, expect, it } from "vitest";
import { screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { http, HttpResponse } from "msw";
import { Route, Routes } from "react-router-dom";
import { server } from "../test/server";
import { renderWithProviders } from "../test/render";
import { LoginPage } from "./LoginPage";
import { authStore } from "./authStore";

afterEach(() => {
  authStore.clear();
  sessionStorage.clear();
});

function renderLogin(route: string) {
  return renderWithProviders(
    <Routes>
      <Route path="/login" element={<LoginPage />} />
      <Route path="/kb/nganh" element={<div>Trang ngành</div>} />
    </Routes>,
    { route },
  );
}

async function submit() {
  const user = userEvent.setup();
  await user.type(screen.getByLabelText("Email"), "a@b.vn");
  await user.type(screen.getByLabelText("Mật khẩu"), "secret");
  await user.click(screen.getByRole("button", { name: "Đăng nhập" }));
}

describe("LoginPage", () => {
  it("logs in, stores the session and goes to returnTo", async () => {
    server.use(
      http.post("/api/auth/login", () =>
        HttpResponse.json({ accessToken: "abc", expiresAt: new Date(Date.now() + 3600_000).toISOString() }),
      ),
    );
    renderLogin("/login?returnTo=%2Fkb%2Fnganh");
    await submit();
    expect(await screen.findByText("Trang ngành")).toBeInTheDocument();
    expect(sessionStorage.getItem("tdmu-admin-auth")).toContain("abc");
  });

  it("shows the wrong-credentials message on 401", async () => {
    server.use(
      http.post("/api/auth/login", () =>
        HttpResponse.json({ title: "Email hoặc mật khẩu không đúng" }, { status: 401 }),
      ),
    );
    renderLogin("/login");
    await submit();
    expect(await screen.findByText("Email hoặc mật khẩu không đúng")).toBeInTheDocument();
  });

  it("shows the server title on 423", async () => {
    server.use(
      http.post("/api/auth/login", () =>
        HttpResponse.json({ title: "Tài khoản tạm khóa do đăng nhập sai nhiều lần" }, { status: 423 }),
      ),
    );
    renderLogin("/login");
    await submit();
    expect(await screen.findByText("Tài khoản tạm khóa do đăng nhập sai nhiều lần")).toBeInTheDocument();
  });

  it("shows the expiry notice with expired=1", async () => {
    renderLogin("/login?expired=1");
    await waitFor(() =>
      expect(screen.getByText("Phiên đăng nhập đã hết hạn, vui lòng đăng nhập lại")).toBeInTheDocument(),
    );
  });

  it.each([
    ["https://evil.com", "/"],
    ["//evil.com", "/"],
    ["/\\evil.com", "/"],
    ["/login?x", "/"],
  ])("ignores unsafe returnTo %s", async (raw) => {
    server.use(
      http.post("/api/auth/login", () =>
        HttpResponse.json({ accessToken: "abc", expiresAt: new Date(Date.now() + 3600_000).toISOString() }),
      ),
    );
    renderWithProviders(
      <Routes>
        <Route path="/login" element={<LoginPage />} />
        <Route path="/" element={<div>Trang chủ</div>} />
      </Routes>,
      { route: `/login?returnTo=${encodeURIComponent(raw)}` },
    );
    await submit();
    expect(await screen.findByText("Trang chủ")).toBeInTheDocument();
  });

  it("keeps a safe returnTo with query", async () => {
    server.use(
      http.post("/api/auth/login", () =>
        HttpResponse.json({ accessToken: "abc", expiresAt: new Date(Date.now() + 3600_000).toISOString() }),
      ),
    );
    renderLogin(`/login?returnTo=${encodeURIComponent("/kb/nganh?nam=2026")}`);
    await submit();
    expect(await screen.findByText("Trang ngành")).toBeInTheDocument();
  });
});
