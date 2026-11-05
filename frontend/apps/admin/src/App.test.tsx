import { screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { AppRoutes } from "./App";
import { renderWithProviders } from "./test/render";

describe("App", () => {
  it("shows the login page at /login", () => {
    renderWithProviders(<AppRoutes />, { route: "/login" });
    expect(screen.getByRole("heading", { name: "Đăng nhập" })).toBeInTheDocument();
  });
});
