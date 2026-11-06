import { useEffect, useRef, type ReactNode } from "react";
import { Navigate, useLocation } from "react-router-dom";
import { useQueryClient } from "@tanstack/react-query";
import { setUnauthorizedHandler } from "../api/client";
import { authStore, useAuth } from "./authStore";

export function RequireAuth({ children }: { children: ReactNode }) {
  const { isAuthenticated } = useAuth();
  const location = useLocation();
  const expired = useRef(false);
  const queryClient = useQueryClient();

  useEffect(() => {
    setUnauthorizedHandler(() => {
      // clear() làm isAuthenticated=false; lần render đó redirect kèm expired=1.
      expired.current = true;
      queryClient.clear();
      authStore.clear();
    });
    return () => setUnauthorizedHandler(null);
  }, [queryClient]);

  if (!isAuthenticated) {
    const returnTo = encodeURIComponent(location.pathname + location.search);
    return <Navigate to={`/login?returnTo=${returnTo}${expired.current ? "&expired=1" : ""}`} replace />;
  }
  return <>{children}</>;
}
