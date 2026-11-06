import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { apiFetch } from "./client";

export interface KbRow {
  logicalId?: string | null;
  [field: string]: unknown;
}

const base = (slug: string) => `/api/admin/kb/${slug}`;

export function useKbList(slug: string, nam?: number, opts: { enabled?: boolean } = {}) {
  return useQuery({
    enabled: opts.enabled ?? true,
    queryKey: ["kb", slug, { nam: nam ?? null }],
    queryFn: () => apiFetch<KbRow[]>(nam == null ? base(slug) : `${base(slug)}?nam=${nam}`),
  });
}

function useInvalidate(slug: string) {
  const qc = useQueryClient();
  return () =>
    Promise.all([
      qc.invalidateQueries({ queryKey: ["kb", slug] }),
      qc.invalidateQueries({ queryKey: ["kb", "diff"] }),
    ]);
}

export function useKbCreate(slug: string) {
  const invalidate = useInvalidate(slug);
  return useMutation({
    mutationFn: (body: Record<string, unknown>) => apiFetch<KbRow>(base(slug), { method: "POST", json: body }),
    onSuccess: invalidate,
  });
}

export function useKbUpdate(slug: string) {
  const invalidate = useInvalidate(slug);
  return useMutation({
    mutationFn: ({ logicalId, body }: { logicalId: string; body: Record<string, unknown> }) =>
      apiFetch<KbRow>(`${base(slug)}/${logicalId}`, { method: "PUT", json: body }),
    onSuccess: invalidate,
  });
}

export function useKbDelete(slug: string) {
  const invalidate = useInvalidate(slug);
  return useMutation({
    mutationFn: (logicalId: string) => apiFetch<void>(`${base(slug)}/${logicalId}`, { method: "DELETE" }),
    onSuccess: invalidate,
  });
}
