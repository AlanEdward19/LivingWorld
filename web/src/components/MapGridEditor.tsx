import { useState } from "react";
import { GridCanvas, type GridMarker } from "./GridCanvas";
import { colorById } from "../colorById";
import type { PaintedCell, SettlementRow } from "../scenarioDefaults";

export interface MapGridEditorProps {
  width: number;
  height: number;
  terrainIds: number[];
  biomeIds: number[];
  cells: Record<string, PaintedCell>;
  onCellsChange: (cells: Record<string, PaintedCell>) => void;
  settlements: SettlementRow[];
  onSettlementsChange: (settlements: SettlementRow[]) => void;
}

type PaintMode = "terrain" | "water" | "erase" | "settlement";

/// T14 (fase 15, UX pass 2): substitui autoria numérica de célula/assentamento por clique num
/// grid de verdade — clicar pinta a célula (terreno/água) ou adiciona/remove um assentamento,
/// em vez de editar arrays de números à mão. `scenarioFormToJson` (scenarioDefaults.ts) decide
/// se emite `Cells` (só quando pelo menos uma célula foi pintada aqui).
export function MapGridEditor({
  width,
  height,
  terrainIds,
  biomeIds,
  cells,
  onCellsChange,
  settlements,
  onSettlementsChange,
}: MapGridEditorProps) {
  const [mode, setMode] = useState<PaintMode>("terrain");
  const [selectedTerrain, setSelectedTerrain] = useState(terrainIds[0] ?? 1);
  const [selectedBiome, setSelectedBiome] = useState(biomeIds[0] ?? 0);

  const boundedWidth = Math.max(1, Math.min(width, 200));
  const boundedHeight = Math.max(1, Math.min(height, 200));

  function paintCell(x: number, y: number) {
    const key = `${x},${y}`;
    if (mode === "erase") {
      const next = { ...cells };
      delete next[key];
      onCellsChange(next);
      return;
    }
    if (mode === "settlement") {
      onSettlementsChange([...settlements, { name: `assentamento-${settlements.length + 1}`, x, y }]);
      return;
    }
    const existing = cells[key];
    onCellsChange({
      ...cells,
      [key]: {
        terrain: mode === "water" ? existing?.terrain ?? selectedTerrain : selectedTerrain,
        biome: existing?.biome ?? selectedBiome,
        altitude: existing?.altitude ?? 0,
        water: mode === "water" ? true : existing?.water ?? false,
      },
    });
  }

  function removeSettlementAt(index: number) {
    onSettlementsChange(settlements.filter((_, i) => i !== index));
  }

  const markers: GridMarker[] = settlements.map((s, i) => ({
    id: `settlement:${i}`,
    x: s.x,
    y: s.y,
    color: "#e05656",
  }));

  return (
    <div className="map-grid-editor">
      <div className="map-grid-editor-toolbar">
        <label>
          Modo:{" "}
          <select value={mode} onChange={(e) => setMode(e.target.value as PaintMode)}>
            <option value="terrain">pintar terreno</option>
            <option value="water">pintar água</option>
            <option value="erase">apagar célula</option>
            <option value="settlement">assentamento (clique adiciona, clique no marcador remove)</option>
          </select>
        </label>{" "}
        {mode === "terrain" && (
          <>
            <label>
              Terreno:{" "}
              <select
                value={selectedTerrain}
                onChange={(e) => setSelectedTerrain(Number(e.target.value))}
              >
                {terrainIds.map((id) => (
                  <option key={id} value={id}>
                    {id}
                  </option>
                ))}
              </select>
            </label>{" "}
            <label>
              Bioma:{" "}
              <select value={selectedBiome} onChange={(e) => setSelectedBiome(Number(e.target.value))}>
                {biomeIds.map((id) => (
                  <option key={id} value={id}>
                    {id}
                  </option>
                ))}
              </select>
            </label>
          </>
        )}
      </div>

      <GridCanvas
        width={boundedWidth}
        height={boundedHeight}
        cellColor={(x, y) => {
          const painted = cells[`${x},${y}`];
          if (!painted) return undefined;
          return painted.water ? "#3a7bd5" : colorById(painted.terrain);
        }}
        markers={markers}
        zoom={16}
        lodTokenThreshold={0}
        onCellClick={paintCell}
        onMarkerClick={(id) => {
          if (mode !== "settlement") return;
          const index = Number(id.split(":")[1]);
          removeSettlementAt(index);
        }}
      />
      <p className="approximate-note">
        {Object.keys(cells).length === 0
          ? "nenhuma célula pintada — mapa gerado 100% por procedimento a partir da Seed"
          : `${Object.keys(cells).length} célula(s) pintada(s) — restante preenchido com terreno/bioma default (${terrainIds[0] ?? 1}/${biomeIds[0] ?? 0})`}
      </p>
    </div>
  );
}
