import { useState, type ReactNode } from "react";
import { DatePicker, Form, Input, Select, Switch, type FormInstance } from "antd";
import type { Rule } from "antd/es/form";
import { formatDecimal, formatInt, formatMoney, parseViDecimal, parseViInt, parseViMoney } from "../format";
import type { FieldConfig, RefOptions } from "./entityConfig";

type NumberKind = "int" | "decimal" | "money";

interface ViNumberInputProps {
  id?: string;
  value?: number | null;
  onChange?: (v: number | null) => void;
  parse: (s: string) => number | null;
  format: (n: number) => string;
  inputMode: "numeric" | "decimal";
}

/**
 * Ô nhập số kiểu Việt Nam. Giữ nguyên chữ người dùng gõ; giá trị form là số, null (trống)
 * hoặc NaN (không hợp lệ -> luật kiểm tra chặn lưu). Không dùng InputNumber vì nó bỏ qua
 * giá trị NaN và giữ lại tiền tố hợp lệ cuối cùng (vd. "1,2" thành 1).
 */
function ViNumberInput({ id, value, onChange, parse, format, inputMode }: ViNumberInputProps) {
  const show = (v: number | null | undefined) => (v == null || Number.isNaN(v) ? "" : format(v));
  const [text, setText] = useState(() => show(value));
  const [last, setLast] = useState(value);
  // Giá trị đổi từ bên ngoài (đặt lại form, gán giá trị ban đầu) -> hiển thị lại.
  if (value !== last && !(Number.isNaN(value) && Number.isNaN(last))) {
    setLast(value);
    if (!Object.is(parse(text), value)) setText(show(value));
  }
  return (
    <Input
      id={id}
      inputMode={inputMode}
      value={text}
      onChange={(e) => {
        const t = e.target.value;
        const v = parse(t);
        setText(t);
        setLast(v);
        onChange?.(v);
      }}
      onBlur={() => {
        const v = parse(text);
        if (v != null && !Number.isNaN(v)) setText(format(v));
      }}
    />
  );
}

function numberProps(field: Extract<FieldConfig, { kind: NumberKind }>) {
  switch (field.kind) {
    case "int":
      return {
        parse: parseViInt,
        format: (n: number) => formatInt(n, field.grouping !== false),
        inputMode: "numeric" as const,
      };
    case "money":
      return { parse: parseViMoney, format: formatMoney, inputMode: "numeric" as const };
    case "decimal": {
      const f = 10 ** field.precision;
      return {
        parse: (s: string) => {
          const v = parseViDecimal(s);
          return v == null || Number.isNaN(v) ? v : Math.round(v * f) / f;
        },
        // Không nhóm nghìn trong ô điểm: "1200" / "21,5".
        format: (n: number) => formatDecimal(n).replace(/\./g, ""),
        inputMode: "decimal" as const,
      };
    }
  }
}

function rulesFor(f: FieldConfig): Rule[] | undefined {
  const rules: Rule[] = [];
  if (f.required) rules.push({ required: true, message: `Vui lòng nhập ${f.label.toLowerCase()}` });
  if (f.kind === "int" || f.kind === "decimal" || f.kind === "money") {
    rules.push({
      validator: (_, v) =>
        typeof v === "number" && Number.isNaN(v)
          ? Promise.reject(new Error(`${f.label} không hợp lệ`))
          : Promise.resolve(),
    });
  }
  return rules.length ? rules : undefined;
}

export const DATETIME_FORMAT = "DD/MM/YYYY HH:mm";

/** Tạo control nhập cho một field. */
export function renderField(field: FieldConfig, refOptions?: RefOptions): ReactNode {
  switch (field.kind) {
    case "text":
      return <Input />;
    case "textarea":
      return <Input.TextArea rows={3} />;
    case "int":
    case "decimal":
    case "money":
      return <ViNumberInput {...numberProps(field)} />;
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
          rules={rulesFor(f)}
        >
          {renderField(f, refOptions)}
        </Form.Item>
      ))}
    </Form>
  );
}
