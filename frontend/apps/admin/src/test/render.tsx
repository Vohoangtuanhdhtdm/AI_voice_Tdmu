import type { ReactElement } from "react";
import { render, type RenderResult } from "@testing-library/react";
import { ConfigProvider } from "antd";
import viVN from "antd/locale/vi_VN";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { MemoryRouter } from "react-router-dom";
import { routerFuture } from "../App";

export function renderWithProviders(
  ui: ReactElement,
  opts: { route?: string } = {},
): RenderResult {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <ConfigProvider locale={viVN}>
      <QueryClientProvider client={client}>
        <MemoryRouter initialEntries={[opts.route ?? "/"]} future={routerFuture}>{ui}</MemoryRouter>
      </QueryClientProvider>
    </ConfigProvider>,
  );
}
