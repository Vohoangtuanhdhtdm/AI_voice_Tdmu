import type { ReactNode } from "react";
import type { KbSlug } from "../layout/navigation";

interface FieldBase {
  name: string;
  label: string;
  required?: boolean;
  /** Giá trị mặc định khi thêm mới. */
  defaultValue?: unknown;
  placeholder?: string;
}

export type RefKind = "nganh" | "phuong-thuc" | "to-hop";

export type FieldConfig =
  | (FieldBase & { kind: "text" })
  | (FieldBase & { kind: "textarea" })
  | (FieldBase & { kind: "int" })
  | (FieldBase & { kind: "decimal"; precision: number })
  | (FieldBase & { kind: "money" })
  | (FieldBase & { kind: "bool" })
  | (FieldBase & { kind: "tags" })
  | (FieldBase & { kind: "enum"; options: { value: string; label: string }[] })
  | (FieldBase & { kind: "ref"; ref: RefKind })
  | (FieldBase & { kind: "datetime" });

export type RefOptions = Record<RefKind, { value: string; label: string }[]>;

export type RefMaps = Record<RefKind, Map<string, string>>;

export interface ColumnConfig<T> {
  title: string;
  render(row: T, refs: RefMaps): ReactNode;
}

export interface EntityConfig<T> {
  slug: KbSlug;
  title: string;
  yearFilter: boolean;
  fields: FieldConfig[];
  columns: ColumnConfig<T>[];
  rowLabel(row: T): string;
}
