import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { apiFetch } from "./client";
import type { TaiLieuDto } from "./types";

const BASE = "/api/admin/tai-lieu";
export const POLL_MS = 3000;

export const hasPending = (docs: TaiLieuDto[] | undefined): boolean =>
  !!docs?.some((d) => d.trangThai === "ChoXuLy" || d.trangThai === "DangXuLy");

/** pollMs = 0 tắt poll (dùng trong test); mặc định 3 giây khi còn tài liệu chưa xử lý xong. */
export function useDocuments(pollMs: number = POLL_MS) {
  return useQuery({
    queryKey: ["kb", "tai-lieu"],
    queryFn: () => apiFetch<TaiLieuDto[]>(BASE),
    refetchInterval: (query) => (pollMs > 0 && hasPending(query.state.data) ? pollMs : false),
  });
}

export const uploadDocument = (form: FormData) => apiFetch<TaiLieuDto>(BASE, { method: "POST", body: form });
export const reprocessDocument = (lid: string) => apiFetch<void>(`${BASE}/${lid}/xu-ly-lai`, { method: "POST" });
export const deleteDocument = (lid: string) => apiFetch<void>(`${BASE}/${lid}`, { method: "DELETE" });

function useDocMutation<V>(fn: (v: V) => Promise<unknown>) {
  const qc = useQueryClient();
  return useMutation({ mutationFn: fn, onSuccess: () => qc.invalidateQueries({ queryKey: ["kb"] }) });
}

export const useUploadDocument = () => useDocMutation(uploadDocument);
export const useReprocessDocument = () => useDocMutation(reprocessDocument);
export const useDeleteDocument = () => useDocMutation(deleteDocument);
