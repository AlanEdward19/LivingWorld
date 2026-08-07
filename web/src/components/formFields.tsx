// Editores genéricos reusados pelo World Creator: linhas nomeadas (dict chave/valor) e listas
// de objetos (array-of-record) com add/remove — ponytail: nenhuma lib de formulário, só
// useState controlado, mesmo padrão do resto do cliente (sem CSS, HTML semântico simples).
//
// Fase 15.1, T26: resolução de id pra rótulo legível — só existe catálogo real pra
// profession/skill (`GET /periods/{id}/catalog`, condicional por período, ver PeriodCatalog.cs);
// terreno/bioma/recurso/cultura/tipo-de-local/prédio não têm catálogo em lugar nenhum do
// domínio, então continuam id cru sempre (nenhum `labels` é passado pra eles — mentir um
// catálogo que não existe seria pior que mostrar o número). Quando `labels` existe, o campo
// mostra nome+id por padrão; o toggle "IDs crus" força número puro (útil pra id sem entrada no
// catálogo do período corrente, ou pra quem já sabe o número de cabeça).
import { useState } from "react";

export interface KeyNumberRow {
  key: string;
  value: number;
}

export function KeyNumberListEditor({
  label,
  keyLabel,
  rows,
  onChange,
  labels,
}: {
  label: string;
  keyLabel: string;
  rows: KeyNumberRow[];
  onChange: (rows: KeyNumberRow[]) => void;
  /** T26: catálogo id→nome pro campo `key` (só profession/skill têm um de verdade). */
  labels?: Record<number, string>;
}) {
  const [rawIds, setRawIds] = useState(false);

  function update(index: number, patch: Partial<KeyNumberRow>) {
    onChange(rows.map((r, i) => (i === index ? { ...r, ...patch } : r)));
  }

  return (
    <fieldset>
      <legend>{label}</legend>
      {labels && (
        <label>
          <input type="checkbox" checked={rawIds} onChange={(e) => setRawIds(e.target.checked)} /> IDs crus
        </label>
      )}
      {rows.map((row, i) => (
        <div key={i}>
          <label>
            {keyLabel}:{" "}
            {labels && !rawIds ? (
              <select
                aria-label={`${label}-${keyLabel}-${i}`}
                value={row.key}
                onChange={(e) => update(i, { key: e.target.value })}
              >
                {!(row.key in labels) && row.key !== "" && <option value={row.key}>{row.key} (sem nome no catálogo)</option>}
                {Object.entries(labels).map(([id, name]) => (
                  <option key={id} value={id}>
                    {name} (#{id})
                  </option>
                ))}
              </select>
            ) : (
              <input
                type="text"
                aria-label={`${label}-${keyLabel}-${i}`}
                value={row.key}
                onChange={(e) => update(i, { key: e.target.value })}
              />
            )}
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
  /** T26: catálogo id→nome pra esse campo numérico (só profession/skill têm um de verdade). */
  labels?: Record<number, string>;
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
  const [rawIds, setRawIds] = useState(false);
  const hasLabels = fields.some((f) => f.labels);

  function update(index: number, name: keyof T, value: unknown) {
    onChange(rows.map((r, i) => (i === index ? { ...r, [name]: value } : r)));
  }

  return (
    <fieldset>
      <legend>{label}</legend>
      {hasLabels && (
        <label>
          <input type="checkbox" checked={rawIds} onChange={(e) => setRawIds(e.target.checked)} /> IDs crus
        </label>
      )}
      {rows.map((row, i) => (
        <div key={i}>
          {fields.map((f) => (
            <label key={String(f.name)}>
              {f.label}:{" "}
              {f.type === "number" && f.labels && !rawIds ? (
                <select
                  aria-label={`${label}-${f.label}-${i}`}
                  value={String(row[f.name] ?? "")}
                  onChange={(e) => update(i, f.name, Number(e.target.value))}
                >
                  {!(Number(row[f.name]) in f.labels) && (
                    <option value={String(row[f.name])}>{String(row[f.name])} (sem nome no catálogo)</option>
                  )}
                  {Object.entries(f.labels).map(([id, name]) => (
                    <option key={id} value={id}>
                      {name} (#{id})
                    </option>
                  ))}
                </select>
              ) : f.type === "select" ? (
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
              ) : f.type === "nullable-number" && f.labels && !rawIds ? (
                <select
                  aria-label={`${label}-${f.label}-${i}`}
                  value={row[f.name] === null || row[f.name] === undefined ? "" : String(row[f.name])}
                  onChange={(e) => update(i, f.name, e.target.value === "" ? null : Number(e.target.value))}
                >
                  <option value="">(qualquer)</option>
                  {row[f.name] !== null && row[f.name] !== undefined && !(Number(row[f.name]) in f.labels) && (
                    <option value={String(row[f.name])}>{String(row[f.name])} (sem nome no catálogo)</option>
                  )}
                  {Object.entries(f.labels).map(([id, name]) => (
                    <option key={id} value={id}>
                      {name} (#{id})
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
