export const KB_SLUGS = [
  "mua-tuyen-sinh",
  "nganh",
  "phuong-thuc",
  "to-hop",
  "nganh-to-hop",
  "diem-chuan",
  "chi-tieu",
  "hoc-phi",
  "moc-thoi-gian",
  "faq",
] as const;

export type KbSlug = (typeof KB_SLUGS)[number];

export const KB_TITLES: Record<KbSlug, string> = {
  "mua-tuyen-sinh": "Mùa tuyển sinh",
  nganh: "Ngành",
  "phuong-thuc": "Phương thức",
  "to-hop": "Tổ hợp",
  "nganh-to-hop": "Ngành – tổ hợp",
  "diem-chuan": "Điểm chuẩn",
  "chi-tieu": "Chỉ tiêu",
  "hoc-phi": "Học phí",
  "moc-thoi-gian": "Lịch tuyển sinh",
  faq: "Câu hỏi thường gặp",
};

export const KB_MENU_GROUPS: { title: string; slugs: KbSlug[] }[] = [
  { title: "Danh mục", slugs: ["mua-tuyen-sinh", "nganh", "phuong-thuc", "to-hop"] },
  { title: "Số liệu", slugs: ["nganh-to-hop", "diem-chuan", "chi-tieu", "hoc-phi"] },
];

export const KB_UNGROUPED: KbSlug[] = ["moc-thoi-gian", "faq"];

export function isKbSlug(s: string | undefined): s is KbSlug {
  return (KB_SLUGS as readonly string[]).includes(s ?? "");
}
