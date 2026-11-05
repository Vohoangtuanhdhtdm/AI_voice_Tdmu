import { useRef, useState } from "react";
import { Alert, Button, Empty, Form, Input, Modal, Popconfirm, Space, Table, Tag, Typography, message } from "antd";
import { ApiError } from "../api/client";
import { useActivate, usePublish, useReleaseDiff, useReleases, useRestoreToDraft } from "../api/releases";
import type { DiffItem, Release } from "../api/types";
import { formatDateTime } from "../format";
import { KB_SLUGS, KB_TITLES } from "../layout/navigation";

const pascal = (slug: string) =>
  slug
    .split("-")
    .map((p) => p[0].toUpperCase() + p.slice(1))
    .join("");

const ENTITY_TITLES: Record<string, string> = {
  ...Object.fromEntries(KB_SLUGS.map((s) => [pascal(s), KB_TITLES[s]])),
  TaiLieu: "Tài liệu",
  TaiLieuChunk: "Đoạn tài liệu",
};

const KIND: Record<DiffItem["kind"], { text: string; color: string }> = {
  Added: { text: "Thêm", color: "green" },
  Modified: { text: "Sửa", color: "gold" },
  Removed: { text: "Xóa", color: "red" },
};

const errorText = (e: unknown) => (e instanceof ApiError ? e.title : "Có lỗi xảy ra");

export function ReleasesPage() {
  const diff = useReleaseDiff();
  const releases = useReleases();
  const publish = usePublish();
  const activate = useActivate();
  const restore = useRestoreToDraft();
  const [open, setOpen] = useState(false);
  const [form] = Form.useForm<{ ghiChu: string }>();

  // isPending chỉ đổi sau khi render lại; validateFields là async nên cần ref để chặn bấm đúp ngay lập tức.
  const inFlight = useRef(false);

  const submit = async () => {
    if (inFlight.current || publish.isPending) return;
    inFlight.current = true;
    try {
      await doPublish();
    } finally {
      inFlight.current = false;
    }
  };

  const doPublish = async () => {
    let values: { ghiChu: string };
    try {
      values = await form.validateFields();
    } catch {
      return;
    }
    try {
      const r = await publish.mutateAsync(values.ghiChu.trim());
      message.success(`Đã xuất bản phiên bản #${r.soHieu}`);
      setOpen(false);
      form.resetFields();
    } catch (e) {
      if (e instanceof ApiError && e.status === 409) {
        message.info(e.title);
        setOpen(false);
      } else {
        message.error(errorText(e));
      }
    }
  };

  const run = async (fn: () => Promise<unknown>, ok: string) => {
    try {
      await fn();
      message.success(ok);
    } catch (e) {
      message.error(errorText(e));
    }
  };

  const items = diff.data ?? [];

  return (
    <Space direction="vertical" size="large" style={{ width: "100%" }}>
      <section>
        <Space style={{ marginBottom: 12, justifyContent: "space-between", width: "100%" }}>
          <Typography.Title level={4} style={{ margin: 0 }}>
            Thay đổi chưa xuất bản
          </Typography.Title>
          <Button type="primary" disabled={items.length === 0} onClick={() => setOpen(true)}>
            Xuất bản
          </Button>
        </Space>
        {diff.isError && <Alert type="error" showIcon message={errorText(diff.error)} />}
        <Table<DiffItem>
          rowKey={(r) => `${r.entity}:${r.logicalId}`}
          loading={diff.isLoading}
          dataSource={items}
          pagination={false}
          locale={{ emptyText: <Empty description="Bản nháp trùng với bản đang phát hành" /> }}
          columns={[
            {
              title: "Loại",
              dataIndex: "kind",
              width: 100,
              render: (k: DiffItem["kind"]) => <Tag color={KIND[k].color}>{KIND[k].text}</Tag>,
            },
            { title: "Mục", dataIndex: "entity", render: (e: string) => ENTITY_TITLES[e] ?? e },
            { title: "Nội dung", dataIndex: "label" },
          ]}
        />
      </section>

      <section>
        <Typography.Title level={4}>Lịch sử phát hành</Typography.Title>
        {releases.isError && <Alert type="error" showIcon message={errorText(releases.error)} />}
        <Table<Release>
          rowKey="id"
          loading={releases.isLoading}
          dataSource={releases.data ?? []}
          pagination={false}
          columns={[
            { title: "Phiên bản", dataIndex: "soHieu", render: (n: number) => `#${n}` },
            { title: "Ghi chú", dataIndex: "ghiChu" },
            { title: "Người tạo", dataIndex: "taoBoi" },
            { title: "Thời gian", dataIndex: "taoLuc", render: (t: string) => formatDateTime(t) },
            {
              title: "Trạng thái",
              dataIndex: "isActive",
              render: (a: boolean) => (a ? <Tag color="green">Đang phát hành</Tag> : null),
            },
            {
              title: "Thao tác",
              render: (_: unknown, r: Release) => (
                <Space>
                  {!r.isActive && (
                    <Popconfirm
                      title={`AI sẽ dùng phiên bản #${r.soHieu}. Tiếp tục?`}
                      onConfirm={() =>
                        run(() => activate.mutateAsync(r.id), `Đã kích hoạt phiên bản #${r.soHieu}`)
                      }
                    >
                      <Button type="link" size="small" loading={activate.isPending && activate.variables === r.id}>
                        Kích hoạt
                      </Button>
                    </Popconfirm>
                  )}
                  <Popconfirm
                    title={`Toàn bộ bản nháp hiện tại sẽ bị thay bằng nội dung phiên bản #${r.soHieu}. Tiếp tục?`}
                    onConfirm={() =>
                      run(() => restore.mutateAsync(r.id), `Đã khôi phục bản nháp từ phiên bản #${r.soHieu}`)
                    }
                  >
                    <Button type="link" size="small" danger loading={restore.isPending && restore.variables === r.id}>
                      Khôi phục về bản nháp
                    </Button>
                  </Popconfirm>
                </Space>
              ),
            },
          ]}
        />
      </section>

      <Modal
        title="Xuất bản"
        open={open}
        confirmLoading={publish.isPending}
        onOk={submit}
        onCancel={() => !publish.isPending && setOpen(false)}
        cancelButtonProps={{ disabled: publish.isPending }}
        forceRender
      >
        <Form form={form} layout="vertical">
          <Form.Item
            name="ghiChu"
            label="Ghi chú"
            rules={[{ required: true, whitespace: true, message: "Vui lòng nhập ghi chú" }]}
          >
            <Input.TextArea rows={3} />
          </Form.Item>
        </Form>
      </Modal>
    </Space>
  );
}
