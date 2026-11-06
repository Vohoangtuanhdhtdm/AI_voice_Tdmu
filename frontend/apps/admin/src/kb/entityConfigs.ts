import type { KbRow } from "../api/kb";
import type {
  ChiTieuDto,
  DiemChuanDto,
  DonViHocPhi,
  FaqDto,
  HocPhiDto,
  MocThoiGianDto,
  MuaTuyenSinhDto,
  NganhDto,
  NganhToHopDto,
  PhuongThucDto,
  ToHopDto,
} from "../api/types";
import type { KbSlug } from "../layout/navigation";
import type { EntityConfig, FieldConfig, RefKind, RefMaps } from "./entityConfig";
import { formatDateTime, formatDecimal, formatInt, formatMoney } from "../format";

const DON_VI_HOC_PHI: { value: DonViHocPhi; label: string }[] = [
  { value: "TinChi", label: "Tín chỉ" },
  { value: "Nam", label: "Năm" },
  { value: "HocKy", label: "Học kỳ" },
];

/** Hai trường nguồn dữ liệu có ở mọi loại tri thức. */
const NGUON: FieldConfig[] = [
  { name: "nguonTen", label: "Tên nguồn", kind: "text" },
  { name: "nguonUrl", label: "Đường dẫn nguồn", kind: "text" },
];

const refName = (refs: RefMaps, kind: RefKind, id: unknown) =>
  typeof id === "string" ? (refs[kind].get(id) ?? "") : "";

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
    { name: "conTuyen", label: "Còn tuyển", kind: "bool", defaultValue: true },
    ...NGUON,
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
    ...NGUON,
  ],
  columns: [
    { title: "Mã", render: (r) => r.ma },
    { title: "Tên", render: (r) => r.ten },
    { title: "Thang điểm", render: (r) => formatDecimal(r.thangDiem) },
  ],
  rowLabel: (r) => r.ten ?? r.ma ?? "",
});

const muaTuyenSinh = define<MuaTuyenSinhDto & KbRow>({
  slug: "mua-tuyen-sinh",
  title: "Mùa tuyển sinh",
  yearFilter: false,
  fields: [
    { name: "nam", label: "Năm", kind: "int", grouping: false, required: true },
    { name: "laHienTai", label: "Mùa hiện tại", kind: "bool" },
    ...NGUON,
  ],
  columns: [
    { title: "Năm", render: (r) => r.nam },
    { title: "Mùa hiện tại", render: (r) => (r.laHienTai ? "Có" : "Không") },
  ],
  rowLabel: (r) => String(r.nam ?? ""),
});

const toHop = define<ToHopDto & KbRow>({
  slug: "to-hop",
  title: "Tổ hợp",
  yearFilter: false,
  fields: [
    { name: "ma", label: "Mã", kind: "text", required: true },
    { name: "mon", label: "Môn", kind: "tags" },
    { name: "cachDoc", label: "Cách đọc", kind: "text", placeholder: "Để trống để tự sinh" },
    ...NGUON,
  ],
  columns: [
    { title: "Mã", render: (r) => r.ma },
    { title: "Môn", render: (r) => r.mon?.join(", ") },
    { title: "Cách đọc", render: (r) => r.cachDoc },
  ],
  rowLabel: (r) => r.ma ?? "",
});

const nganhToHop = define<NganhToHopDto & KbRow>({
  slug: "nganh-to-hop",
  title: "Ngành – tổ hợp",
  yearFilter: true,
  fields: [
    { name: "nganhLid", label: "Ngành", kind: "ref", ref: "nganh", required: true },
    { name: "nam", label: "Năm", kind: "int", grouping: false, required: true },
    { name: "toHopLid", label: "Tổ hợp", kind: "ref", ref: "to-hop", required: true },
    ...NGUON,
  ],
  columns: [
    { title: "Ngành", render: (r, refs) => refName(refs, "nganh", r.nganhLid) },
    { title: "Năm", render: (r) => r.nam },
    { title: "Tổ hợp", render: (r, refs) => refName(refs, "to-hop", r.toHopLid) },
  ],
  rowLabel: (r, refs) => [refs && refName(refs, "nganh", r.nganhLid), refs && refName(refs, "to-hop", r.toHopLid), r.nam].filter(Boolean).join(" – "),
});

const diemChuan = define<DiemChuanDto & KbRow>({
  slug: "diem-chuan",
  title: "Điểm chuẩn",
  yearFilter: true,
  fields: [
    { name: "nganhLid", label: "Ngành", kind: "ref", ref: "nganh", required: true },
    { name: "nam", label: "Năm", kind: "int", grouping: false, required: true },
    { name: "phuongThucLid", label: "Phương thức", kind: "ref", ref: "phuong-thuc", required: true },
    { name: "diem", label: "Điểm", kind: "decimal", precision: 2, required: true },
    { name: "ghiChu", label: "Ghi chú", kind: "text" },
    ...NGUON,
  ],
  columns: [
    { title: "Ngành", render: (r, refs) => refName(refs, "nganh", r.nganhLid) },
    { title: "Năm", render: (r) => r.nam },
    { title: "Phương thức", render: (r, refs) => refName(refs, "phuong-thuc", r.phuongThucLid) },
    { title: "Điểm", render: (r) => formatDecimal(r.diem) },
    { title: "Ghi chú", render: (r) => r.ghiChu },
  ],
  rowLabel: (r, refs) => [refs && refName(refs, "nganh", r.nganhLid), refs && refName(refs, "phuong-thuc", r.phuongThucLid), r.nam].filter(Boolean).join(" – "),
});

