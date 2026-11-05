"""Tạo file giọng nói tiếng Việt cho bộ câu hỏi bằng Gemini TTS (thay cho thu âm thật).

Đầu ra: out/audio/<id>.wav (24 kHz) và <id>.pcm16k (PCM 16-bit, 16 kHz, mono) để đưa vào Live API.
"""

import os
import pathlib
import re
import time
import wave

import numpy as np
from dotenv import load_dotenv
from google import genai
from google.genai import types

from questions import QUESTIONS

load_dotenv()
OUT = pathlib.Path(__file__).parent / "out" / "audio"
OUT.mkdir(parents=True, exist_ok=True)
MODEL = os.getenv("TTS_MODEL", "gemini-3.8-flash-tts")
VOICES = ["Kore", "Puck"]  # đổi giọng giữa các câu cho đa dạng


def resample(pcm: bytes, src: int, dst: int) -> bytes:
    x = np.frombuffer(pcm, dtype=np.int16).astype(np.float32)
    n = int(len(x) * dst / src)
    y = np.interp(np.linspace(0, len(x) - 1, n), np.arange(len(x)), x)
    return y.astype(np.int16).tobytes()


def _with_retry(call, tries: int = 6):
    """Gói miễn phí giới hạn số request/phút: chờ theo retryDelay rồi thử lại."""
    for k in range(tries):
        try:
            return call()
        except (genai.errors.ClientError, genai.errors.ServerError) as e:
            if e.code not in (429, 500, 503) or k == tries - 1:
                raise
            m = re.search(r"retry in ([\d.]+)s", str(e))
            time.sleep(float(m.group(1)) + 1 if m else 30)


def main() -> None:
    client = genai.Client()
    for i, q in enumerate(QUESTIONS):
        if (OUT / f"{q['id']}.pcm16k").exists():
            continue
        resp = _with_retry(lambda: client.models.generate_content(
            model=MODEL,
            contents=q["text"],  # chỉ đưa đúng câu hỏi, không kèm hướng dẫn (TTS sẽ đọc cả hướng dẫn)
            config=types.GenerateContentConfig(
                response_modalities=["AUDIO"],
                speech_config=types.SpeechConfig(voice_config=types.VoiceConfig(
                    prebuilt_voice_config=types.PrebuiltVoiceConfig(voice_name=VOICES[i % 2]))),
            ),
        ))
        pcm24 = resp.candidates[0].content.parts[0].inline_data.data
        with wave.open(str(OUT / f"{q['id']}.wav"), "wb") as w:
            w.setnchannels(1); w.setsampwidth(2); w.setframerate(24000); w.writeframes(pcm24)
        (OUT / f"{q['id']}.pcm16k").write_bytes(resample(pcm24, 24000, 16000))
        print(f"{q['id']}: {len(pcm24) / 48000:.1f}s")


if __name__ == "__main__":
    main()
