import { ConfigProvider } from "antd";
import viVN from "antd/locale/vi_VN";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { BrowserRouter, Navigate, Route, Routes, useParams } from "react-router-dom";
import dayjs from "dayjs";
import "dayjs/locale/vi";
import { LoginPage } from "./auth/LoginPage";
import { RequireAuth } from "./auth/RequireAuth";
import { AdminLayout, KbPlaceholder, NotFound } from "./layout/AdminLayout";
import { KbEntityPage } from "./kb/KbEntityPage";
import { entityConfigs } from "./kb/entityConfigs";
import { isKbSlug } from "./layout/navigation";

dayjs.locale("vi");

export const routerFuture = { v7_startTransition: true, v7_relativeSplatPath: true } as const;

const queryClient = new QueryClient();

function KbRoute() {
  const { slug } = useParams();
  const config = isKbSlug(slug) ? entityConfigs[slug] : undefined;
  // key buộc dựng lại trang (và reset state) khi đổi slug.
  return config ? <KbEntityPage key={slug} config={config} /> : <KbPlaceholder />;
}

export function AppRoutes() {
  return (
    <Routes>
      <Route path="/login" element={<LoginPage />} />
      <Route element={<RequireAuth><AdminLayout /></RequireAuth>}>
        <Route path="/" element={<Navigate to="/kb/nganh" replace />} />
        <Route path="/kb/:slug" element={<KbRoute />} />
        <Route path="/import" element={<div>Nhập dữ liệu</div>} />
        <Route path="/releases" element={<div>Xuất bản</div>} />
        <Route path="*" element={<NotFound />} />
      </Route>
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