const chiTieu = define<ChiTieuDto & KbRow>({
  slug: "chi-tieu",
  title: "Chỉ tiêu",
  yearFilter: true,
  fields: [
    { name: "nganhLid", label: "Ngành", kind: "ref", ref: "nganh", required: true },
    { name: "nam", label: "Năm", kind: "int", grouping: false, required: true },
    { name: "phuongThucLid", label: "Phương thức", kind: "ref", ref: "phuong-thuc" },
    { name: "soLuong", label: "Số lượng", kind: "int", required: true },
    ...NGUON,
  ],
  columns: [
    { title: "Ngành", render: (r, refs) => refName(refs, "nganh", r.nganhLid) },
    { title: "Năm", render: (r) => r.nam },
    { title: "Phương thức", render: (r, refs) => refName(refs, "phuong-thuc", r.phuongThucLid) },
    { title: "Số lượng", render: (r) => formatInt(r.soLuong) },
  ],
  rowLabel: (r, refs) => [refs && refName(refs, "nganh", r.nganhLid), refs && refName(refs, "phuong-thuc", r.phuongThucLid), r.nam].filter(Boolean).join(" – "),
});

const hocPhi = define<HocPhiDto & KbRow>({
  slug: "hoc-phi",
  title: "Học phí",
  yearFilter: true,
  fields: [
    { name: "nganhLid", label: "Ngành", kind: "ref", ref: "nganh", placeholder: "Áp dụng chung" },
    { name: "nam", label: "Năm", kind: "int", grouping: false, required: true },
    { name: "muc", label: "Mức", kind: "money", required: true },
    { name: "donVi", label: "Đơn vị", kind: "enum", options: DON_VI_HOC_PHI, required: true },
    { name: "ghiChu", label: "Ghi chú", kind: "text" },
    ...NGUON,
  ],
  columns: [
    { title: "Ngành", render: (r, refs) => (r.nganhLid ? refName(refs, "nganh", r.nganhLid) : "Áp dụng chung") },
    { title: "Năm", render: (r) => r.nam },
    { title: "Mức", render: (r) => formatMoney(r.muc) },
    { title: "Đơn vị", render: (r) => DON_VI_HOC_PHI.find((o) => o.value === r.donVi)?.label ?? r.donVi },
    { title: "Ghi chú", render: (r) => r.ghiChu },
  ],
  rowLabel: (r, refs) => [(refs && refName(refs, "nganh", r.nganhLid)) || "Áp dụng chung", r.nam].filter(Boolean).join(" – "),
});

const mocThoiGian = define<MocThoiGianDto & KbRow>({
  slug: "moc-thoi-gian",
  title: "Lịch tuyển sinh",
  yearFilter: true,
  fields: [
    { name: "nam", label: "Năm", kind: "int", grouping: false, required: true },
    { name: "tenMoc", label: "Tên mốc", kind: "text", required: true },
    { name: "batDau", label: "Bắt đầu", kind: "datetime", required: true },
    { name: "ketThuc", label: "Kết thúc", kind: "datetime" },
    { name: "phuongThucLid", label: "Phương thức", kind: "ref", ref: "phuong-thuc" },
    { name: "moTa", label: "Mô tả", kind: "textarea" },
    ...NGUON,
  ],
  columns: [
    { title: "Năm", render: (r) => r.nam },
    { title: "Tên mốc", render: (r) => r.tenMoc },
    { title: "Bắt đầu", render: (r) => formatDateTime(r.batDau) },
    { title: "Kết thúc", render: (r) => formatDateTime(r.ketThuc) },
    { title: "Phương thức", render: (r, refs) => refName(refs, "phuong-thuc", r.phuongThucLid) },
  ],
  rowLabel: (r) => r.tenMoc ?? "",
});

const faq = define<FaqDto & KbRow>({
  slug: "faq",
  title: "Câu hỏi thường gặp",
  yearFilter: false,
  fields: [
    { name: "cauHoi", label: "Câu hỏi", kind: "textarea", required: true },
    { name: "cachHoiKhac", label: "Cách hỏi khác", kind: "tags" },
    { name: "traLoi", label: "Trả lời", kind: "textarea", required: true },
    { name: "chuDe", label: "Chủ đề", kind: "text" },
    ...NGUON,
  ],
  columns: [
    { title: "Câu hỏi", render: (r) => r.cauHoi },
    { title: "Chủ đề", render: (r) => r.chuDe },
  ],
  rowLabel: (r) => r.cauHoi ?? "",
});

export const entityConfigs: Record<KbSlug, EntityConfig<KbRow>> = {
  "mua-tuyen-sinh": muaTuyenSinh,
  nganh,
  "phuong-thuc": phuongThuc,
  "to-hop": toHop,
  "nganh-to-hop": nganhToHop,
  "diem-chuan": diemChuan,
  "chi-tieu": chiTieu,
  "hoc-phi": hocPhi,
  "moc-thoi-gian": mocThoiGian,
  faq,
};
