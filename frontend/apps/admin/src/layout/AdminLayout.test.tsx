import { afterEach, describe, expect, it } from "vitest";
import { screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { useLocation } from "react-router-dom";
import { http, HttpResponse } from "msw";
import { server } from "../test/server";
import { renderWithProviders } from "../test/render";
import { AppRoutes } from "../App";
import { authStore } from "../auth/authStore";

afterEach(() => {
  authStore.clear();
  sessionStorage.clear();
});

function Where() {
  return <div data-testid="where">{useLocation().pathname}</div>;
}

function renderAuthed(route: string) {
  server.use(http.get("/api/admin/kb/:slug", () => HttpResponse.json([])));
  authStore.setSession({ accessToken: "t", expiresAt: new Date(Date.now() + 3600_000).toISOString() });
  return renderWithProviders(
    <>
      <AppRoutes />
      <Where />
    </>,
    { route },
  );
}

describe("AdminLayout", () => {
  it("redirects / to /kb/nganh and selects the Ngành menu item", async () => {
    renderAuthed("/");
    expect(await screen.findByText("Quản trị tri thức tuyển sinh TDMU")).toBeInTheDocument();
    expect(screen.getByTestId("where")).toHaveTextContent("/kb/nganh");
    const item = screen.getByRole("menuitem", { name: "Ngành" });
    expect(item).toHaveClass("ant-menu-item-selected");
    expect(screen.getByText("Danh mục")).toBeInTheDocument();
    expect(screen.getByText("Số liệu")).toBeInTheDocument();
  });

  it("navigates when a menu item is clicked", async () => {
    renderAuthed("/kb/nganh");
    await userEvent.setup().click(await screen.findByRole("menuitem", { name: "Điểm chuẩn" }));
    expect(screen.getByTestId("where")).toHaveTextContent("/kb/diem-chuan");
  });

  it("logs out and returns to login", async () => {
    const { queryClient } = renderAuthed("/kb/nganh");
    queryClient.setQueryData(["kb", "khac"], [{ logicalId: "x" }]);
    await userEvent.setup().click(await screen.findByRole("button", { name: "Đăng xuất" }));
    expect(await screen.findByRole("button", { name: "Đăng nhập" })).toBeInTheDocument();
    expect(sessionStorage.getItem("tdmu-admin-auth")).toBeNull();
    // Dữ liệu của phiên trước không còn trong cache.
    expect(queryClient.getQueryCache().getAll()).toHaveLength(0);
  });

  it("shows not-found for unknown slug", async () => {
    renderAuthed("/kb/khong-co");
    expect(await screen.findByText("Không tìm thấy trang")).toBeInTheDocument();
  });
});
