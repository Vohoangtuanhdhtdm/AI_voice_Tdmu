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
