import { describe, expect, it, vi } from "vitest";
import type { FormInstance } from "antd";
import { ApiError } from "../api/client";
import { applyApiErrorToForm } from "./formErrors";

const fakeForm = () => {
  const setFields = vi.fn();
  return { form: { setFields } as unknown as FormInstance, setFields };
};

describe("applyApiErrorToForm", () => {
  it("sets first message per field on 400", () => {
    const { form, setFields } = fakeForm();
    const err = new ApiError(400, "x", { thangDiem: ["a", "b"], ma: ["c"] });
    expect(applyApiErrorToForm(form, err)).toBe(true);
    expect(setFields).toHaveBeenCalledWith([
      { name: "thangDiem", errors: ["a"] },
      { name: "ma", errors: ["c"] },
    ]);
  });
  it("returns false for 400 without fields, other statuses and non-api errors", () => {
    const { form, setFields } = fakeForm();
    expect(applyApiErrorToForm(form, new ApiError(400, "x"))).toBe(false);
    expect(applyApiErrorToForm(form, new ApiError(409, "x", { a: ["b"] }))).toBe(false);
    expect(applyApiErrorToForm(form, new Error("x"))).toBe(false);
    expect(setFields).not.toHaveBeenCalled();
  });
});
