import { useMemo, useState } from "react";
import { Alert, Button, Form, Modal, Popconfirm, Select, Space, Table, Typography, message } from "antd";
import { ApiError } from "../api/client";
import { useKbCreate, useKbDelete, useKbList, useKbUpdate, type KbRow } from "../api/kb";
import type { EntityConfig, RefMaps } from "./entityConfig";
import { KbEntityForm } from "./KbEntityForm";
import { applyApiErrorToForm } from "./formErrors";

// Task 6 sẽ nạp dữ liệu thật cho các cột kiểu tham chiếu.
const EMPTY_REFS: RefMaps = { nganh: new Map(), "phuong-thuc": new Map(), "to-hop": new Map() };

const ALL = "all";

export function KbEntityPage({ config }: { config: EntityConfig<KbRow> }) {
  const { slug } = config;
  const list = useKbList(slug);
  const create = useKbCreate(slug);
  const update = useKbUpdate(slug);
  const del = useKbDelete(slug);
  const [form] = Form.useForm();
  const [modal, setModal] = useState<{ row: KbRow | null } | null>(null);
  const [year, setYear] = useState<string | number>(ALL);

  const rows = list.data ?? [];
  const years = useMemo(
    () => [...new Set(rows.map((r) => r.nam).filter((n): n is number => typeof n === "number"))].sort((a, b) => b - a),
    [rows],
  );
  const shown = year === ALL ? rows : rows.filter((r) => r.nam === year);

  const close = () => setModal(null);

  const save = async (values: Record<string, unknown>) => {
    const editing = modal?.row ?? null;
    try {
      if (editing?.logicalId) {
        const { logicalId, ...rest } = editing;
        await update.mutateAsync({ logicalId, body: { ...rest, ...values } });
      } else {
        await create.mutateAsync(values);
      }
      message.success("Đã lưu vào bản nháp");
      close();
    } catch (err) {
      if (applyApiErrorToForm(form, err)) return;
      message.error(err instanceof ApiError ? err.title : "Có lỗi xảy ra");
    }
  };

  const remove = async (row: KbRow) => {
    if (!row.logicalId) return;
    try {
      await del.mutateAsync(row.logicalId);
      message.success("Đã xóa khỏi bản nháp");
    } catch (err) {
      if (err instanceof ApiError && err.status === 409) {
        Modal.error({
          title: err.title,
          content: (
            <ul>
              {err.thamChieu.map((t) => (
                <li key={t}>{t}</li>
              ))}
            </ul>
          ),
        });
      } else {
        message.error(err instanceof ApiError ? err.title : "Có lỗi xảy ra");
      }
    }
  };

  const columns = [
    ...config.columns.map((c) => ({
      title: c.title,
      key: c.title,
      render: (_: unknown, row: KbRow) => c.render(row, EMPTY_REFS),
    })),
    {
      title: "Thao tác",
      key: "actions",
      render: (_: unknown, row: KbRow) => (
        <Space>
          <Button type="link" onClick={() => setModal({ row })}>
            Sửa
          </Button>
          <Popconfirm
            title="Xóa bản ghi này khỏi bản nháp?"
            okText="Xóa"
            cancelText="Hủy"
            onConfirm={() => remove(row)}
          >
            <Button type="link" danger>
              Xóa
            </Button>
          </Popconfirm>
        </Space>
      ),
    },
  ];

  return (
    <Space direction="vertical" style={{ width: "100%" }} size="middle">
      <Typography.Title level={3} style={{ margin: 0 }}>
        {config.title}
      </Typography.Title>
      <Alert type="info" showIcon message="Thay đổi chỉ áp dụng cho bản nháp. Vào mục Xuất bản để AI sử dụng." />
      <Space>
        <Button type="primary" onClick={() => setModal({ row: null })}>
          Thêm mới
        </Button>
        {config.yearFilter && (
          <Select
            aria-label="Năm"
            style={{ width: 160 }}
            value={year}
            onChange={setYear}
            options={[{ value: ALL, label: "Tất cả" }, ...years.map((y) => ({ value: y, label: String(y) }))]}
          />
        )}
      </Space>
      {list.error && <Alert type="error" showIcon message="Không tải được dữ liệu" />}
      <Table
        rowKey={(r) => r.logicalId ?? JSON.stringify(r)}
        loading={list.isLoading}
        dataSource={shown}
        columns={columns}
        pagination={{ pageSize: 20, hideOnSinglePage: true }}
      />
      <Modal
        open={modal !== null}
        title={modal?.row ? `Sửa: ${config.rowLabel(modal.row)}` : `Thêm mới: ${config.title}`}
        okText="Lưu"
        cancelText="Hủy"
        confirmLoading={create.isPending || update.isPending}
        onOk={() => form.submit()}
        onCancel={close}
        destroyOnHidden
        afterOpenChange={(open) => !open && form.resetFields()}
      >
        {modal && (
          <KbEntityForm
            form={form}
            fields={config.fields}
            initialValues={modal.row ?? undefined}
            onFinish={save}
          />
        )}
      </Modal>
    </Space>
  );
}
