import dayjs from "dayjs";
import type { FieldConfig } from "./entityConfig";
import "../format"; // nạp plugin utc/timezone cho dayjs

export const VN_TZ = "Asia/Ho_Chi_Minh";

/** Hàng API -> giá trị form: ISO (UTC) -> dayjs theo giờ Việt Nam; thêm mới dùng giá trị mặc định. */
export function toFormValues(fields: FieldConfig[], row: Record<string, unknown> | null): Record<string, unknown> {
  const values: Record<string, unknown> = row ? { ...row } : {};
  for (const f of fields) {
    if (!row && f.defaultValue !== undefined) values[f.name] = f.defaultValue;
    const v = values[f.name];
    if (f.kind === "datetime" && typeof v === "string" && v) values[f.name] = dayjs(v).tz(VN_TZ);
  }
  return values;
}

/**
 * Giá trị form -> thân yêu cầu: ngày giờ lấy đúng giờ trên ô nhập, hiểu theo giờ Việt Nam,
 * gửi dạng ISO có offset (`+07:00`); không phụ thuộc múi giờ của máy.
 */
export function toRequestBody(fields: FieldConfig[], values: Record<string, unknown>): Record<string, unknown> {
  const body = { ...values };
  for (const f of fields) {
    if (f.kind !== "datetime") continue;
    const v = body[f.name];
    body[f.name] = v && dayjs.isDayjs(v) ? dayjs.tz(v.format("YYYY-MM-DD HH:mm:ss"), VN_TZ).format() : null;
  }
  return body;
}
