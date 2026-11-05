import type { ReactNode } from "react";
import { DatePicker, Form, Input, InputNumber, Select, Switch, type FormInstance } from "antd";
import { parseViNumber } from "../format";
import type { FieldConfig, RefOptions } from "./entityConfig";

export const DATETIME_FORMAT = "DD/MM/YYYY HH:mm";

/** Tạo control nhập cho một field. */
export function renderField(field: FieldConfig, refOptions?: RefOptions): ReactNode {
  switch (field.kind) {
    case "text":
      return <Input />;
    case "textarea":
      return <Input.TextArea rows={3} />;
    case "int":
      return <InputNumber precision={0} style={{ width: "100%" }} />;
    case "decimal":
      return (
        <InputNumber<number>
          precision={field.precision}
          style={{ width: "100%" }}
          formatter={(v, info) => (info.userTyping ? info.input : String(v ?? "").replace(".", ","))}
          // Ô trống -> null (bắt buộc báo lỗi, tùy chọn gửi null); chữ không phải số -> NaN.
          parser={(s) => {
            if ((s ?? "").trim() === "") return null as unknown as number;
            return parseViNumber(s ?? "") ?? NaN;
          }}
        />
      );
    case "money":
      return (
        <InputNumber<number>
          precision={0}
          min={0}
          style={{ width: "100%" }}
          formatter={(v) => String(v ?? "").replace(/\B(?=(\d{3})+(?!\d))/g, ".")}
          // Ô trống -> null (không ép về 0) để bắt buộc/tùy chọn hoạt động đúng.
          parser={(s) => {
            const digits = (s ?? "").replace(/\D/g, "");
            return (digits === "" ? null : Number(digits)) as unknown as number;
          }}
        />
      );
    case "bool":
      return <Switch />;
    case "tags":
      return <Select mode="tags" tokenSeparators={[","]} open={false} />;
    case "enum":
      return <Select options={field.options} />;
    case "ref":
      return (
        <Select
          showSearch
          allowClear={!field.required}
          placeholder={field.placeholder}
          options={refOptions?.[field.ref] ?? []}
          optionFilterProp="label"
        />
      );
    case "datetime":
      return <DatePicker showTime format={DATETIME_FORMAT} style={{ width: "100%" }} />;
  }
}

interface Props {
  form: FormInstance;
  fields: FieldConfig[];
  initialValues?: Record<string, unknown>;
  refOptions?: RefOptions;
  onFinish: (values: Record<string, unknown>) => void;
}

export function KbEntityForm({ form, fields, initialValues, refOptions, onFinish }: Props) {
  return (
    <Form form={form} layout="vertical" initialValues={initialValues} onFinish={onFinish}>
      {fields.map((f) => (
        <Form.Item
          key={f.name}
          name={f.name}
          label={f.label}
          valuePropName={f.kind === "bool" ? "checked" : "value"}
          rules={f.required ? [{ required: true, message: `Vui lòng nhập ${f.label.toLowerCase()}` }] : undefined}
        >
          {renderField(f, refOptions)}
        </Form.Item>
      ))}
    </Form>
  );
}
