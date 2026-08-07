import { useState } from "react";
import type { LayerBuildResult, VisualLayerName } from "../types";

export interface LayerLegendProps {
  layers: Record<VisualLayerName, LayerBuildResult>;
}

/// UX pass 3: a lista de camadas ocupava uma tela inteira abaixo do mapa — virou um botão HUD
/// que abre um popover compacto, o mapa nunca perde espaço por causa dela.
export function LayerLegend({ layers }: LayerLegendProps) {
  const [open, setOpen] = useState(false);
  return (
    <div className="layer-legend">
      <button type="button" onClick={() => setOpen((v) => !v)}>
        Camadas {open ? "▲" : "▼"}
      </button>
      {open && (
        <ul aria-label="camadas-globais">
          {Object.entries(layers).map(([name, layer]) => (
            <li key={name} className={layer.isModeled ? "" : "layer-not-modeled"}>
              {name}: {layer.isModeled ? "disponível" : "ainda não modelada"}
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}
