import { apiRequest } from "./client";

export type ChatScope = "draft" | "published";

export type ChatStreamEvent =
  | { type: "delta"; text: string }
  | { type: "tool_status"; name: string; state: "start" | "end" }
  | { type: "citations"; items: { ten: string; url?: string | null }[] }
  | { type: "done"; conversationId: string | null; messageId: string | null }
  | { type: "error"; title: string };

/** Parse một khối SSE (các dòng giữa hai dòng trống); trả null nếu không hợp lệ. */
function parseBlock(block: string): ChatStreamEvent | null {
  let name = "";
  const data: string[] = [];
  for (const line of block.split("\n")) {
    if (line.startsWith("event:")) name = line.slice(6).trim();
    else if (line.startsWith("data:")) data.push(line.slice(5).replace(/^ /, ""));
  }
  if (data.length === 0) return null;
  let d: unknown;
  try {
    d = JSON.parse(data.join("\n"));
  } catch {
    return null;
  }
  if (!d || typeof d !== "object") return null;
  const o = d as Record<string, unknown>;
  const idOrNull = (v: unknown) => (typeof v === "string" ? v : null);
  switch (name) {
    case "delta":
      return typeof o.text === "string" ? { type: "delta", text: o.text } : null;
    case "tool_status":
      return typeof o.name === "string" && (o.state === "start" || o.state === "end")
        ? { type: "tool_status", name: o.name, state: o.state }
        : null;
    case "citations":
      return Array.isArray(o.items)
        ? {
            type: "citations",
            items: o.items
              .filter((x): x is { ten: string; url?: unknown } => !!x && typeof (x as { ten?: unknown }).ten === "string")
              .map((x) => ({ ten: x.ten, url: typeof x.url === "string" ? x.url : null })),
          }
        : null;
    case "done":
      return { type: "done", conversationId: idOrNull(o.conversationId), messageId: idOrNull(o.messageId) };
    case "error":
      return typeof o.title === "string" ? { type: "error", title: o.title } : null;
    default:
      return null;
  }
}

export async function streamChat(
  body: { conversationId?: string; message: string; scope: ChatScope },
  onEvent: (e: ChatStreamEvent) => void,
  signal?: AbortSignal,
): Promise<void> {
  // apiRequest gắn Bearer, ném ApiError cho non-2xx và xử lý 401 như apiFetch.
  const res = await apiRequest("/api/chat", { method: "POST", json: body, signal });
  if (!res.body) return;
  const reader = res.body.getReader();
  // Hủy luôn luồng đọc: không phụ thuộc môi trường có tự đóng body khi fetch bị abort hay không.
  const onAbort = () => void reader.cancel().catch(() => {});
  if (signal?.aborted) onAbort();
  else signal?.addEventListener("abort", onAbort, { once: true });
  const decoder = new TextDecoder();
  let buffer = "";
  const flush = (final: boolean) => {
    // Giữ lại "\r" cuối chuỗi: có thể là nửa đầu của "\r\n" nằm ở mảnh sau.
    const hold = !final && buffer.endsWith("\r") ? "\r" : "";
    if (hold) buffer = buffer.slice(0, -1);
    buffer = buffer.replace(/\r\n?/g, "\n");
    let idx: number;
    while ((idx = buffer.indexOf("\n\n")) >= 0) {
      const ev = parseBlock(buffer.slice(0, idx));
      buffer = buffer.slice(idx + 2);
      if (ev) onEvent(ev);
    }
    buffer += hold;
    if (final && buffer.trim() !== "") {
      const ev = parseBlock(buffer);
      buffer = "";
      if (ev) onEvent(ev);
    }
  };
  try {
    for (;;) {
      const { done, value } = await reader.read();
      if (done) break;
      buffer += decoder.decode(value, { stream: true });
      flush(false);
    }
  } catch (err) {
    if (!signal?.aborted) throw err;
  } finally {
    signal?.removeEventListener("abort", onAbort);
  }
  if (signal?.aborted) return;
  buffer += decoder.decode();
  flush(true);
}
