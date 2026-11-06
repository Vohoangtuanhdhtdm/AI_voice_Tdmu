// Ghim múi giờ khác Việt Nam (trước mọi lần dùng Date/dayjs) để chứng minh hiển thị/nhập giờ
// không phụ thuộc múi giờ máy.
process.env.TZ = "America/New_York";

import "@testing-library/jest-dom/vitest";
import { afterAll, afterEach, beforeAll } from "vitest";
import { cleanup } from "@testing-library/react";
import { server } from "./src/test/server";

Object.defineProperty(window, "matchMedia", {
  writable: true,
  value: (query: string) => ({
    matches: false,
    media: query,
    onchange: null,
    addListener: () => {},
    removeListener: () => {},
    addEventListener: () => {},
    removeEventListener: () => {},
    dispatchEvent: () => false,
  }),
});

// jsdom không hỗ trợ getComputedStyle với pseudo-element (antd Modal gọi khi khóa cuộn).
const realGetComputedStyle = window.getComputedStyle.bind(window);
window.getComputedStyle = (elt: Element) => realGetComputedStyle(elt);

beforeAll(() => server.listen({ onUnhandledRequest: "error" }));
afterEach(() => {
  cleanup();
  server.resetHandlers();
});
afterAll(() => server.close());
