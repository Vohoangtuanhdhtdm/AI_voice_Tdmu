import { afterEach, describe, expect, it } from "vitest";
import { http, HttpResponse } from "msw";
import { server } from "../test/server";
import { ApiError, apiFetch } from "./client";
import { authStore } from "../auth/authStore";

const problem = (body: object, status: number) =>
  HttpResponse.json(body, { status, headers: { "Content-Type": "application/problem+json" } });

afterEach(() => authStore.clear());

describe("apiFetch", () => {
  it("maps 400 field errors", async () => {
    server.use(
      http.post("/api/x", () =>
        problem({ title: "Dữ liệu không hợp lệ", errors: { diem: ["Điểm là bắt buộc"] } }, 400),
      ),
    );
    const err = await apiFetch("/api/x", { method: "POST", json: {} }).catch((e: unknown) => e);
    expect(err).toBeInstanceOf(ApiError);
    expect((err as ApiError).status).toBe(400);
    expect((err as ApiError).fieldErrors.diem[0]).toBe("Điểm là bắt buộc");
  });

  it("maps 409 thamChieu", async () => {
    const ref = "Điểm chuẩn Công nghệ thông tin 2026 – thi THPT";
    server.use(http.delete("/api/x", () => problem({ title: "Đang được tham chiếu", thamChieu: [ref] }, 409)));
    const err = (await apiFetch("/api/x", { method: "DELETE" }).catch((e: unknown) => e)) as ApiError;
    expect(err.thamChieu).toEqual([ref]);
    expect(err.title).toBe("Đang được tham chiếu");
  });

  it("returns undefined on 204", async () => {
    server.use(http.delete("/api/x", () => new HttpResponse(null, { status: 204 })));
    await expect(apiFetch("/api/x", { method: "DELETE" })).resolves.toBeUndefined();
  });

  it("sends Authorization with a valid token and serializes json", async () => {
    authStore.setSession({ accessToken: "tok", expiresAt: new Date(Date.now() + 60_000).toISOString() });
    let auth: string | null = null;
    let ct: string | null = null;
    let body: unknown;
    server.use(
      http.post("/api/x", async ({ request }) => {
        auth = request.headers.get("Authorization");
        ct = request.headers.get("Content-Type");
        body = await request.json();
        return HttpResponse.json({ ok: true });
      }),
    );
    await apiFetch("/api/x", { method: "POST", json: { a: 1 } });
    expect(auth).toBe("Bearer tok");
    expect(ct).toBe("application/json");
    expect(body).toEqual({ a: 1 });
  });

  it("omits Authorization when the token is expired", async () => {
    authStore.setSession({ accessToken: "tok", expiresAt: new Date(Date.now() - 1000).toISOString() });
    let auth: string | null = "unset";
    server.use(
      http.get("/api/x", ({ request }) => {
        auth = request.headers.get("Authorization");
        return HttpResponse.json({});
      }),
    );
    await apiFetch("/api/x");
    expect(auth).toBeNull();
  });

  it("gives a non-empty Vietnamese title for a 502 HTML body", async () => {
    server.use(http.get("/api/x", () => new HttpResponse("<html>Bad Gateway</html>", { status: 502, statusText: "" })));
    const err = (await apiFetch("/api/x").catch((e: unknown) => e)) as ApiError;
    expect(err).toBeInstanceOf(ApiError);
    expect(err.status).toBe(502);
    expect(err.title).toBe("Lỗi máy chủ (502)");
  });

  it("tolerates non-object JSON and non-string title", async () => {
    server.use(http.get("/api/x", () => HttpResponse.json({ title: 5, errors: [] }, { status: 400 })));
    const err = (await apiFetch("/api/x").catch((e: unknown) => e)) as ApiError;
    expect(err.title).toBe("Có lỗi xảy ra (400)");
    expect(err.fieldErrors).toEqual({});
    server.use(http.get("/api/y", () => HttpResponse.json(null, { status: 409 })));
    const err2 = (await apiFetch("/api/y").catch((e: unknown) => e)) as ApiError;
    expect(err2.title).toBe("Có lỗi xảy ra (409)");
  });

  it("returns undefined for a 2xx with empty body", async () => {
    server.use(http.post("/api/x", () => new HttpResponse("", { status: 200 })));
    await expect(apiFetch("/api/x", { method: "POST" })).resolves.toBeUndefined();
  });

  it("sends FormData without a JSON Content-Type", async () => {
    let ct: string | null = null;
    server.use(
      http.post("/api/x", ({ request }) => {
        ct = request.headers.get("Content-Type");
        return HttpResponse.json({});
      }),
    );
    const fd = new FormData();
    fd.append("file", new Blob(["a"]), "a.csv");
    await apiFetch("/api/x", { method: "POST", body: fd });
    expect(ct).toMatch(/^multipart\/form-data/);
  });
});
