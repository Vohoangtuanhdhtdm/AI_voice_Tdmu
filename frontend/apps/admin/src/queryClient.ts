import { QueryClient } from "@tanstack/react-query";
import { ApiError } from "./api/client";

/** Chỉ thử lại lỗi mạng/5xx, tối đa 2 lần; lỗi 4xx (401, 404, 409...) không thử lại. */
export const shouldRetry = (failureCount: number, error: unknown): boolean =>
  !(error instanceof ApiError && error.status < 500) && failureCount < 2;

export function createQueryClient(): QueryClient {
  return new QueryClient({ defaultOptions: { queries: { retry: shouldRetry } } });
}
