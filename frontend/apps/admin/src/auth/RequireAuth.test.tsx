import { afterEach, describe, expect, it } from "vitest";
import { screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { http, HttpResponse } from "msw";
import { Route, Routes } from "react-router-dom";
import { useQuery } from "@tanstack/react-query";
import { server } from "../test/server";
import { renderWithProviders } from "../test/render";
import { apiFetch } from "../api/client";
import { LoginPage } from "./LoginPage";
import { RequireAuth } from "./RequireAuth";
import { authStore } from "./authStore";

afterEach(() => {
  authStore.clear();
  sessionStorage.clear();
});

function Protected() {
  const q = useQuery({ queryKey: ["ping"], queryFn: () => apiFetch<{ ok: boolean }>("/api/admin/ping") });
  return <div>{q.isSuccess ? "Trang ngành" : "Đang tải"}</div>;
}

function renderApp(route: string) {
  return renderWithProviders(
    <Routes>
      <Route path="/login" element={<LoginPage />} />
      <Route
        path="/*"
        element={
          <RequireAuth>
            <Protected />
          </RequireAuth>
        }
      />
    </Routes>,
    { route },
  );
}

const future = () => new Date(Date.now() + 3600_000).toISOString();

describe("RequireAuth", () => {
  it("redirects to login when not authenticated", async () => {
    renderApp("/kb/nganh");
    expect(await screen.findByRole("button", { name: "Đăng nhập" })).toBeInTheDocument();
  });

  it("on 401 goes to login with expiry notice, then returns to the page after login", async () => {
    authStore.setSession({ accessToken: "old", expiresAt: future() });
    let calls = 0;
    server.use(
      http.get("/api/admin/ping", () => {
        calls += 1;
        return calls === 1
          ? HttpResponse.json({ title: "Hết hạn" }, { status: 401 })
          : HttpResponse.json({ ok: true });
      }),
      http.post("/api/auth/login", () =>
        HttpResponse.json({ accessToken: "new", expiresAt: future() }),
      ),
    );
    renderApp("/kb/nganh");
    expect(
      await screen.findByText("Phiên đăng nhập đã hết hạn, vui lòng đăng nhập lại"),
    ).toBeInTheDocument();
    expect(authStore.getToken()).toBeNull();

    const user = userEvent.setup();
    await user.type(screen.getByLabelText("Email"), "a@b.vn");
    await user.type(screen.getByLabelText("Mật khẩu"), "secret");
    await user.click(screen.getByRole("button", { name: "Đăng nhập" }));
    expect(await screen.findByText("Trang ngành")).toBeInTheDocument();
  });
});
