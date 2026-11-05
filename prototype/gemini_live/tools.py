"""Công cụ tra cứu dùng cho bản thử nghiệm Gemini Live.

CẢNH BÁO: toàn bộ số liệu dưới đây là DỮ LIỆU MẪU để thử kỹ thuật,
KHÔNG phải số liệu tuyển sinh thật của Trường Đại học Thủ Dầu Một.
"""

from google.genai import types

SAMPLE_NOTE = "DỮ LIỆU MẪU dùng để thử nghiệm, không phải số liệu chính thức."

# Tên ngành chuẩn và các cách gọi khác (phục vụ khớp tên ngành).
NGANH_ALIASES = {
    "Công nghệ thông tin": ["cntt", "it", "công nghệ thông tin", "cong nghe thong tin"],
    "Kỹ thuật phần mềm": ["ktpm", "kỹ thuật phần mềm", "phần mềm"],
    "Ngôn ngữ Anh": ["ngôn ngữ anh", "tiếng anh", "anh văn"],
}

_DIEM_CHUAN = {  # (ngành, năm, phương thức) -> điểm  [DỮ LIỆU MẪU]
    ("Công nghệ thông tin", 2025, "thi THPT"): 21.5,
    ("Kỹ thuật phần mềm", 2025, "thi THPT"): 20.75,
    ("Ngôn ngữ Anh", 2025, "thi THPT"): 22.0,
}
_TO_HOP = {  # [DỮ LIỆU MẪU]
    "Công nghệ thông tin": ["A00", "A01", "D01"],
    "Kỹ thuật phần mềm": ["A00", "A01", "D01"],
    "Ngôn ngữ Anh": ["D01", "D14", "D15"],
}


def _chuan_hoa_nganh(nganh: str) -> str | None:
    q = (nganh or "").strip().lower()
    for chuan, aliases in NGANH_ALIASES.items():
        if q == chuan.lower() or any(a in q for a in aliases):
            return chuan
    return None


PHUONG_THUC = ["thi THPT", "học bạ", "đánh giá năng lực"]


def _chuan_hoa_phuong_thuc(pt: str) -> str:
    q = (pt or "").lower()
    if "học bạ" in q or "hoc ba" in q:
        return "học bạ"
    if "năng lực" in q or "dgnl" in q or "đgnl" in q:
        return "đánh giá năng lực"
    return "thi THPT"  # mặc định: điểm thi tốt nghiệp THPT


def _doc_to_hop(ma: str) -> str:
    so = {"0": "không", "1": "một", "2": "hai", "3": "ba", "4": "bốn",
          "5": "năm", "6": "sáu", "7": "bảy", "8": "tám", "9": "chín"}
    return ma[0] + " " + " ".join(so[c] for c in ma[1:])


def _khong_tim_thay(nganh: str) -> dict:
    return {"ket_qua": "khong_tim_thay_nganh", "nganh_hoi": nganh,
            "huong_dan": ("Không tìm thấy ngành này trong CƠ SỞ DỮ LIỆU của trợ lý. Điều này KHÔNG có nghĩa "
                          "là trường không đào tạo ngành này. Hãy nói rõ là chưa có thông tin và đề nghị "
                          "người dùng xem danh mục ngành trên website hoặc hỏi cán bộ tư vấn.")}


def tra_diem_chuan(nganh: str, nam: int = 2025, phuong_thuc: str = "thi THPT") -> dict:
    chuan = _chuan_hoa_nganh(nganh)
    if not chuan:
        return _khong_tim_thay(nganh)
    phuong_thuc = _chuan_hoa_phuong_thuc(phuong_thuc)
    diem = _DIEM_CHUAN.get((chuan, int(nam), phuong_thuc))
    if diem is None:
        return {"ket_qua": "chua_co_du_lieu", "nganh": chuan, "nam": nam,
                "phuong_thuc": phuong_thuc}
    return {"ket_qua": "ok", "nganh": chuan, "nam": nam, "phuong_thuc": phuong_thuc,
            "diem_chuan": diem, "cach_doc": f"{str(diem).replace('.', ' phẩy ')} điểm",
            "nguon": "Thông báo điểm chuẩn (mẫu)", "ghi_chu": SAMPLE_NOTE}


