import { describe, expect, it } from "vitest";
import { safeReturnTo } from "./safeReturnTo";

describe("safeReturnTo", () => {
  it.each([
    "https://evil.com",
    "//evil.com",
    "/\\evil.com",
    "/login?x",
    "/login",
    "/login/",
    "/a://b",
    "/a\\b",
    "/a\nb",
    "",
    null,
    "kb/nganh",
  ])("falls back to / for %s", (raw) => {
    expect(safeReturnTo(raw)).toBe("/");
  });

  it("keeps a safe local path", () => {
    expect(safeReturnTo("/kb/nganh?nam=2026")).toBe("/kb/nganh?nam=2026");
  });

  it("keeps paths that merely start with login letters", () => {
    expect(safeReturnTo("/loginx")).toBe("/loginx");
  });
});
