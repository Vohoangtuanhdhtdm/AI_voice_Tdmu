import { useMemo } from "react";
import { useKbList, type KbRow } from "../api/kb";
import type { RefKind, RefMaps, RefOptions } from "./entityConfig";

const KINDS: RefKind[] = ["nganh", "phuong-thuc", "to-hop"];

const str = (v: unknown) => (typeof v === "string" ? v : "");

/** Tên hiển thị trong bảng và nhãn trong ô chọn cho từng loại tham chiếu. */
const describe: Record<RefKind, (r: KbRow) => { name: string; label: string }> = {
  nganh: (r) => ({ name: str(r.ten), label: `${str(r.maNganh)} – ${str(r.ten)}` }),
  "phuong-thuc": (r) => ({ name: str(r.ten), label: str(r.ten) }),
  "to-hop": (r) => ({ name: str(r.ma), label: str(r.ma) }),
};

/**
 * Tải danh sách bản nháp của các loại được tham chiếu. Chỉ tải các loại trong `kinds`
 * (mặc định cả ba) để trang không gọi API thừa.
 */
export function useRefOptions(kinds: readonly RefKind[] = KINDS) {
  const q = {
    nganh: useKbList("nganh", undefined, { enabled: kinds.includes("nganh") }),
    "phuong-thuc": useKbList("phuong-thuc", undefined, { enabled: kinds.includes("phuong-thuc") }),
    "to-hop": useKbList("to-hop", undefined, { enabled: kinds.includes("to-hop") }),
  };
  const data = [q.nganh.data, q["phuong-thuc"].data, q["to-hop"].data];
  const isLoading = KINDS.some((k) => kinds.includes(k) && q[k].isLoading);

  return useMemo(() => {
    const maps = {} as RefMaps;
    const options = {} as RefOptions;
    for (const k of KINDS) {
      const rows = q[k].data ?? [];
      maps[k] = new Map();
      options[k] = [];
      for (const r of rows) {
        if (!r.logicalId) continue;
        const d = describe[k](r);
        maps[k].set(r.logicalId, d.name);
        options[k].push({ value: r.logicalId, label: d.label });
      }
    }
    return { maps, options, isLoading };
  }, [...data, isLoading]);
}
