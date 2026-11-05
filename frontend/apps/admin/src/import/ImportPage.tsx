import { useRef, useState } from "react";
import { Alert, Button, Select, Space, Table, Typography, Upload, message } from "antd";
import { DownloadOutlined, InboxOutlined } from "@ant-design/icons";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { ApiError, downloadBlob } from "../api/client";
import { commitImport, importKindLabels, previewImport, type ImportKind } from "../api/importApi";
import type { ImportPreview, ImportRow } from "../api/types";

const MAX_BYTES = 5 * 1024 * 1024;

const errorText = (e: unknown) => (e instanceof ApiError ? e.title : "Có lỗi xảy ra");

export function ImportPage() {
  const qc = useQueryClient();
  const [kind, setKind] = useState<ImportKind>("diem-chuan");
  const [file, setFile] = useState<File | null>(null);
  const [preview, setPreview] = useState<ImportPreview | null>(null);
  const [fileError, setFileError] = useState<string | null>(null);

  // Mỗi lần chọn file/đổi loại tăng id; kết quả preview cũ bị bỏ qua.
  const requestId = useRef(0);

  const isPreview = (b: unknown): b is ImportPreview =>
    !!b &&
    typeof b === "object" &&
    Array.isArray((b as ImportPreview).rows) &&
    typeof (b as ImportPreview).errorCount === "number";

  const reset = () => {
    requestId.current++;
    setFile(null);
    setPreview(null);
    setFileError(null);
  };

  const previewMut = useMutation({
    mutationFn: (v: { k: ImportKind; f: File; id: number }) => previewImport(v.k, v.f),
    onSuccess: (p, v) => {
      if (v.id === requestId.current) setPreview(p);
    },
    onError: (e, v) => {
      if (v.id === requestId.current) setFileError(errorText(e));
    },
  });

  const commitMut = useMutation({
    mutationFn: () => {
      setFileError(null);
      return commitImport(kind, file!);
    },
    onSuccess: async (r) => {
      message.success(`Đã thêm ${r.inserted}, cập nhật ${r.updated} bản ghi trong bản nháp`);
      reset();
      await qc.invalidateQueries({ queryKey: ["kb"] });
    },
    onError: (e) => {
      setFileError(errorText(e));
      if (e instanceof ApiError && e.status === 400 && isPreview(e.body)) setPreview(e.body);
    },
  });

  const busy = previewMut.isPending || commitMut.isPending;

  const pick = (f: File) => {
    reset();
    if (f.size > MAX_BYTES) {
      setFileError("File vượt quá 5 MB");
      return;
    }
    setFile(f);
    previewMut.mutate({ k: kind, f, id: requestId.current });
  };

  const download = async () => {
    try {
      await downloadBlob(`/api/admin/import/${kind}/template`, `mau-${kind}.xlsx`);
    } catch (e) {
      message.error(errorText(e));
    }
  };

  const valueKeys = preview ? [...new Set(preview.rows.flatMap((r) => Object.keys(r.values)))] : [];
  const columns = [
    { title: "Dòng", dataIndex: "rowNumber", key: "rowNumber" },
    ...valueKeys.map((k) => ({
      title: k,
      key: k,
      render: (_: unknown, r: ImportRow) => r.values[k] ?? "",
    })),
    {
      title: "Lỗi",
      key: "errors",
      render: (_: unknown, r: ImportRow) =>
        r.errors.map((e, i) => (
          <div key={i}>
            <Typography.Text type="danger">{e}</Typography.Text>
          </div>
        )),
    },
  ];

  return (
    <Space direction="vertical" size="middle" style={{ width: "100%" }}>
      <style>{".import-row-error > td { background: #fff1f0 !important; }"}</style>
      <Space wrap>
        <Select
          aria-label="Loại dữ liệu"
          value={kind}
          disabled={busy}
          style={{ width: 220 }}
          options={(Object.keys(importKindLabels) as ImportKind[]).map((k) => ({
            value: k,
            label: importKindLabels[k],
          }))}
          onChange={(k) => {
            setKind(k);
            reset();
          }}
        />
        <Button icon={<DownloadOutlined />} onClick={download}>
          Tải file mẫu
        </Button>
      </Space>

      <Upload.Dragger
        accept=".csv,.xlsx"
        multiple={false}
        disabled={busy}
        showUploadList={false}
        beforeUpload={(f) => {
          pick(f);
          return false;
        }}
      >
        <p className="ant-upload-drag-icon">
          <InboxOutlined />
        </p>
        <p className="ant-upload-text">Kéo thả hoặc bấm để chọn file .csv / .xlsx (tối đa 5 MB)</p>
      </Upload.Dragger>

      {fileError && <Alert type="error" showIcon message={fileError} />}

      {preview && (
        <>
          <Typography.Text strong>
            {`${preview.validCount} dòng hợp lệ, ${preview.errorCount} dòng lỗi`}
          </Typography.Text>
          <Table<ImportRow>
            size="small"
            rowKey="rowNumber"
            columns={columns}
            dataSource={preview.rows}
            rowClassName={(r) => (r.errors.length > 0 ? "import-row-error" : "")}
            scroll={{ x: true }}
          />
          <Button
            type="primary"
            disabled={!file || preview.errorCount > 0 || previewMut.isPending}
            loading={commitMut.isPending}
            onClick={() => commitMut.mutate()}
          >
            Ghi vào bản nháp
          </Button>
        </>
      )}
    </Space>
  );
}
