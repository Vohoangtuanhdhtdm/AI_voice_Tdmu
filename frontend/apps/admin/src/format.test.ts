import { describe, expect, it } from "vitest";
import { formatDateTime, formatDecimal, formatMoney, parseViNumber } from "./format";

describe("format", () => {
  it("formats decimals with vi-VN comma, max 2 digits", () => {
    expect(formatDecimal(21.5)).toBe("21,5");
    expect(formatDecimal(21.456)).toBe("21,46");
    expect(formatDecimal(null)).toBe("");
  });
  it("formats money with dot thousands separator", () => {
    expect(formatMoney(1200000)).toBe("1.200.000");
    expect(formatMoney(5000)).toBe("5.000");
  });
  it("formats date-times in Asia/Ho_Chi_Minh", () => {
    expect(formatDateTime("2026-03-01T17:30:00Z")).toBe("02/03/2026 00:30");
  });
  it("parses vi-VN numbers", () => {
    expect(parseViNumber("21,5")).toBe(21.5);
    expect(parseViNumber("21.5")).toBe(21.5);
    expect(parseViNumber("abc")).toBeNull();
  });
});
