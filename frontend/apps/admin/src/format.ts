import dayjs from "dayjs";
import utc from "dayjs/plugin/utc";
import timezone from "dayjs/plugin/timezone";

dayjs.extend(utc);
dayjs.extend(timezone);

const decimalFmt = new Intl.NumberFormat("vi-VN", { maximumFractionDigits: 2 });

export function formatDecimal(n: number | null | undefined): string {
  return n == null ? "" : decimalFmt.format(n);
}

export function formatMoney(n: number | null | undefined): string {
  // Tự chèn dấu chấm nghìn: Intl vi-VN không nhóm số 4 chữ số.
  return n == null ? "" : group(String(Math.round(n)));
}

export function formatDateTime(iso: string | null | undefined): string {
  return iso ? dayjs(iso).tz("Asia/Ho_Chi_Minh").format("DD/MM/YYYY HH:mm") : "";
}

const group = (digits: string) => digits.replace(/\B(?=(\d{3})+(?!\d))/g, ".");

/** Số nguyên; `grouping=false` cho năm (2026, không phải 2.026). */
export function formatInt(n: number | null | undefined, grouping = true): string {
  if (n == null) return "";
  const s = String(Math.trunc(n));
  return grouping ? group(s) : s;
}

/*
 * Bộ đọc số kiểu Việt Nam cho ô nhập. Trả về: số; null nếu ô trống; NaN nếu không hợp lệ
 * (form chặn lưu, không bao giờ âm thầm cắt bớt).
 */

/** Số nguyên (năm, số lượng): "." và khoảng trắng là dấu nghìn (đúng nhóm 3 chữ số); có "," là không hợp lệ. */
export function parseViInt(s: string): number | null {
  const t = s.trim();
  if (t === "") return null;
  if (/^\d+$/.test(t)) return Number(t);
  if (/^\d{1,3}([. ]\d{3})+$/.test(t)) return Number(t.replace(/[. ]/g, ""));
  return NaN;
}

/** Tiền VND: không có phần lẻ — đuôi ",dd"/".dd" (1–2 chữ số) là không hợp lệ; ".", "," và khoảng trắng là dấu nghìn. */
export function parseViMoney(s: string): number | null {
  const t = s.trim();
  if (t === "") return null;
  if (/[,.]\d{1,2}$/.test(t)) return NaN;
  const digits = t.replace(/[.,\s]/g, "");
  return /^\d+$/.test(digits) ? Number(digits) : NaN;
}

/** Số thập phân (điểm): "1.200" là nghìn; "21,5"/"21.5" là 21,5; "1.200,5" là 1200,5. */
export function parseViDecimal(s: string): number | null {
  const t = s.trim();
  if (t === "") return null;
  if (/^\d{1,3}(\.\d{3})+$/.test(t)) return Number(t.replace(/\./g, ""));
  const m = /^(\d{1,3}(?:\.\d{3})+),(\d+)$/.exec(t);
  if (m) return Number(`${m[1].replace(/\./g, "")}.${m[2]}`);
  const u = t.replace(",", ".");
  return /^-?\d+(\.\d+)?$/.test(u) ? Number(u) : NaN;
}
