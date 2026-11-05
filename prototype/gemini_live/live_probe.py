"""Spike Gemini Live: gửi câu hỏi bằng giọng nói tiếng Việt, cho mô hình gọi công cụ, đo độ trễ.

Cách chạy:  python live_probe.py [--model gemini-3.8-live] [--only q1_diem_chuan]
Đầu ra:     out/replies/<model>/<id>.wav, out/results_<model>.json
"""

import argparse
import asyncio
import json
import os
import pathlib
import time
import wave

from dotenv import load_dotenv
from google import genai
from google.genai import types

from questions import QUESTIONS
from tools import FUNCTIONS, SYSTEM_INSTRUCTION, TOOL_DECLARATIONS

load_dotenv()
HERE = pathlib.Path(__file__).parent
CHUNK_MS = 100
CHUNK_BYTES = 16000 * 2 * CHUNK_MS // 1000  # PCM 16-bit mono 16 kHz


def build_config(voice: str) -> types.LiveConnectConfig:
    return types.LiveConnectConfig(
        response_modalities=["AUDIO"],
        system_instruction=SYSTEM_INSTRUCTION,
        tools=TOOL_DECLARATIONS,
        input_audio_transcription=types.AudioTranscriptionConfig(),
        output_audio_transcription=types.AudioTranscriptionConfig(),
        speech_config=types.SpeechConfig(voice_config=types.VoiceConfig(
            prebuilt_voice_config=types.PrebuiltVoiceConfig(voice_name=voice))),
    )


async def ask(client: genai.Client, model: str, voice: str, q: dict, out_dir: pathlib.Path) -> dict:
    pcm = (HERE / "out" / "audio" / f"{q['id']}.pcm16k").read_bytes()
    r = {"id": q["id"], "loai": q["loai"], "cau_hoi_goc": q["text"], "model": model,
         "input_transcript": "", "output_transcript": "", "tool_calls": [], "error": None}
    reply = bytearray()
    t0 = time.perf_counter()
    async with client.aio.live.connect(model=model, config=build_config(voice)) as s:
        r["connect_ms"] = round((time.perf_counter() - t0) * 1000)

        async def sender():
            # Gửi theo nhịp thời gian thực, sau đó 1 giây im lặng để VAD nhận biết hết câu.
            for i in range(0, len(pcm), CHUNK_BYTES):
                await s.send_realtime_input(audio=types.Blob(
                    data=pcm[i:i + CHUNK_BYTES], mime_type="audio/pcm;rate=16000"))
                await asyncio.sleep(CHUNK_MS / 1000)
            r["_t_end_speech"] = time.perf_counter()
            for _ in range(10):
                await s.send_realtime_input(audio=types.Blob(
                    data=b"\x00" * CHUNK_BYTES, mime_type="audio/pcm;rate=16000"))
                await asyncio.sleep(CHUNK_MS / 1000)

        send_task = asyncio.create_task(sender())
        done = False
        tool_pending = False  # đã gửi kết quả tool, đang chờ mô hình nói câu trả lời
        while not done:
            async for msg in s.receive():
                now = time.perf_counter()
                if msg.tool_call:
                    responses = []
                    for fc in msg.tool_call.function_calls:
                        t_call = time.perf_counter()
                        args = dict(fc.args or {})
                        fn = FUNCTIONS.get(fc.name)
                        result = fn(**args) if fn else {"error": "unknown tool"}
                        r["tool_calls"].append({
                            "name": fc.name, "args": args, "result": result,
                            "t_after_speech_ms": _since(r, t_call)})
                        responses.append(types.FunctionResponse(id=fc.id, name=fc.name, response=result))
                    await s.send_tool_response(function_responses=responses)
                    tool_pending = True
                sc = msg.server_content
                if not sc:
                    continue
                if sc.input_transcription and sc.input_transcription.text:
                    r["input_transcript"] += sc.input_transcription.text
                if sc.output_transcription and sc.output_transcription.text:
                    r["output_transcript"] += sc.output_transcription.text
                if sc.model_turn:
                    for p in sc.model_turn.parts:
                        if p.inline_data and p.inline_data.data:
                            if "first_audio_ms" not in r:
                                r["first_audio_ms"] = _since(r, now)
                            reply.extend(p.inline_data.data)
                            tool_pending = False
                if sc.turn_complete and not tool_pending:
                    r["turn_complete_ms"] = _since(r, now)
                    done = True
                    break
        send_task.cancel()

    r.pop("_t_end_speech", None)
    r["reply_audio_s"] = round(len(reply) / 48000, 1)  # đầu ra 24 kHz, 16-bit
    with wave.open(str(out_dir / f"{q['id']}.wav"), "wb") as w:
        w.setnchannels(1); w.setsampwidth(2); w.setframerate(24000); w.writeframes(bytes(reply))
    return r


def _since(r: dict, t: float):
    """Mili-giây tính từ lúc người dùng nói xong (âm có thể nếu mô hình phản hồi sớm)."""
    te = r.get("_t_end_speech")
    return None if te is None else round((t - te) * 1000)


async def main() -> None:
    ap = argparse.ArgumentParser()
    ap.add_argument("--model", default=os.getenv("LIVE_MODEL", "gemini-3.8-live"))
    ap.add_argument("--voice", default="Kore")
    ap.add_argument("--only")
    a = ap.parse_args()
    client = genai.Client()
    out_dir = HERE / "out" / "replies" / a.model
    out_dir.mkdir(parents=True, exist_ok=True)
    results = []
    for q in QUESTIONS:
        if a.only and q["id"] != a.only:
            continue
        try:
            res = await asyncio.wait_for(ask(client, a.model, a.voice, q, out_dir), timeout=90)
        except Exception as e:  # ghi lỗi để còn chạy tiếp các câu khác
            res = {"id": q["id"], "model": a.model, "error": f"{type(e).__name__}: {e}"}
        results.append(res)
        print(json.dumps(res, ensure_ascii=False, indent=1))
    (HERE / "out" / f"results_{a.model}.json").write_text(
        json.dumps(results, ensure_ascii=False, indent=1))


if __name__ == "__main__":
    asyncio.run(main())
