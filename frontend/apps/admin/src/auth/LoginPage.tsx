import { useState } from "react";
import { Alert, Button, Card, Form, Input, Typography } from "antd";
import { useNavigate, useSearchParams } from "react-router-dom";
import { ApiError, apiFetch } from "../api/client";
import type { LoginResponse } from "../api/types";
import { authStore } from "./authStore";
import { safeReturnTo } from "./safeReturnTo";

interface FormValues {
  email: string;
  password: string;
}

export function LoginPage() {
  const [params] = useSearchParams();
  const navigate = useNavigate();
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);

  async function onFinish(values: FormValues) {
    setError(null);
    setLoading(true);
    try {
      const r = await apiFetch<LoginResponse>("/api/auth/login", { method: "POST", json: values });
      authStore.setSession(r);
      navigate(safeReturnTo(params.get("returnTo")), { replace: true });
    } catch (e) {
      if (e instanceof ApiError) {
        setError(e.status === 401 ? "Email hoặc mật khẩu không đúng" : e.title);
      } else {
        setError("Không thể kết nối máy chủ");
      }
      setLoading(false);
    }
  }

  return (
    <div style={{ display: "flex", justifyContent: "center", padding: 48 }}>
      <Card style={{ width: 380 }}>
        <Typography.Title level={3}>Đăng nhập</Typography.Title>
        {params.get("expired") === "1" && (
          <Alert
            type="warning"
            showIcon
            style={{ marginBottom: 16 }}
            message="Phiên đăng nhập đã hết hạn, vui lòng đăng nhập lại"
          />
        )}
        {error && <Alert type="error" showIcon style={{ marginBottom: 16 }} message={error} />}
        <Form<FormValues> layout="vertical" onFinish={onFinish}>
          <Form.Item label="Email" name="email" rules={[{ required: true, message: "Vui lòng nhập email" }]}>
            <Input type="email" autoComplete="username" />
          </Form.Item>
          <Form.Item
            label="Mật khẩu"
            name="password"
            rules={[{ required: true, message: "Vui lòng nhập mật khẩu" }]}
          >
            <Input.Password autoComplete="current-password" />
          </Form.Item>
          <Button type="primary" htmlType="submit" block loading={loading}>
            Đăng nhập
          </Button>
        </Form>
      </Card>
    </div>
  );
}
