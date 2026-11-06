import { authStore } from "../auth/authStore";

export class ApiError extends Error {
  readonly status: number;
  readonly title: string;
  readonly fieldErrors: Record<string, string[]>;
  readonly thamChieu: string[];
  readonly body?: unknown;

  constructor(
    status: number,
    title: string,
    fieldErrors: Record<string, string[]> = {},
    thamChieu: string[] = [],
    body?: unknown,
  ) {
    super(title);
    this.name = "ApiError";
    this.status = status;
    this.title = title;
    this.fieldErrors = fieldErrors;
    this.thamChieu = thamChieu;
    this.body = body;
  }
}

/** 401 đã được xử lý bằng chuyển về trang đăng nhập: không cần hiện thông báo lỗi riêng. */
export const isUnauthorized = (e: unknown): boolean => e instanceof ApiError && e.status === 401;

let onUnauthorized: (() => void) | null = null;

/** Đăng ký hàm chạy khi API trả 401 (phiên hết hạn). Truyền null để gỡ. */
export function setUnauthorizedHandler(fn: (() => void) | null): void {
  onUnauthorized = fn;
}

const LOGIN_PATH = "/api/auth/login";

async function toApiError(res: Response): Promise<ApiError> {
  let body: Record<string, unknown> = {};
  let rawBody: unknown;
  try {
    const parsed: unknown = await res.json();
    rawBody = parsed;
    if (parsed && typeof parsed === "object" && !Array.isArray(parsed)) {
      body = parsed as Record<string, unknown>;
    }
  } catch {
    // thân phản hồi không phải JSON
  }
  const title =
    typeof body.title === "string" && body.title.trim() !== ""
      ? body.title
      : res.status >= 500
        ? `Lỗi máy chủ (${res.status})`
        : `Có lỗi xảy ra (${res.status})`;
  const errors =
    body.errors && typeof body.errors === "object" && !Array.isArray(body.errors)
      ? (body.errors as Record<string, string[]>)
      : {};
  const thamChieu = Array.isArray(body.thamChieu) ? (body.thamChieu as string[]) : [];
  return new ApiError(res.status, title, errors, thamChieu, rawBody);
}

async function request(path: string, init: RequestInit & { json?: unknown } = {}): Promise<Response> {
  const { json, headers: initHeaders, ...rest } = init;
  const headers = new Headers(initHeaders);
  const token = authStore.getToken();
  if (token) headers.set("Authorization", `Bearer ${token}`);
  if (json !== undefined) {
    headers.set("Content-Type", "application/json");
    rest.body = JSON.stringify(json);
  }
  const res = await fetch(path, { ...rest, headers });
  if (!res.ok) {
    const err = await toApiError(res);
    // 401 của chính request đăng nhập là sai mật khẩu, không phải hết phiên.
    if (res.status === 401 && path !== LOGIN_PATH) onUnauthorized?.();
    throw err;
  }
  return res;
}

export async function apiFetch<T>(path: string, init?: RequestInit & { json?: unknown }): Promise<T> {
  const res = await request(path, init);
  if (res.status === 204) return undefined as T;
  const text = await res.text();
  return (text === "" ? undefined : JSON.parse(text)) as T;
}

export async function downloadBlob(path: string, fileName: string): Promise<void> {
  const res = await request(path);
  const url = URL.createObjectURL(await res.blob());
  const a = document.createElement("a");
  a.href = url;
  a.download = fileName;
  document.body.appendChild(a);
  a.click();
  a.remove();
  URL.revokeObjectURL(url);
}
