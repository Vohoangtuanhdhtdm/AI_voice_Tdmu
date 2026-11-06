import type { ReactElement } from "react";
import { render, type RenderResult } from "@testing-library/react";
import { App as AntApp, ConfigProvider } from "antd";
import viVN from "antd/locale/vi_VN";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { MemoryRouter } from "react-router-dom";
import { routerFuture } from "../App";

export function renderWithProviders(
  ui: ReactElement,
  opts: { route?: string } = {},
): RenderResult & { queryClient: QueryClient } {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  const result = render(
    <ConfigProvider locale={viVN}>
      <AntApp>
        <QueryClientProvider client={client}>
          <MemoryRouter initialEntries={[opts.route ?? "/"]} future={routerFuture}>{ui}</MemoryRouter>
        </QueryClientProvider>
      </AntApp>
    </ConfigProvider>,
  );
  return { ...result, queryClient: client };
}
