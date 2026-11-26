import { afterEach, beforeEach, describe, expect, it } from "vitest";
import { http, HttpResponse } from "msw";
import { server } from "../test/server";
import { authStore } from "../auth/authStore";
import { ApiError } from "./client";
import { streamChat, type ChatStreamEvent } from "./chat";

beforeEach(() => {
  authStore.setSession({ accessToken: "tok", expiresAt: new Date(Date.now() + 3600_000).toISOString() });
});
afterEach(() => {
  authStore.clear();
  sessionStorage.clear();
});

function sseResponse(chunks: string[]) {
  const enc = new TextEncoder();
  return new HttpResponse(
    new ReadableStream({
      start(c) {
        for (const ch of chunks) c.enqueue(enc.encode(ch));
        c.close();
      },
    }),
    { headers: { "Content-Type": "text/event-stream" } },
  );
}

const body = { message: "hi", scope: "draft" as const };

describe("streamChat", () => {
  it("Parses_events_split_at_arbitrary_chunk_boundaries", async () => {
    const full =
      'event: delta\ndata: {"text":"Xin "}\n\n' +
      'event: tool_status\ndata: {"name":"tra_diem_chuan","state":"start"}\n\n' +
      'event: delta\ndata: {"text":"chào"}\n\n' +
      'event: citations\ndata: {"items":[{"ten":"Đề án","url":null}]}\n\n' +
      'event: done\ndata: {"conversationId":"c1","messageId":null}\n\n';
    let auth: string | null = null;
    server.use(
      http.post("/api/chat", ({ request }) => {
        auth = request.headers.get("Authorization");
        // cắt giữa dòng và giữa ký tự nhiều byte
        const bytes = new TextEncoder().encode(full);
        const parts: Uint8Array[] = [];
        for (let i = 0; i < bytes.length; i += 7) parts.push(bytes.slice(i, i + 7));
        return new HttpResponse(
          new ReadableStream({
            start(c) {
              parts.forEach((p) => c.enqueue(p));
              c.close();
            },
          }),
          { headers: { "Content-Type": "text/event-stream" } },
        );
      }),
    );
    const events: ChatStreamEvent[] = [];
    await streamChat(body, (e) => events.push(e));
    expect(auth).toBe("Bearer tok");
    expect(events).toEqual([
      { type: "delta", text: "Xin " },
      { type: "tool_status", name: "tra_diem_chuan", state: "start" },
      { type: "delta", text: "chào" },
      { type: "citations", items: [{ ten: "Đề án", url: null }] },
      { type: "done", conversationId: "c1", messageId: null },
    ]);
  });

  it("Handles_CRLF_and_trailing_event_without_blank_line", async () => {
    server.use(
      http.post("/api/chat", () =>
        sseResponse(['event: delta\r\ndata: {"text":"a"}\r\n\r\nevent: error\r\ndata: {"title":"Lỗi"}']),
      ),
    );
    const events: ChatStreamEvent[] = [];
    await streamChat(body, (e) => events.push(e));
    expect(events).toEqual([
      { type: "delta", text: "a" },
      { type: "error", title: "Lỗi" },
    ]);
  });

  it("Throws_ApiError_with_server_title_on_403", async () => {
    server.use(http.post("/api/chat", () => HttpResponse.json({ title: "Cần quyền quản trị" }, { status: 403 })));
    const err = await streamChat(body, () => {}).catch((e: unknown) => e);
    expect(err).toBeInstanceOf(ApiError);
    expect((err as ApiError).status).toBe(403);
    expect((err as ApiError).title).toBe("Cần quyền quản trị");
  });

  it("CR_at_chunk_end_followed_by_LF_is_not_a_blank_line", async () => {
    // "\r" | "\n" tách mảnh: phải là một "\r\n", không thành "\n\n" cắt giữa khối.
    server.use(
      http.post("/api/chat", () =>
        sseResponse(['event: delta\r', '\ndata: {"text":"x"}\r', '\n\r', '\n']),
      ),
    );
    const events: ChatStreamEvent[] = [];
    await streamChat(body, (e) => events.push(e));
    expect(events).toEqual([{ type: "delta", text: "x" }]);
  });

  it("Ignores_wrong_shapes_and_payload_type_cannot_override_event_name", async () => {
    server.use(
      http.post("/api/chat", () =>
        sseResponse([
          'event: delta\ndata: {"foo":1}\n\n',
          'event: delta\ndata: {"text":5}\n\n',
          'event: delta\ndata: {"type":"error","title":"x","text":"ok"}\n\n',
        ]),
      ),
    );
    const events: ChatStreamEvent[] = [];
    await streamChat(body, (e) => events.push(e));
    expect(events).toEqual([{ type: "delta", text: "ok" }]);
  });
});
