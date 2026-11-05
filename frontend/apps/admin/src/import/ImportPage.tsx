import { useState } from "react";
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

  const reset = () => {
    setFile(null);
    setPreview(null);
    setFileError(null);
  };

  const previewMut = useMutation({
    mutationFn: (v: { k: ImportKind; f: File }) => previewImport(v.k, v.f),
    onSuccess: setPreview,
    onError: (e) => setFileError(errorText(e)),
  });

  const commitMut = useMutation({
    mutationFn: () => commitImport(kind, file!),
    onSuccess: async (r) => {
      message.success(`Đã thêm ${r.inserted}, cập nhật ${r.updated} bản ghi trong bản nháp`);
      reset();
      await qc.invalidateQueries({ queryKey: ["kb"] });
    },
    onError: (e) => setFileError(errorText(e)),
  });

  const pick = (f: File) => {
    reset();
    if (f.size > MAX_BYTES) {
      setFileError("File vượt quá 5 MB");
      return;
    }
    setFile(f);
    previewMut.mutate({ k: kind, f });
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
        r.errors.map((e) => (
          <div key={e}>
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
            disabled={preview.errorCount !== 0}
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
