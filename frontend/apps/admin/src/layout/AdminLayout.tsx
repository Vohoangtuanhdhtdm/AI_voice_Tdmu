import { Button, Layout, Menu, Typography, type MenuProps } from "antd";
import { Outlet, useLocation, useNavigate } from "react-router-dom";
import { useAuth } from "../auth/authStore";
import { KB_MENU_GROUPS, KB_TITLES, KB_UNGROUPED, type KbSlug } from "./navigation";

const kbItem = (slug: KbSlug) => ({
  key: `/kb/${slug}`,
  label: KB_TITLES[slug],
});

const items: MenuProps["items"] = [
  ...KB_MENU_GROUPS.map((g) => ({
    type: "group" as const,
    key: g.title,
    label: g.title,
    children: g.slugs.map(kbItem),
  })),
  ...KB_UNGROUPED.map(kbItem),
  { key: "/import", label: "Nhập dữ liệu" },
  { key: "/releases", label: "Xuất bản" },
];

export function AdminLayout() {
  const { pathname } = useLocation();
  const navigate = useNavigate();
  const { logout } = useAuth();

  return (
    <Layout style={{ minHeight: "100vh" }}>
      <Layout.Header style={{ display: "flex", alignItems: "center", justifyContent: "space-between" }}>
        <Typography.Title level={4} style={{ color: "#fff", margin: 0 }}>
          Quản trị tri thức tuyển sinh TDMU
        </Typography.Title>
        <Button
          onClick={() => {
            logout();
            navigate("/login");
          }}
        >
          Đăng xuất
        </Button>
      </Layout.Header>
      <Layout>
        <Layout.Sider width={240} theme="light">
          <Menu mode="inline" items={items} selectedKeys={[pathname]} onClick={({ key }) => navigate(key)} />
        </Layout.Sider>
        <Layout.Content style={{ padding: 24 }}>
          <Outlet />
        </Layout.Content>
      </Layout>
    </Layout>
  );
}

export function NotFound() {
  return <Typography.Title level={3}>Không tìm thấy trang</Typography.Title>;
}
