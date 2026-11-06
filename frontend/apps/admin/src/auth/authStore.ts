import { useCallback, useSyncExternalStore } from "react";
import type { LoginResponse } from "../api/types";

const KEY = "tdmu-admin-auth";
const listeners = new Set<() => void>();

function notify() {
  listeners.forEach((fn) => fn());
}

function read(): LoginResponse | null {
  try {
    const raw = sessionStorage.getItem(KEY);
    if (!raw) return null;
    const s = JSON.parse(raw) as Partial<LoginResponse>;
    return typeof s.accessToken === "string" && typeof s.expiresAt === "string"
      ? { accessToken: s.accessToken, expiresAt: s.expiresAt }
      : null;
  } catch {
    return null;
  }
}

export const authStore = {
  getToken(): string | null {
    const s = read();
    if (!s) return null;
    const exp = Date.parse(s.expiresAt);
    return Number.isFinite(exp) && exp > Date.now() ? s.accessToken : null;
  },
  setSession(r: LoginResponse): void {
    sessionStorage.setItem(KEY, JSON.stringify({ accessToken: r.accessToken, expiresAt: r.expiresAt }));
    notify();
  },
  clear(): void {
    sessionStorage.removeItem(KEY);
    notify();
  },
  subscribe(fn: () => void): () => void {
    listeners.add(fn);
    return () => {
      listeners.delete(fn);
    };
  },
};

export function useAuth(): { isAuthenticated: boolean; logout(): void } {
  const isAuthenticated = useSyncExternalStore(
    authStore.subscribe,
    () => authStore.getToken() !== null,
  );
  const logout = useCallback(() => authStore.clear(), []);
  return { isAuthenticated, logout };
}
