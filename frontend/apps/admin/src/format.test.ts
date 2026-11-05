import { describe, expect, it } from "vitest";
import {
  formatDateTime,
  formatDecimal,
  formatInt,
  formatMoney,
  parseViDecimal,
  parseViInt,
  parseViMoney,
} from "./format";

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
  it("formats ints with optional dot grouping", () => {
    expect(formatInt(1200)).toBe("1.200");
    expect(formatInt(2026, false)).toBe("2026");
    expect(formatInt(null)).toBe("");
  });
  it("tests run in a non-Vietnam timezone (proves TZ independence)", () => {
    expect(Intl.DateTimeFormat().resolvedOptions().timeZone).toBe("America/New_York");
    expect(new Date("2026-01-15T00:00:00Z").getTimezoneOffset()).toBe(300);
  });
  it("formats date-times in Asia/Ho_Chi_Minh", () => {
    expect(formatDateTime("2026-03-01T17:30:00Z")).toBe("02/03/2026 00:30");
  });
});

describe("parseViInt", () => {
  it("reads dots and spaces as thousands separators", () => {
    expect(parseViInt("1.200")).toBe(1200);
    expect(parseViInt("1 200")).toBe(1200);
    expect(parseViInt("2026")).toBe(2026);
    expect(parseViInt(" 12 ")).toBe(12);
  });
  it("rejects a comma (no decimals for counts)", () => {
    expect(parseViInt("1,2")).toBeNaN();
    expect(parseViInt("1,200")).toBeNaN();
  });
  it("rejects misplaced separators and non-digits", () => {
    expect(parseViInt("1.2")).toBeNaN();
    expect(parseViInt("12.00")).toBeNaN();
    expect(parseViInt("abc")).toBeNaN();
    expect(parseViInt("-5")).toBeNaN();
  });
  it("empty is null", () => {
    expect(parseViInt("")).toBeNull();
    expect(parseViInt("   ")).toBeNull();
  });
});

describe("parseViMoney", () => {
  it("strips dot, comma and space thousands separators", () => {
    expect(parseViMoney("1.200.000")).toBe(1200000);
    expect(parseViMoney("1,200,000")).toBe(1200000);
    expect(parseViMoney("1 200 000")).toBe(1200000);
    expect(parseViMoney("1200000")).toBe(1200000);
  });
  it("rejects a decimal part (VND has no decimals)", () => {
    expect(parseViMoney("1.200.000,50")).toBeNaN();
    expect(parseViMoney("1.200.000,5")).toBeNaN();
    expect(parseViMoney("1200000.50")).toBeNaN();
  });
  it("rejects non-digits", () => {
    expect(parseViMoney("12tr")).toBeNaN();
    expect(parseViMoney("-1000")).toBeNaN();
  });
  it("empty is null", () => {
    expect(parseViMoney("")).toBeNull();
  });
});

describe("parseViDecimal", () => {
  it("reads grouped dots as thousands", () => {
    expect(parseViDecimal("1.200")).toBe(1200);
    expect(parseViDecimal("1.200.000")).toBe(1200000);
  });
  it("accepts comma or dot as the decimal mark", () => {
    expect(parseViDecimal("21,5")).toBe(21.5);
    expect(parseViDecimal("21.5")).toBe(21.5);
    expect(parseViDecimal("30")).toBe(30);
  });
  it("accepts dot thousands with comma decimals", () => {
    expect(parseViDecimal("1.200,5")).toBe(1200.5);
  });
  it("rejects garbage", () => {
    expect(parseViDecimal("abc")).toBeNaN();
    expect(parseViDecimal("21,5,1")).toBeNaN();
    expect(parseViDecimal("1,200.5")).toBeNaN();
  });
  it("empty is null", () => {
    expect(parseViDecimal("")).toBeNull();
  });
});
