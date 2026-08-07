// Feedback do usuário (2026-08-07): "uma legenda de cores que podemos ler (não obrigatório
// ficar aparecendo)" — mesmo padrão colapsável de `LayerLegend.tsx`, mas pra categoria de
// entidade (cidade/prédio/npc) em vez de camada.
import { useState } from "react";
import { CATEGORY_COLOR } from "../map-engine/categoryColors";

const ROWS: { kind: keyof typeof CATEGORY_COLOR; label: string; shape: string }[] = [
  { kind: "city", label: "Cidade", shape: "área (retângulo)" },
  { kind: "building", label: "Prédio", shape: "área tracejada (posição aproximada)" },
  { kind: "npc", label: "NPC", shape: "ponto/token" },
];

export function EntityLegend() {
  const [open, setOpen] = useState(false);
  return (
    <div className="entity-legend">
      <button type="button" onClick={() => setOpen((v) => !v)}>
        Legenda {open ? "▲" : "▼"}
      </button>
      {open && (
        <ul aria-label="legenda-de-entidades">
          {ROWS.map((row) => (
            <li key={row.kind}>
              <span className="entity-legend-swatch" style={{ backgroundColor: CATEGORY_COLOR[row.kind] }} />
              {row.label} — {row.shape}
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}
