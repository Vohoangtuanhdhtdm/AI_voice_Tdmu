import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { configure, fireEvent, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { http, HttpResponse } from "msw";
import { server } from "../test/server";
import { renderWithProviders } from "../test/render";
import { authStore } from "../auth/authStore";
import { Route, Routes } from "react-router-dom";
import { LoginPage } from "../auth/LoginPage";
import { RequireAuth } from "../auth/RequireAuth";
import { PlaygroundPage } from "./PlaygroundPage";

vi.setConfig({ testTimeout: 15000 });
// Mặc định 1s của findBy/waitFor quá chặt khi cả bộ test chạy song song và máy bận.
configure({ asyncUtilTimeout: 5000 });

// Gõ từng ký tự bằng userEvent rất chậm với antd TextArea dưới tải; đặt giá trị trực tiếp cho xác định.
const typeText = (text: string) => fireEvent.change(screen.getByRole("textbox"), { target: { value: text } });

beforeEach(() => {
  authStore.setSession({ accessToken: "t", expiresAt: new Date(Date.now() + 3600_000).toISOString() });
});
afterEach(() => {
  authStore.clear();
  sessionStorage.clear();
});

const enc = new TextEncoder();
const ev = (name: string, data: unknown) => enc.encode(`event: ${name}\ndata: ${JSON.stringify(data)}\n\n`);

function sse(chunks: Uint8Array[]) {
  return new HttpResponse(
    new ReadableStream({
      start(c) {
        chunks.forEach((x) => c.enqueue(x));
        c.close();
      },
    }),
    { headers: { "Content-Type": "text/event-stream" } },
  );
}

async function ask(text: string) {
  const user = userEvent.setup();
  typeText(text);
  await user.click(screen.getByRole("button", { name: "Gửi" }));
  return user;
}

describe("PlaygroundPage", () => {
  it("Streams_answer_tools_and_citations", async () => {
    server.use(
      http.post("/api/chat", () =>
        sse([
          ev("tool_status", { name: "tra_diem_chuan", state: "start" }),
          ev("tool_status", { name: "tra_diem_chuan", state: "end" }),
          ev("delta", { text: "Điểm chuẩn " }),
          ev("delta", { text: "là 25." }),
          ev("citations", { items: [{ ten: "Đề án 2026", url: "https://tdmu.edu.vn/de-an" }, { ten: "Không link", url: null }] }),
          ev("done", { conversationId: "c1", messageId: "m1" }),
        ]),
      ),
    );
    renderWithProviders(<PlaygroundPage />);
    await ask("Điểm chuẩn CNTT?");
    expect(await screen.findByText("Điểm chuẩn là 25.")).toBeInTheDocument();
    expect(screen.getByText("Điểm chuẩn CNTT?")).toBeInTheDocument();
    expect(screen.getByText("Công cụ đã gọi")).toBeInTheDocument();
    expect(screen.getByText("Tra điểm chuẩn")).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "Đề án 2026" })).toHaveAttribute("href", "https://tdmu.edu.vn/de-an");
    expect(screen.getByText("Không link")).toBeInTheDocument();
    await waitFor(() => expect(screen.queryByRole("button", { name: "Dừng" })).not.toBeInTheDocument());
  });

  it("Sends_draft_scope_by_default_and_reuses_conversation_id", async () => {
    const bodies: Record<string, unknown>[] = [];
    server.use(
      http.post("/api/chat", async ({ request }) => {
        bodies.push((await request.json()) as Record<string, unknown>);
        return sse([ev("delta", { text: "ok" }), ev("done", { conversationId: "c9", messageId: null })]);
      }),
    );
    renderWithProviders(<PlaygroundPage />);
    const user = await ask("một");
    await waitFor(() => expect(screen.queryByRole("button", { name: "Dừng" })).not.toBeInTheDocument());
    typeText("hai");
    await user.click(screen.getByRole("button", { name: "Gửi" }));
    await waitFor(() => expect(bodies).toHaveLength(2));
    expect(bodies[0]).toEqual({ message: "một", scope: "draft" });
    expect(bodies[1]).toEqual({ message: "hai", scope: "draft", conversationId: "c9" });

    // đổi phạm vi => hội thoại mới
    await waitFor(() => expect(screen.queryByRole("button", { name: "Dừng" })).not.toBeInTheDocument());
    await user.click(screen.getByText("Bản đang phát hành"));
    expect(screen.queryByText("một")).not.toBeInTheDocument();
    typeText("ba");
    await user.click(screen.getByRole("button", { name: "Gửi" }));
    await waitFor(() => expect(bodies).toHaveLength(3));
    expect(bodies[2]).toEqual({ message: "ba", scope: "published" });
  });

  it("New_conversation_clears_messages_and_conversation_id", async () => {
    const bodies: Record<string, unknown>[] = [];
    server.use(
      http.post("/api/chat", async ({ request }) => {
        bodies.push((await request.json()) as Record<string, unknown>);
        return sse([ev("delta", { text: "ok" }), ev("done", { conversationId: "c9", messageId: null })]);
      }),
    );
    renderWithProviders(<PlaygroundPage />);
    const user = await ask("một");
    await waitFor(() => expect(screen.queryByRole("button", { name: "Dừng" })).not.toBeInTheDocument());
    await user.click(screen.getByRole("button", { name: "Hội thoại mới" }));
    expect(screen.queryByText("một")).not.toBeInTheDocument();
    typeText("hai");
    await user.click(screen.getByRole("button", { name: "Gửi" }));
    await waitFor(() => expect(bodies).toHaveLength(2));
    expect(bodies[1]).toEqual({ message: "hai", scope: "draft" });
  });

  it("Error_event_shows_alert_and_keeps_partial_text", async () => {
    server.use(
      http.post("/api/chat", () =>
        sse([ev("delta", { text: "Một phần" }), ev("error", { title: "Hệ thống đang bận" }), ev("done", { conversationId: null, messageId: null })]),
      ),
    );
    renderWithProviders(<PlaygroundPage />);
    await ask("hỏi");
    expect(await screen.findByText("Hệ thống đang bận")).toBeInTheDocument();
    expect(screen.getByText("Một phần")).toBeInTheDocument();
  });

  it("Http_error_shows_alert_with_server_title", async () => {
    server.use(http.post("/api/chat", () => HttpResponse.json({ title: "Quá nhiều yêu cầu" }, { status: 429 })));
    renderWithProviders(<PlaygroundPage />);
    await ask("hỏi");
    expect(await screen.findByText("Quá nhiều yêu cầu")).toBeInTheDocument();
  });

  it("Stop_aborts_stream", async () => {
    let aborted = false;
    server.use(
      http.post("/api/chat", ({ request }) => {
        request.signal.addEventListener("abort", () => (aborted = true));
        return new HttpResponse(
          new ReadableStream({
            start(c) {
              c.enqueue(ev("delta", { text: "đang trả lời" }));
            },
          }),
          { headers: { "Content-Type": "text/event-stream" } },
        );
      }),
    );
    renderWithProviders(<PlaygroundPage />);
    const user = await ask("hỏi");
    expect(await screen.findByText("đang trả lời")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Gửi" })).toBeDisabled();
    await user.click(screen.getByRole("button", { name: "Dừng" }));
    await waitFor(() => expect(screen.queryByRole("button", { name: "Dừng" })).not.toBeInTheDocument());
    expect(screen.getByText("đang trả lời")).toBeInTheDocument();
    expect(screen.queryByRole("alert")).not.toBeInTheDocument();
    await waitFor(() => expect(aborted).toBe(true));
  });

  it("Message_limit_1000", async () => {
    renderWithProviders(<PlaygroundPage />);
    const box = screen.getByRole("textbox") as HTMLTextAreaElement;
    expect(box).toHaveAttribute("maxlength", "1000");
    expect(screen.getByText("0/1000")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Gửi" })).toBeDisabled();
    typeText("abc");
    expect(screen.getByText("3/1000")).toBeInTheDocument();
  });

  it("Shows_friendly_labels_for_all_tools_and_raw_name_fallback", async () => {
    const names = ["tim_nganh","tra_diem_chuan","tra_chi_tieu","tra_to_hop","tra_hoc_phi","tra_lich_tuyen_sinh","tim_kiem_tri_thuc","chuyen_tiep_can_bo","cong_cu_la"];
    server.use(
      http.post("/api/chat", () =>
        sse([...names.map((n) => ev("tool_status", { name: n, state: "start" })), ev("done", { conversationId: null, messageId: null })]),
      ),
    );
    renderWithProviders(<PlaygroundPage />);
    await ask("hỏi");
    for (const l of ["Tìm ngành","Tra điểm chuẩn","Tra chỉ tiêu","Tra tổ hợp","Tra học phí","Tra lịch tuyển sinh","Tìm kiếm tài liệu","Chuyển cán bộ tư vấn","cong_cu_la"])
      expect(await screen.findByText(l)).toBeInTheDocument();
  });

  it("Non_http_citation_url_is_not_a_link", async () => {
    server.use(
      http.post("/api/chat", () =>
        sse([
          ev("citations", { items: [{ ten: "Xấu", url: "javascript:alert(1)" }, { ten: "Hỏng", url: "not a url" }, { ten: "Tốt", url: "https://a.vn/x" }] }),
          ev("done", { conversationId: null, messageId: null }),
        ]),
      ),
    );
    renderWithProviders(<PlaygroundPage />);
    await ask("hỏi");
    expect(await screen.findByText("Xấu")).toBeInTheDocument();
    expect(screen.getByText("Hỏng")).toBeInTheDocument();
    expect(screen.queryByRole("link", { name: "Xấu" })).not.toBeInTheDocument();
    expect(screen.queryByRole("link", { name: "Hỏng" })).not.toBeInTheDocument();
    const ok = screen.getByRole("link", { name: "Tốt" });
    expect(ok).toHaveAttribute("rel", "noopener noreferrer");
    expect(ok).toHaveAttribute("target", "_blank");
  });

  it("Http_error_removes_empty_assistant_bubble_and_shows_single_error", async () => {
    server.use(http.post("/api/chat", () => HttpResponse.json({ title: "Quá nhiều yêu cầu" }, { status: 429 })));
    const { container } = renderWithProviders(<PlaygroundPage />);
    await ask("hỏi");
    expect(await screen.findByText("Quá nhiều yêu cầu")).toBeInTheDocument();
    expect(screen.getAllByText("Quá nhiều yêu cầu")).toHaveLength(1);
    expect(container.querySelectorAll(".ant-card")).toHaveLength(1); // chỉ bong bóng người hỏi
  });

  it("Late_events_from_superseded_stream_are_ignored", async () => {
    let first: ReadableStreamDefaultController<Uint8Array> | undefined;
    let n = 0;
    server.use(
      http.post("/api/chat", () => {
        if (n++ === 0)
          return new HttpResponse(new ReadableStream({ start(c) { first = c; } }), { headers: { "Content-Type": "text/event-stream" } });
        return sse([ev("delta", { text: "mới" }), ev("done", { conversationId: null, messageId: null })]);
      }),
    );
    renderWithProviders(<PlaygroundPage />);
    const user = await ask("một");
    await user.click(screen.getByRole("button", { name: "Hội thoại mới" }));
    first?.enqueue(ev("delta", { text: "MUỘN" }));
    typeText("hai");
    await user.click(screen.getByRole("button", { name: "Gửi" }));
    expect(await screen.findByText("mới")).toBeInTheDocument();
    expect(screen.queryByText(/MUỘN/)).not.toBeInTheDocument();
  });

  it("Expired_session_401_goes_to_login_with_expiry_notice", async () => {
    server.use(
      http.post("/api/chat", () => HttpResponse.json({ title: "Phiên đăng nhập đã hết hạn" }, { status: 401 })),
    );
    renderWithProviders(
      <Routes>
        <Route path="/login" element={<LoginPage />} />
        <Route
          path="/*"
          element={
            <RequireAuth>
              <PlaygroundPage />
            </RequireAuth>
          }
        />
      </Routes>,
      { route: "/playground" },
    );
    await ask("hỏi");
    expect(await screen.findByText("Phiên đăng nhập đã hết hạn, vui lòng đăng nhập lại")).toBeInTheDocument();
    expect(authStore.getToken()).toBeNull();
    expect(screen.queryByRole("button", { name: "Gửi" })).not.toBeInTheDocument();
  });
});

