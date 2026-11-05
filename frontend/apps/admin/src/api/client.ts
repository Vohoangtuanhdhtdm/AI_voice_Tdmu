import { authStore } from "../auth/authStore";

export class ApiError extends Error {
  readonly status: number;
  readonly title: string;
  readonly fieldErrors: Record<string, string[]>;
  readonly thamChieu: string[];

  constructor(
    status: number,
    title: string,
    fieldErrors: Record<string, string[]> = {},
    thamChieu: string[] = [],
  ) {
    super(title);
    this.name = "ApiError";
    this.status = status;
    this.title = title;
    this.fieldErrors = fieldErrors;
    this.thamChieu = thamChieu;
  }
}

let onUnauthorized: (() => void) | null = null;

/** Đăng ký hàm chạy khi API trả 401 (phiên hết hạn). Truyền null để gỡ. */
export function setUnauthorizedHandler(fn: (() => void) | null): void {
  onUnauthorized = fn;
}

const LOGIN_PATH = "/api/auth/login";

async function toApiError(res: Response): Promise<ApiError> {
  let body: { title?: string; errors?: Record<string, string[]>; thamChieu?: string[] } = {};
  try {
    body = (await res.json()) as typeof body;
  } catch {
    // thân phản hồi không phải JSON
  }
  return new ApiError(
    res.status,
    body.title ?? res.statusText ?? "Đã xảy ra lỗi",
    body.errors ?? {},
    body.thamChieu ?? [],
  );
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
  return (await res.json()) as T;
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
