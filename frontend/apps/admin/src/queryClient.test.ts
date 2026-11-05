import { describe, expect, it } from "vitest";
import { ApiError } from "./api/client";
import { shouldRetry } from "./queryClient";

describe("shouldRetry", () => {
  it("retries network errors and 5xx at most twice", () => {
    expect(shouldRetry(0, new TypeError("Failed to fetch"))).toBe(true);
    expect(shouldRetry(1, new ApiError(503, "x"))).toBe(true);
    expect(shouldRetry(2, new ApiError(500, "x"))).toBe(false);
  });
  it("never retries 4xx", () => {
    expect(shouldRetry(0, new ApiError(401, "x"))).toBe(false);
    expect(shouldRetry(0, new ApiError(404, "x"))).toBe(false);
    expect(shouldRetry(0, new ApiError(409, "x"))).toBe(false);
  });
});
