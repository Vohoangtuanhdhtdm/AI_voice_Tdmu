import { ConfigProvider, Typography } from "antd";
import viVN from "antd/locale/vi_VN";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { BrowserRouter, Route, Routes } from "react-router-dom";
import dayjs from "dayjs";
import "dayjs/locale/vi";

dayjs.locale("vi");

export const routerFuture = { v7_startTransition: true, v7_relativeSplatPath: true } as const;

const queryClient = new QueryClient();

export function AppRoutes() {
  return (
    <Routes>
      <Route path="/login" element={<Typography.Title level={2}>Đăng nhập</Typography.Title>} />
      <Route path="/*" element={<div />} />
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
