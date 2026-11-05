/** Chỉ chấp nhận đường dẫn nội bộ; mọi giá trị khác (open redirect, /login) quay về "/". */
export function safeReturnTo(raw: string | null): string {
  if (!raw || !raw.startsWith("/") || raw.startsWith("//")) return "/";
  // eslint-disable-next-line no-control-regex
  if (/[\u0000-\u001f\u007f\\]/.test(raw) || raw.includes("://")) return "/";
  if (/^\/login(?:[/?#]|$)/.test(raw)) return "/";
  return raw;
}
