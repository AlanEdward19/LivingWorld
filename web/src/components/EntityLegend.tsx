// Feedback do usuário (2026-08-07): "uma legenda de cores que podemos ler (não obrigatório
// ficar aparecendo)" — mesmo padrão colapsável de `LayerLegend.tsx`, mas pra categoria de
// entidade (cidade/prédio/npc) e, dentro do prédio, por material de parede/porta/piso
// (`buildingFootprint.ts` — "wireframe com material colorido").
import { useState } from "react";
import { CATEGORY_COLOR } from "../map-engine/categoryColors";
import { MATERIAL_COLOR } from "../map-engine/buildingFootprint";

const CATEGORY_ROWS: { kind: keyof typeof CATEGORY_COLOR; label: string; shape: string }[] = [
  { kind: "city", label: "Cidade", shape: "área (retângulo real)" },
  { kind: "building", label: "Prédio", shape: "footprint gerado (posição aproximada)" },
  { kind: "npc", label: "NPC", shape: "ponto/token" },
];

const MATERIAL_ROWS: { material: keyof typeof MATERIAL_COLOR; label: string }[] = [
  { material: "stoneWall", label: "Parede de pedra" },
  { material: "woodWall", label: "Parede de madeira" },
  { material: "door", label: "Porta" },
  { material: "floor", label: "Piso" },
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
          {CATEGORY_ROWS.map((row) => (
            <li key={row.kind}>
              <span className="entity-legend-swatch" style={{ backgroundColor: CATEGORY_COLOR[row.kind] }} />
              {row.label} — {row.shape}
            </li>
          ))}
          <li className="entity-legend-separator">Materiais do prédio</li>
          {MATERIAL_ROWS.map((row) => (
            <li key={row.material}>
              <span className="entity-legend-swatch" style={{ backgroundColor: MATERIAL_COLOR[row.material] }} />
              {row.label}
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}
