import { useEffect, useRef, useState } from "react";
import { Alert, Button, Card, Input, List, Segmented, Space, Tag, Typography } from "antd";
import { isUnauthorized } from "../api/client";
import { streamChat, type ChatScope, type ChatStreamEvent } from "../api/chat";

const MAX_LEN = 1000;

const TOOL_LABELS: Record<string, string> = {
  tim_nganh: "Tìm ngành",
  tra_diem_chuan: "Tra điểm chuẩn",
  tra_chi_tieu: "Tra chỉ tiêu",
  tra_to_hop: "Tra tổ hợp",
  tra_hoc_phi: "Tra học phí",
  tra_lich_tuyen_sinh: "Tra lịch tuyển sinh",
  tim_kiem_tri_thuc: "Tìm kiếm tài liệu",
  chuyen_tiep_can_bo: "Chuyển cán bộ tư vấn",
};

const toolLabel = (name: string) => TOOL_LABELS[name] ?? name;

function isHttpUrl(url: string): boolean {
  try {
    const p = new URL(url).protocol;
    return p === "http:" || p === "https:";
  } catch {
    return false;
  }
}

type ToolCall = { name: string; state: "start" | "end" };
type Citation = { ten: string; url?: string | null };
type Turn =
  | { role: "user"; text: string }
  | { role: "assistant"; text: string; tools: ToolCall[]; citations: Citation[] };

export function PlaygroundPage() {
  const [scope, setScope] = useState<ChatScope>("draft");
  const [turns, setTurns] = useState<Turn[]>([]);
  const [input, setInput] = useState("");
  const [streaming, setStreaming] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const conversationId = useRef<string | undefined>(undefined);
  const abortRef = useRef<AbortController | null>(null);

  useEffect(() => () => abortRef.current?.abort(), []);

  const reset = () => {
    abortRef.current?.abort();
    conversationId.current = undefined;
    setTurns([]);
    setError(null);
  };

  const patchAssistant = (fn: (t: Extract<Turn, { role: "assistant" }>) => Turn) =>
    setTurns((prev) => {
      const last = prev[prev.length - 1];
      return last?.role === "assistant" ? [...prev.slice(0, -1), fn(last)] : prev;
    });

  const onEvent = (e: ChatStreamEvent, signal: AbortSignal) => {
    if (signal.aborted) return;
    switch (e.type) {
      case "delta":
        patchAssistant((t) => ({ ...t, text: t.text + e.text }));
        break;
      case "tool_status":
        patchAssistant((t) => {
          const i = t.tools.findIndex((x) => x.name === e.name && x.state === "start");
          const tools =
            e.state === "end" && i >= 0
              ? t.tools.map((x, j) => (j === i ? { ...x, state: "end" as const } : x))
              : [...t.tools, { name: e.name, state: e.state }];
          return { ...t, tools };
        });
        break;
      case "citations":
        patchAssistant((t) => ({ ...t, citations: e.items }));
        break;
      case "done":
        if (e.conversationId) conversationId.current = e.conversationId;
        break;
      case "error":
        setError(e.title);
        break;
    }
  };

  const send = async () => {
    const text = input.trim();
    if (!text || streaming) return;
    const controller = new AbortController();
    abortRef.current = controller;
    setInput("");
    setError(null);
    setStreaming(true);
    setTurns((prev) => [...prev, { role: "user", text }, { role: "assistant", text: "", tools: [], citations: [] }]);
    try {
      await streamChat({ conversationId: conversationId.current, message: text, scope }, (e) => onEvent(e, controller.signal), controller.signal);
    } catch (err) {
      if (!controller.signal.aborted && !isUnauthorized(err)) {
        // Lỗi HTTP: bỏ bong bóng trợ lý rỗng, chỉ hiện một Alert.
        setTurns((prev) => {
          const last = prev[prev.length - 1];
          return last?.role === "assistant" && last.text === "" && last.tools.length === 0 ? prev.slice(0, -1) : prev;
        });
        setError(err instanceof Error ? err.message : "Có lỗi xảy ra");
      }
    } finally {
      if (abortRef.current === controller) setStreaming(false);
    }
  };

  return (
    <Space direction="vertical" size="middle" style={{ width: "100%" }}>
      <Space wrap style={{ justifyContent: "space-between", width: "100%" }}>
        <Segmented
          value={scope}
          onChange={(v) => {
            setScope(v as ChatScope);
            reset();
            setStreaming(false);
          }}
          options={[
            { label: "Bản nháp", value: "draft" },
            { label: "Bản đang phát hành", value: "published" },
          ]}
        />
        <Button
          onClick={() => {
            reset();
            setStreaming(false);
          }}
        >
          Hội thoại mới
        </Button>
      </Space>

      <Space direction="vertical" size="small" style={{ width: "100%" }}>
        {turns.map((t, i) =>
          t.role === "user" ? (
            <div key={i} style={{ textAlign: "right" }}>
              <Card size="small" style={{ display: "inline-block", maxWidth: "80%", textAlign: "left" }}>
                <Typography.Text style={{ whiteSpace: "pre-wrap" }}>{t.text}</Typography.Text>
              </Card>
            </div>
          ) : (
            <Card key={i} size="small" style={{ maxWidth: "80%" }}>
              <Typography.Paragraph style={{ whiteSpace: "pre-wrap", marginBottom: 0 }}>{t.text}</Typography.Paragraph>
              {t.citations.length > 0 && (
                <Space wrap style={{ marginTop: 8 }}>
                  {t.citations.map((c, j) =>
                    c.url && isHttpUrl(c.url) ? (
                      <Tag key={j}>
                        <a href={c.url} target="_blank" rel="noopener noreferrer">
                          {c.ten}
                        </a>
                      </Tag>
                    ) : (
                      <Tag key={j}>{c.ten}</Tag>
                    ),
                  )}
                </Space>
              )}
              {t.tools.length > 0 && (
                <div style={{ marginTop: 8 }}>
                  <Typography.Text type="secondary">Công cụ đã gọi</Typography.Text>
                  <List
                    size="small"
                    dataSource={t.tools}
                    renderItem={(x) => (
                      <List.Item>
                        {toolLabel(x.name)}{" "}
                        <Tag color={x.state === "end" ? "green" : "processing"}>
                          {x.state === "end" ? "xong" : "đang chạy"}
                        </Tag>
                      </List.Item>
                    )}
                  />
                </div>
              )}
            </Card>
          ),
        )}
      </Space>

      {error && <Alert type="error" showIcon message={error} />}

      <div>
        <Input.TextArea
          value={input}
          maxLength={MAX_LEN}
          autoSize={{ minRows: 2, maxRows: 6 }}
          placeholder="Nhập câu hỏi thử…"
          aria-label="Câu hỏi"
          onChange={(e) => setInput(e.target.value)}
          onPressEnter={(e) => {
            if (e.shiftKey || e.nativeEvent.isComposing) return;
            e.preventDefault();
            void send();
          }}
        />
        <Space style={{ marginTop: 8, justifyContent: "space-between", width: "100%" }}>
          <Typography.Text type="secondary">{`${input.length}/${MAX_LEN}`}</Typography.Text>
          <Space>
            {streaming && <Button onClick={() => abortRef.current?.abort()}>Dừng</Button>}
            <Button type="primary" disabled={streaming || input.trim() === ""} onClick={() => void send()}>
              Gửi
            </Button>
          </Space>
        </Space>
      </div>
    </Space>
  );
}
