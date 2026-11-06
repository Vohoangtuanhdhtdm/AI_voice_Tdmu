export type DonViHocPhi = "TinChi" | "Nam" | "HocKy";

interface Nguon {
  nguonTen?: string | null;
  nguonUrl?: string | null;
}

export interface MuaTuyenSinhDto extends Nguon {
  logicalId?: string | null;
  nam?: number | null;
  laHienTai?: boolean | null;
}

export interface NganhDto extends Nguon {
  logicalId?: string | null;
  maNganh?: string | null;
  ten?: string | null;
  tenGoiKhac?: string[] | null;
  donVi?: string | null;
  moTaNgan?: string | null;
  conTuyen?: boolean | null;
}

export interface PhuongThucDto extends Nguon {
  logicalId?: string | null;
  ma?: string | null;
  ten?: string | null;
  moTa?: string | null;
  thangDiem?: number | null;
}

export interface ToHopDto extends Nguon {
  logicalId?: string | null;
  ma?: string | null;
  mon?: string[] | null;
  cachDoc?: string | null;
}

export interface NganhToHopDto extends Nguon {
  logicalId?: string | null;
  nganhLid?: string | null;
  nam?: number | null;
  toHopLid?: string | null;
}

export interface DiemChuanDto extends Nguon {
  logicalId?: string | null;
  nganhLid?: string | null;
  nam?: number | null;
  phuongThucLid?: string | null;
  diem?: number | null;
  ghiChu?: string | null;
}

export interface ChiTieuDto extends Nguon {
  logicalId?: string | null;
  nganhLid?: string | null;
  nam?: number | null;
  phuongThucLid?: string | null;
  soLuong?: number | null;
}

export interface HocPhiDto extends Nguon {
  logicalId?: string | null;
  nganhLid?: string | null;
  nam?: number | null;
  muc?: number | null;
  donVi?: DonViHocPhi | null;
  ghiChu?: string | null;
}

export interface MocThoiGianDto extends Nguon {
  logicalId?: string | null;
  nam?: number | null;
  tenMoc?: string | null;
  batDau?: string | null; // ISO 8601
  ketThuc?: string | null; // ISO 8601
  phuongThucLid?: string | null;
  moTa?: string | null;
}

export interface FaqDto extends Nguon {
  logicalId?: string | null;
  cauHoi?: string | null;
  cachHoiKhac?: string[] | null;
  traLoi?: string | null;
  chuDe?: string | null;
}

export interface Release {
  id: string;
  soHieu: number;
  ghiChu: string;
  taoBoi: string;
  taoLuc: string;
  isActive: boolean;
}

export interface DiffItem {
  entity: string;
  logicalId: string;
  kind: "Added" | "Modified" | "Removed";
  label: string;
}

export interface ReleaseDiff {
  items: DiffItem[];
}

export interface ImportRow {
  rowNumber: number;
  values: Record<string, string | null>;
  errors: string[];
}

export interface ImportPreview {
  kind: string;
  rows: ImportRow[];
  validCount: number;
  errorCount: number;
}

export interface ImportResult {
  inserted: number;
  updated: number;
}

export interface LoginResponse {
  accessToken: string;
  expiresAt: string;
}
