import type { FormInstance } from "antd";
import { ApiError } from "../api/client";

/** Gắn lỗi 400 theo field vào form. Trả true nếu có ít nhất một field được gắn. */
export function applyApiErrorToForm(form: FormInstance, err: unknown): boolean {
  if (!(err instanceof ApiError) || err.status !== 400) return false;
  const fields = Object.entries(err.fieldErrors)
    .filter(([, msgs]) => msgs.length > 0)
    .map(([name, msgs]) => ({ name, errors: [msgs[0]] }));
  if (fields.length === 0) return false;
  form.setFields(fields);
  return true;
}
