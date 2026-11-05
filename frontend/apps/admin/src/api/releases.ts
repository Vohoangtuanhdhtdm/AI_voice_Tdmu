import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { ApiError, apiFetch } from "./client";
import type { DiffItem, Release, ReleaseDiff } from "./types";

const base = "/api/admin/releases";

export function useReleaseDiff() {
  return useQuery({
    queryKey: ["kb", "diff"],
    queryFn: async () => (await apiFetch<ReleaseDiff>(`${base}/diff`)).items as DiffItem[],
  });
}

export function useReleases() {
  return useQuery({ queryKey: ["releases"], queryFn: () => apiFetch<Release[]>(base) });
}

function useInvalidateAll() {
  const qc = useQueryClient();
  return () =>
    Promise.all([
      qc.invalidateQueries({ queryKey: ["releases"] }),
      qc.invalidateQueries({ queryKey: ["kb", "diff"] }),
      qc.invalidateQueries({ queryKey: ["kb"] }),
    ]);
}

export function usePublish() {
  const invalidate = useInvalidateAll();
  return useMutation({
    mutationFn: (ghiChu: string) => apiFetch<Release>(base, { method: "POST", json: { ghiChu } }),
    onSuccess: invalidate,
    // 409: bản nháp thực ra không còn thay đổi; làm mới để trang phản ánh đúng.
    onError: (e) => {
      if (e instanceof ApiError && e.status === 409) void invalidate();
    },
  });
}

export function useActivate() {
  const invalidate = useInvalidateAll();
  return useMutation({
    mutationFn: (id: string) => apiFetch<void>(`${base}/${id}/activate`, { method: "POST" }),
    onSuccess: invalidate,
  });
}

export function useRestoreToDraft() {
  const invalidate = useInvalidateAll();
  return useMutation({
    mutationFn: (id: string) => apiFetch<void>(`${base}/${id}/restore-to-draft`, { method: "POST" }),
    onSuccess: invalidate,
  });
}
