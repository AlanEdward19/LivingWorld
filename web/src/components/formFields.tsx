// Editores genéricos reusados pelo CreateWorldForm: linhas nomeadas (dict chave/valor) e listas
// de objetos (array-of-record) com add/remove — ponytail: nenhuma lib de formulário, só
// useState controlado, mesmo padrão do resto do cliente (sem CSS, HTML semântico simples).

export interface KeyNumberRow {
  key: string;
  value: number;
}

export function KeyNumberListEditor({
  label,
  keyLabel,
  rows,
  onChange,
}: {
  label: string;
  keyLabel: string;
  rows: KeyNumberRow[];
  onChange: (rows: KeyNumberRow[]) => void;
}) {
  function update(index: number, patch: Partial<KeyNumberRow>) {
    onChange(rows.map((r, i) => (i === index ? { ...r, ...patch } : r)));
  }

  return (
    <fieldset>
      <legend>{label}</legend>
      {rows.map((row, i) => (
        <div key={i}>
          <label>
            {keyLabel}:{" "}
            <input
              type="text"
              aria-label={`${label}-${keyLabel}-${i}`}
              value={row.key}
              onChange={(e) => update(i, { key: e.target.value })}
            />
          </label>{" "}
          <label>
            valor:{" "}
            <input
              type="number"
              aria-label={`${label}-valor-${i}`}
              value={row.value}
              onChange={(e) => update(i, { value: Number(e.target.value) })}
            />
          </label>{" "}
          <button type="button" onClick={() => onChange(rows.filter((_, j) => j !== i))}>
            remover
          </button>
        </div>
      ))}
      <button type="button" onClick={() => onChange([...rows, { key: "", value: 0 }])}>
        + {label}
      </button>
    </fieldset>
  );
}

export interface FieldSpec<T> {
  name: keyof T;
  label: string;
  type: "text" | "number" | "select" | "nullable-number";
  options?: readonly string[];
}

export function ObjectListEditor<T extends object>({
  label,
  fields,
  rows,
  emptyRow,
  onChange,
}: {
  label: string;
  fields: readonly FieldSpec<T>[];
  rows: T[];
  emptyRow: T;
  onChange: (rows: T[]) => void;
}) {
  function update(index: number, name: keyof T, value: unknown) {
    onChange(rows.map((r, i) => (i === index ? { ...r, [name]: value } : r)));
  }

  return (
    <fieldset>
      <legend>{label}</legend>
      {rows.map((row, i) => (
        <div key={i}>
          {fields.map((f) => (
            <label key={String(f.name)}>
              {f.label}:{" "}
              {f.type === "select" ? (
                <select
                  aria-label={`${label}-${f.label}-${i}`}
                  value={String(row[f.name] ?? "")}
                  onChange={(e) => update(i, f.name, e.target.value)}
                >
                  {f.options?.map((o) => (
                    <option key={o} value={o}>
                      {o}
                    </option>
                  ))}
                </select>
              ) : f.type === "nullable-number" ? (
                <input
                  type="number"
                  aria-label={`${label}-${f.label}-${i}`}
                  value={row[f.name] === null || row[f.name] === undefined ? "" : String(row[f.name])}
                  onChange={(e) =>
                    update(i, f.name, e.target.value === "" ? null : Number(e.target.value))
                  }
                />
              ) : f.type === "number" ? (
                <input
                  type="number"
                  aria-label={`${label}-${f.label}-${i}`}
                  value={String(row[f.name] ?? 0)}
                  onChange={(e) => update(i, f.name, Number(e.target.value))}
                />
              ) : (
                <input
                  type="text"
                  aria-label={`${label}-${f.label}-${i}`}
                  value={String(row[f.name] ?? "")}
                  onChange={(e) => update(i, f.name, e.target.value)}
                />
              )}
            </label>
          ))}{" "}
          <button type="button" onClick={() => onChange(rows.filter((_, j) => j !== i))}>
            remover
          </button>
        </div>
      ))}
      <button type="button" onClick={() => onChange([...rows, emptyRow])}>
        + {label}
      </button>
    </fieldset>
  );
}
