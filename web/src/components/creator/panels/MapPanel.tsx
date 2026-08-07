// Fase 15.1, T26: bloco "Mapa" do antigo `CreateWorldForm`, portado sem perder campo (AD-001).
// A pintura de terreno/água/assentamento por clique é a ferramenta do toolbar (T25) — este
// painel cobre o resto: dimensões, ids de catálogo (csv) e o bloco avançado de custo/assentamento
// por número, atrás de disclosure.
import { KeyNumberListEditor, ObjectListEditor } from "../../formFields";
import type { PanelProps } from "./types";

export function MapPanel({ form, set }: PanelProps) {
  return (
    <div>
      <div className="form-row">
        <label>
          Largura:{" "}
          <input
            type="number"
            aria-label="map-width"
            value={form.width}
            onChange={(e) => set("width", Number(e.target.value))}
          />
        </label>
        <label>
          Altura:{" "}
          <input
            type="number"
            aria-label="map-height"
            value={form.height}
            onChange={(e) => set("height", Number(e.target.value))}
          />
        </label>
        <label>
          Seed:{" "}
          <input
            type="number"
            aria-label="map-seed"
            value={form.seed}
            onChange={(e) => set("seed", Number(e.target.value))}
          />
        </label>
        <label>
          Tamanho da região:{" "}
          <input
            type="number"
            aria-label="map-region-size"
            value={form.regionSize}
            onChange={(e) => set("regionSize", Number(e.target.value))}
          />
        </label>
      </div>
      <div className="form-row">
        <label>
          Terrenos (ids, csv):{" "}
          <input
            type="text"
            aria-label="map-terrain-ids"
            value={form.terrainIds}
            onChange={(e) => set("terrainIds", e.target.value)}
          />
        </label>
        <label>
          Biomas (ids, csv):{" "}
          <input type="text" value={form.biomeIds} onChange={(e) => set("biomeIds", e.target.value)} />
        </label>
        <label>
          Recursos (ids, csv):{" "}
          <input
            type="text"
            value={form.resourceIds}
            onChange={(e) => set("resourceIds", e.target.value)}
          />
        </label>
      </div>

      <details>
        <summary>Avançado (custo de deslocamento, assentamentos por número)</summary>
        <div className="form-row">
          <label>
            Custo base:{" "}
            <input
              type="number"
              step="any"
              value={form.costWeightsBase}
              onChange={(e) => set("costWeightsBase", Number(e.target.value))}
            />
          </label>
          <label>
            Peso de altitude:{" "}
            <input
              type="number"
              step="any"
              value={form.costWeightsAltitude}
              onChange={(e) => set("costWeightsAltitude", Number(e.target.value))}
            />
          </label>
        </div>

        <KeyNumberListEditor
          label="Peso de custo por terreno"
          keyLabel="terrainId"
          rows={form.terrainWeight}
          onChange={(rows) => set("terrainWeight", rows)}
        />

        <ObjectListEditor
          label="Assentamentos (mesma lista da ferramenta no mapa — edição fina por número)"
          fields={[
            { name: "name", label: "nome", type: "text" },
            { name: "x", label: "x", type: "number" },
            { name: "y", label: "y", type: "number" },
          ]}
          rows={form.settlements}
          emptyRow={{ name: "", x: 0, y: 0 }}
          onChange={(rows) => set("settlements", rows)}
        />
      </details>
    </div>
  );
}
