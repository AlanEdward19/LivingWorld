import { useMemo, useState } from "react";
import { GridCanvas } from "./GridCanvas";
import { SidePanel } from "./SidePanel";
import { riverOverlayPoints, terrainColorLookup, worldMarkers } from "../worldMapData";
import type { GlobalSnapshot } from "../types";

export interface WorldMapViewProps {
  snapshot: GlobalSnapshot;
  onSelectCity: (cityId: string) => void;
}

type Selection = { kind: "city"; id: string } | { kind: "npc"; id: string } | null;

/// T12 (fase 15, UX pass 2): mapa-múndi como grid 2D de verdade — terreno colorido por id (camada
/// Terrain), rios como overlay, cidades e NPCs externos como marcadores reais na posição
/// (CellCoord), não mais lista/botão. Clique num marcador abre o SidePanel (T13); clique numa
/// célula vazia não faz nada (spec.md P1 "Grid 2D real").
export function WorldMapView({ snapshot, onSelectCity }: WorldMapViewProps) {
  const [zoom, setZoom] = useState(16);
  const [selection, setSelection] = useState<Selection>(null);

  const terrainColor = useMemo(() => terrainColorLookup(snapshot), [snapshot.layers.Terrain]);
  const riverPoints = useMemo(() => riverOverlayPoints(snapshot), [snapshot.layers.Rivers]);
  const markers = useMemo(() => worldMarkers(snapshot), [snapshot.cities, snapshot.externalNpcs]);

  const selectedCity = selection?.kind === "city"
    ? snapshot.cities.find((c) => c.id.value === selection.id)
    : undefined;
  const selectedNpc = selection?.kind === "npc"
    ? snapshot.externalNpcs.find((n) => String(n.id.value) === selection.id)
    : undefined;

  return (
    <div data-testid="world-map-view">
      <h2>Mapa-múndi</h2>
      <div className="map-view-body">
        <GridCanvas
          width={snapshot.width}
          height={snapshot.height}
          cellColor={terrainColor}
          overlayPoints={riverPoints}
          markers={markers}
          zoom={zoom}
          onZoomChange={setZoom}
          onMarkerClick={(id) => {
            const [kind, refId] = id.split(":");
            setSelection(kind === "city" ? { kind: "city", id: refId } : { kind: "npc", id: refId });
          }}
        />

        {selectedCity && (
          <SidePanel
            title={`Cidade ${selectedCity.id.value.slice(0, 8)}`}
            onClose={() => setSelection(null)}
            action={{ label: "Entrar", onClick: () => onSelectCity(selectedCity.id.value) }}
          >
            <p>População: {selectedCity.population}</p>
            <p>
              Posição: ({selectedCity.location.x}, {selectedCity.location.y})
            </p>
          </SidePanel>
        )}

        {selectedNpc && (
          <SidePanel title={`NPC ${selectedNpc.id.value}`} onClose={() => setSelection(null)}>
            <p>
              Posição: ({selectedNpc.location.x}, {selectedNpc.location.y})
            </p>
          </SidePanel>
        )}
      </div>

      <h3>Camadas</h3>
      <ul aria-label="camadas-globais">
        {Object.entries(snapshot.layers).map(([name, layer]) => (
          <li key={name}>
            {name}: {layer.isModeled ? "disponível" : "ainda não modelada"}
          </li>
        ))}
      </ul>
    </div>
  );
}
