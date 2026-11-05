import { ConfigProvider } from "antd";
import viVN from "antd/locale/vi_VN";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { BrowserRouter, Route, Routes } from "react-router-dom";
import dayjs from "dayjs";
import "dayjs/locale/vi";
import { LoginPage } from "./auth/LoginPage";
import { RequireAuth } from "./auth/RequireAuth";

dayjs.locale("vi");

export const routerFuture = { v7_startTransition: true, v7_relativeSplatPath: true } as const;

const queryClient = new QueryClient();

export function AppRoutes() {
  return (
    <Routes>
      <Route path="/login" element={<LoginPage />} />
      <Route path="/*" element={
          <RequireAuth>
            <div />
          </RequireAuth>
        } />
    </Routes>
  );
}

export default function App() {
  return (
    <ConfigProvider locale={viVN}>
      <QueryClientProvider client={queryClient}>
        <BrowserRouter future={routerFuture}>
          <AppRoutes />
        </BrowserRouter>
      </QueryClientProvider>
    </ConfigProvider>
  );
}
