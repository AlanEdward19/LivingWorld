// Fase 15.1, T18: evolui `LayerLegend.tsx` de informativo (isModeled sim/não) para toggle real —
// ligar/desligar uma camada muda o render sem nenhuma requisição (o estado é local à view, quem
// desenha é `WorldMapView`). Camada `NotYetModeled` continua listada, mas desabilitada com o
// motivo em vez de virar um toggle que não faz nada (VTT2-47).
import { useState } from "react";
import type { LayerBuildResult, VisualLayerName } from "../types";
import { LAYER_Z_ORDER } from "../map-engine/layers";

export interface LayerPanelProps {
  layers: Record<VisualLayerName, LayerBuildResult>;
  active: ReadonlySet<VisualLayerName>;
  onToggle: (name: VisualLayerName) => void;
}

const NOT_YET_MODELED_REASON = "ainda não modelada — o motor devolve NotYetModeled pra esta camada";

export function LayerPanel({ layers, active, onToggle }: LayerPanelProps) {
  const [open, setOpen] = useState(false);
  return (
    <div className="layer-panel">
      <button type="button" onClick={() => setOpen((v) => !v)}>
        Camadas {open ? "▲" : "▼"}
      </button>
      {open && (
        <ul aria-label="camadas-globais">
          {LAYER_Z_ORDER.map((name) => {
            const layer = layers[name];
            if (!layer?.isModeled) {
              return (
                <li key={name} className="layer-not-modeled">
                  <label>
                    <input type="checkbox" disabled />
                    {name} — {NOT_YET_MODELED_REASON}
                  </label>
                </li>
              );
            }
            return (
              <li key={name}>
                <label>
                  <input
                    type="checkbox"
                    checked={active.has(name)}
                    onChange={() => onToggle(name)}
                    aria-label={`toggle-${name}`}
                  />
                  {name}
                </label>
              </li>
            );
          })}
        </ul>
      )}
    </div>
  );
}
