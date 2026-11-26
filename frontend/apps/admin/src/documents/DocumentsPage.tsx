import { useState } from "react";
import { Alert, App, Button, Form, Input, InputNumber, Popconfirm, Space, Table, Tag, Tooltip, Typography, Upload } from "antd";
import { InboxOutlined, LoadingOutlined } from "@ant-design/icons";
import { ApiError, isUnauthorized } from "../api/client";
import {
  POLL_MS,
  useDeleteDocument,
  useDocuments,
  useReprocessDocument,
  useUploadDocument,
} from "../api/documents";
import type { TaiLieuDto } from "../api/types";
import { formatDateTime } from "../format";

const MAX_BYTES = 20 * 1024 * 1024;

const errorText = (e: unknown) => (e instanceof ApiError ? e.title : "Có lỗi xảy ra");

function StatusTag({ doc }: { doc: TaiLieuDto }) {
  switch (doc.trangThai) {
    case "ChoXuLy":
      return <Tag>Chờ xử lý</Tag>;
    case "DangXuLy":
      return (
        <Tag color="blue" icon={<LoadingOutlined spin />}>
          Đang xử lý
        </Tag>
      );
    case "Xong":
      return <Tag color="green">Đã xử lý</Tag>;
    case "ThatBai":
      return (
        <Tooltip title={doc.loi ?? "Không rõ nguyên nhân"}>
          <Tag color="red">Lỗi</Tag>
        </Tooltip>
      );
  }
}

interface UploadValues {
  tieuDe: string;
  namApDung?: number | null;
  nguonTen?: string;
  nguonUrl?: string;
}

export function DocumentsPage({ pollMs = POLL_MS }: { pollMs?: number }) {
  const { message } = App.useApp();
  const [form] = Form.useForm<UploadValues>();
  const [file, setFile] = useState<File | null>(null);
  const [fileError, setFileError] = useState<string | null>(null);
  const tieuDe = Form.useWatch("tieuDe", form);

  const docs = useDocuments(pollMs);
  const upload = useUploadDocument();
  const reprocess = useReprocessDocument();
  const remove = useDeleteDocument();

  const fail = (e: unknown) => {
    if (!isUnauthorized(e)) message.error(errorText(e));
  };

  const submit = (v: UploadValues) => {
    if (!file) return;
    const fd = new FormData();
    fd.append("file", file);
    fd.append("tieuDe", v.tieuDe.trim());
    if (v.namApDung != null) fd.append("namApDung", String(v.namApDung));
    if (v.nguonTen?.trim()) fd.append("nguonTen", v.nguonTen.trim());
    if (v.nguonUrl?.trim()) fd.append("nguonUrl", v.nguonUrl.trim());
    upload.mutate(fd, {
      onSuccess: () => {
        message.success("Đã tải lên, tài liệu đang được xử lý");
        form.resetFields();
        setFile(null);
      },
      onError: fail,
    });
  };

  const columns = [
    { title: "Tiêu đề", dataIndex: "tieuDe", key: "tieuDe" },
    { title: "File", dataIndex: "tenFileGoc", key: "tenFileGoc" },
    { title: "Năm", dataIndex: "namApDung", key: "namApDung" },
    { title: "Trạng thái", key: "trangThai", render: (_: unknown, d: TaiLieuDto) => <StatusTag doc={d} /> },
    { title: "Số đoạn", dataIndex: "soDoan", key: "soDoan" },
    { title: "Cập nhật", key: "capNhatLuc", render: (_: unknown, d: TaiLieuDto) => formatDateTime(d.capNhatLuc) },
    {
      title: "",
      key: "actions",
      render: (_: unknown, d: TaiLieuDto) => (
        <Space>
          <Button
            size="small"
            loading={reprocess.isPending && reprocess.variables === d.logicalId}
            onClick={() => reprocess.mutate(d.logicalId, { onError: fail })}
          >
            Xử lý lại
          </Button>
          <Popconfirm
            title="Xóa tài liệu này?"
            okText="Xóa"
            cancelText="Hủy"
            onConfirm={() => remove.mutate(d.logicalId, { onError: fail })}
          >
            <Button size="small" danger>
              Xóa
            </Button>
          </Popconfirm>
        </Space>
      ),
    },
  ];

  return (
    <Space direction="vertical" size="middle" style={{ width: "100%" }}>
      <Typography.Title level={3} style={{ margin: 0 }}>
        Tài liệu
      </Typography.Title>
      <Form form={form} layout="vertical" onFinish={submit} style={{ maxWidth: 560 }}>
        <Form.Item label="Tiêu đề" name="tieuDe" rules={[{ required: true, whitespace: true, message: "Nhập tiêu đề" }]}>
          <Input />
        </Form.Item>
        <Form.Item label="Năm áp dụng" name="namApDung">
          <InputNumber min={2000} max={2100} precision={0} style={{ width: 160 }} />
        </Form.Item>
        <Form.Item label="Tên nguồn" name="nguonTen">
          <Input />
        </Form.Item>
        <Form.Item label="Link nguồn" name="nguonUrl">
          <Input />
        </Form.Item>
        <Upload.Dragger
          accept=".pdf,.docx,.md,.txt"
          multiple={false}
          maxCount={1}
          disabled={upload.isPending}
          fileList={[]}
          beforeUpload={(f) => {
            if (f.size > MAX_BYTES) {
              setFile(null);
              setFileError("File vượt quá 20 MB");
            } else {
              setFile(f);
              setFileError(null);
            }
            return false;
          }}
        >
          <p className="ant-upload-drag-icon">
            <InboxOutlined />
          </p>
          <p className="ant-upload-text">
            {file ? file.name : "Kéo thả hoặc bấm để chọn file .pdf / .docx / .md / .txt (tối đa 20 MB)"}
          </p>
        </Upload.Dragger>
        {fileError && <Alert style={{ marginTop: 8 }} type="error" showIcon message={fileError} />}
        <Button
          style={{ marginTop: 16 }}
          type="primary"
          htmlType="submit"
          loading={upload.isPending}
          disabled={!file || !tieuDe?.trim()}
        >
          Tải lên
        </Button>
      </Form>

      <Typography.Text type="secondary">
        Tài liệu được xử lý tự động. Cần chờ xử lý xong trước khi xuất bản.
      </Typography.Text>
      {docs.isError && <Alert type="error" showIcon message={errorText(docs.error)} />}
      <Table<TaiLieuDto>
        size="small"
        rowKey="logicalId"
        loading={docs.isLoading}
        columns={columns}
        dataSource={docs.data ?? []}
        scroll={{ x: true }}
      />
    </Space>
  );
}
