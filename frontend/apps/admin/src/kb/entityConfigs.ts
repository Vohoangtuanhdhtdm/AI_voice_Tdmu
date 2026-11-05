import type { KbRow } from "../api/kb";
import type { NganhDto, PhuongThucDto } from "../api/types";
import type { KbSlug } from "../layout/navigation";
import type { EntityConfig } from "./entityConfig";
import { formatDecimal } from "../format";

/** Ép cấu hình theo DTO cụ thể về dạng hàng chung để đưa vào registry. */
function define<T extends KbRow>(c: EntityConfig<T>): EntityConfig<KbRow> {
  return c as unknown as EntityConfig<KbRow>;
}

const nganh = define<NganhDto & KbRow>({
  slug: "nganh",
  title: "Ngành",
  yearFilter: false,
  fields: [
    { name: "maNganh", label: "Mã ngành", kind: "text", required: true },
    { name: "ten", label: "Tên ngành", kind: "text", required: true },
    { name: "tenGoiKhac", label: "Tên gọi khác", kind: "tags" },
    { name: "donVi", label: "Đơn vị", kind: "text" },
    { name: "moTaNgan", label: "Mô tả ngắn", kind: "textarea" },
    { name: "conTuyen", label: "Còn tuyển", kind: "bool" },
  ],
  columns: [
    { title: "Mã ngành", render: (r) => r.maNganh },
    { title: "Tên ngành", render: (r) => r.ten },
    { title: "Đơn vị", render: (r) => r.donVi },
    { title: "Còn tuyển", render: (r) => (r.conTuyen ? "Có" : "Không") },
  ],
  rowLabel: (r) => r.ten ?? r.maNganh ?? "",
});

const phuongThuc = define<PhuongThucDto & KbRow>({
  slug: "phuong-thuc",
  title: "Phương thức",
  yearFilter: false,
  fields: [
    { name: "ma", label: "Mã", kind: "text", required: true },
    { name: "ten", label: "Tên", kind: "text", required: true },
    { name: "moTa", label: "Mô tả", kind: "textarea" },
    { name: "thangDiem", label: "Thang điểm", kind: "decimal", precision: 2, required: true },
  ],
  columns: [
    { title: "Mã", render: (r) => r.ma },
    { title: "Tên", render: (r) => r.ten },
    { title: "Thang điểm", render: (r) => formatDecimal(r.thangDiem) },
  ],
  rowLabel: (r) => r.ten ?? r.ma ?? "",
});

export const entityConfigs: Partial<Record<KbSlug, EntityConfig<KbRow>>> = {
  nganh,
  "phuong-thuc": phuongThuc,
};
