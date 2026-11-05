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
  return n == null ? "" : String(Math.round(n)).replace(/\B(?=(\d{3})+(?!\d))/g, ".");
}

export function formatDateTime(iso: string | null | undefined): string {
  return iso ? dayjs(iso).tz("Asia/Ho_Chi_Minh").format("DD/MM/YYYY HH:mm") : "";
}

/** Chấp nhận "21,5" và "21.5"; trả null nếu không phải số. */
export function parseViNumber(s: string): number | null {
  const t = s.trim().replace(",", ".");
  return /^-?\d+(\.\d+)?$/.test(t) ? Number(t) : null;
}
