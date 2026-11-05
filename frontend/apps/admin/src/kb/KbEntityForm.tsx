import type { ReactNode } from "react";
import { Form, Input, InputNumber, Select, Switch, type FormInstance } from "antd";
import { parseViNumber } from "../format";
import type { FieldConfig } from "./entityConfig";

/** Tạo control nhập cho một field. `ref` và `datetime` sẽ được bổ sung ở Task 6. */
export function renderField(field: FieldConfig): ReactNode {
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
          parser={(s) => parseViNumber(s ?? "") ?? NaN}
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
    case "datetime":
      throw new Error("not implemented until Task 6");
  }
}

interface Props {
  form: FormInstance;
  fields: FieldConfig[];
  initialValues?: Record<string, unknown>;
  onFinish: (values: Record<string, unknown>) => void;
}

export function KbEntityForm({ form, fields, initialValues, onFinish }: Props) {
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
          {renderField(f)}
        </Form.Item>
      ))}
    </Form>
  );
}
