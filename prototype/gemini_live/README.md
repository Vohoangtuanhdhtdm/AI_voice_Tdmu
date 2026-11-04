# Spike Gemini Live: trợ lý tuyển sinh bằng giọng nói

Bản thử nghiệm kỹ thuật (Giai đoạn 2 trong `docs/KE_HOACH_THUC_HIEN.md`) để kiểm tra:
- Gemini Live có nghe và nói tốt tiếng Việt không.
- Mô hình có gọi đúng công cụ tra cứu không.
- Độ trễ ra sao.
- Cách đọc mã tổ hợp và số thập phân.

> ⚠️ Số liệu trong `tools.py` là **DỮ LIỆU MẪU**, không phải số liệu tuyển sinh thật của TDMU.

## Cài đặt

```bash
cd prototype/gemini_live
pip install -r requirements.txt
cp ../../.env.example .env   # điền GEMINI_API_KEY; KHÔNG commit .env
```

## Chạy

```bash
python make_test_audio.py                       # tạo giọng nói tiếng Việt cho bộ câu hỏi (Gemini TTS)
python live_probe.py --model gemini-3.8-live    # gửi audio vào Live API, gọi tool, đo độ trễ
```

Kết quả nằm trong `out/` (đã gitignore):
- `out/results_<model>.json`: transcript hai chiều, các lần gọi tool, độ trễ.
- `out/replies/<model>/*.wav`: câu trả lời bằng giọng nói, để nghe lại và chấm điểm.

## Các file

| File | Vai trò |
|---|---|
| `questions.py` | 5 câu thử: số liệu, tổ hợp, khẩu ngữ/viết tắt, không có dữ liệu, ngoài phạm vi |
| `tools.py` | Công cụ tra cứu (dữ liệu mẫu), khai báo function cho Gemini, system instruction |
| `make_test_audio.py` | Tổng hợp giọng hỏi bằng Gemini TTS, xuất PCM 16 kHz |
| `live_probe.py` | Mở phiên Live, gửi audio theo nhịp thời gian thực, xử lý `tool_call`, ghi transcript và độ trễ |

## Ghi chú kỹ thuật
- Đầu vào: PCM 16-bit, 16 kHz, mono. Đầu ra: PCM 24 kHz.
- Độ trễ đo từ lúc gửi xong câu nói. Con số này đã gồm thời gian VAD chờ im lặng để xác định người dùng nói xong.
- Sau khi gửi `tool_response`, mô hình mở một lượt mới để nói câu trả lời, nên không được dừng ở `turn_complete` của lượt gọi tool.
- Gói miễn phí giới hạn số request/phút (TTS: 3 RPM), nên `make_test_audio.py` có retry.

## Kết quả lần chạy 04/11/2026 (5 câu, audio do TTS tạo, gói miễn phí)

| Model | Gọi tool đúng | Hành vi đúng (5 câu) | Độ trễ đến âm thanh đầu tiên* | Ghi chú |
|---|---|---|---|---|
| `gemini-3.8-live` | 4/4 | 5/5 | 0,9 – 2,3 s (trung vị ~1,6 s) | Đọc “D không một”, “21 phẩy 5” đúng theo `cach_doc`; từ chối câu ngoài phạm vi, không tự chuyển cán bộ |
| `gemini-3.1-flash-live-preview` | 4/4 | 5/5 | 1,1 – 1,8 s (có 1 lần 11 s) | Hiển thị “D01” trong transcript; một lần độ trễ bất thường |
| `gemini-2.5-flash-native-audio-latest` | 3/4 | 4/5 | 2,9 – 9,4 s | **q3 tự trả lời tổ hợp mà không gọi tool**: đúng loại rủi ro “bịa” cần chặn |

\* Đo từ lúc gửi xong câu nói, đã gồm thời gian VAD chờ im lặng.

**Nhận dạng tiếng Việt:** transcript đầu vào gần như đúng hoàn toàn ở cả ba model, kể cả khẩu ngữ (“dzậy cô”), viết tắt (“IT”) và “THPT”. Vẫn cần kiểm tra lại bằng giọng người thật thuộc ba miền và có tạp âm.

**Bài học khi thiết kế tool** (đã sửa trong `tools.py`):
1. Tham số dạng danh mục phải dùng `enum` và chuẩn hóa trong tool. Lần đầu mô hình truyền “thi tốt nghiệp THPT”, không khớp khóa nên công cụ trả về “chưa có dữ liệu”.
2. Kết quả “không tìm thấy” phải ghi rõ là “không có trong cơ sở dữ liệu”. Lần đầu mô hình suy ra thành “trường không đào tạo”.
3. Phải mô tả rõ khi nào được chuyển cho cán bộ. Lần đầu mô hình tự chuyển cả câu hỏi bài tập toán.

**Khuyến nghị:** chọn `gemini-3.8-live` làm model chính, `gemini-3.1-flash-live-preview` làm dự phòng. Không dùng dòng 2.5 native audio.