def tra_to_hop(nganh: str) -> dict:
    chuan = _chuan_hoa_nganh(nganh)
    if not chuan:
        return _khong_tim_thay(nganh)
    th = _TO_HOP[chuan]
    return {"ket_qua": "ok", "nganh": chuan, "to_hop": th,
            "cach_doc": [_doc_to_hop(x) for x in th], "ghi_chu": SAMPLE_NOTE}


def chuyen_tiep_can_bo(ly_do: str, lien_he: str = "") -> dict:
    return {"ket_qua": "da_tao_yeu_cau", "ma_yeu_cau": "TEST-0001",
            "thong_bao": "Cán bộ tuyển sinh sẽ liên hệ lại trong giờ hành chính."}


FUNCTIONS = {f.__name__: f for f in (tra_diem_chuan, tra_to_hop, chuyen_tiep_can_bo)}

TOOL_DECLARATIONS = [types.Tool(function_declarations=[
    types.FunctionDeclaration(
        name="tra_diem_chuan",
        description="Tra điểm chuẩn trúng tuyển của một ngành theo năm và phương thức xét tuyển.",
        parameters={"type": "object", "properties": {
            "nganh": {"type": "string", "description": "Tên ngành, có thể viết tắt (vd. CNTT)"},
            "nam": {"type": "integer", "description": "Năm tuyển sinh, mặc định 2025"},
            "phuong_thuc": {"type": "string", "enum": PHUONG_THUC,
                            "description": "Phương thức xét tuyển; 'thi THPT' là điểm thi tốt nghiệp THPT (mặc định)"},
        }, "required": ["nganh"]},
    ),
    types.FunctionDeclaration(
        name="tra_to_hop",
        description="Tra các tổ hợp môn xét tuyển của một ngành.",
        parameters={"type": "object", "properties": {
            "nganh": {"type": "string"}}, "required": ["nganh"]},
    ),
    types.FunctionDeclaration(
        name="chuyen_tiep_can_bo",
        description=("Tạo yêu cầu để cán bộ tuyển sinh liên hệ lại. CHỈ gọi khi câu hỏi liên quan tuyển sinh "
                     "mà trợ lý không có dữ liệu, VÀ người dùng đã đồng ý được chuyển tiếp."),
        parameters={"type": "object", "properties": {
            "ly_do": {"type": "string"}, "lien_he": {"type": "string"}},
            "required": ["ly_do"]},
    ),
])]

SYSTEM_INSTRUCTION = """Bạn là trợ lý tư vấn tuyển sinh của Trường Đại học Thủ Dầu Một, nói tiếng Việt thân thiện, ngắn gọn (2–4 câu).
Quy tắc bắt buộc:
- Mọi con số (điểm chuẩn, chỉ tiêu, học phí, ngày tháng) PHẢI lấy từ kết quả công cụ. Không có kết quả công cụ thì không được nêu số.
- Khi đọc số và mã tổ hợp, dùng trường 'cach_doc' nếu công cụ trả về.
- Nếu kết quả có ghi chú là dữ liệu mẫu, hãy nói rõ đây là dữ liệu thử nghiệm.
- Chỉ khẳng định điều có trong kết quả công cụ. Công cụ không tìm thấy thì nói "mình chưa có thông tin", không suy ra "trường không có".
- Câu hỏi không liên quan tuyển sinh (bài tập, chuyện ngoài lề): từ chối lịch sự, KHÔNG chuyển cho cán bộ.
- Câu hỏi tuyển sinh mà không có dữ liệu: hỏi người dùng có muốn để lại thông tin để cán bộ liên hệ không; chỉ gọi chuyen_tiep_can_bo khi họ đồng ý.
"""
