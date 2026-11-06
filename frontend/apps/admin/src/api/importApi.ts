import { apiFetch } from "./client";
import type { ImportPreview, ImportResult } from "./types";

export type ImportKind = "diem-chuan" | "chi-tieu" | "hoc-phi" | "nganh-to-hop";

export const importKindLabels: Record<ImportKind, string> = {
  "diem-chuan": "Điểm chuẩn",
  "chi-tieu": "Chỉ tiêu",
  "hoc-phi": "Học phí",
  "nganh-to-hop": "Ngành – tổ hợp",
};

function send<T>(kind: ImportKind, action: "preview" | "commit", file: File): Promise<T> {
  const body = new FormData();
  body.append("file", file);
  return apiFetch<T>(`/api/admin/import/${kind}/${action}`, { method: "POST", body });
}

export const previewImport = (kind: ImportKind, file: File) => send<ImportPreview>(kind, "preview", file);
export const commitImport = (kind: ImportKind, file: File) => send<ImportResult>(kind, "commit", file);
