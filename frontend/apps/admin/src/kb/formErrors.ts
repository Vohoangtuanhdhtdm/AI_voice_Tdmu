import type { FormInstance } from "antd";
import { ApiError } from "../api/client";

function entries(err: unknown): [string, string][] {
  if (!(err instanceof ApiError) || err.status !== 400) return [];
  return Object.entries(err.fieldErrors)
    .filter(([, msgs]) => msgs.length > 0)
    .map(([name, msgs]) => [name, msgs[0]]);
}

/**
 * Gắn lỗi 400 theo field vào form (chỉ các field trong `names` nếu có).
 * Trả true nếu có ít nhất một field được gắn.
 */
export function applyApiErrorToForm(form: FormInstance, err: unknown, names?: string[]): boolean {
  const fields = entries(err)
    .filter(([name]) => !names || names.includes(name))
    .map(([name, msg]) => ({ name, errors: [msg] }));
  if (fields.length === 0) return false;
  form.setFields(fields);
  return true;
}

/** Thông điệp lỗi 400 của các field không có trong form (người dùng sẽ không thấy chúng). */
export function unmatchedFieldMessages(err: unknown, names: string[]): string[] {
  return entries(err)
    .filter(([name]) => !names.includes(name))
    .map(([, msg]) => msg);
}
