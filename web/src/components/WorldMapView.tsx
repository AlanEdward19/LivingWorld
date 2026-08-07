import { useMemo, useState } from "react";
import { GridCanvas } from "./GridCanvas";
import { SidePanel } from "./SidePanel";
import { LayerLegend } from "./LayerLegend";
import { riverOverlayPoints, terrainColorLookup, worldMarkers } from "../worldMapData";
import { computeFitZoom } from "../gridFit";
import type { GlobalSnapshot } from "../types";

export interface WorldMapViewProps {
  snapshot: GlobalSnapshot;
  onSelectCity: (cityId: string) => void;
}

type Selection = { kind: "city"; id: string } | { kind: "npc"; id: string } | null;

/// T12 (fase 15, UX pass 2) + UX pass 3: mapa-múndi em tela cheia (feedback: "o mapa deveria
/// ser a tela toda, tipo Civilization/Skyrim") — título e camadas viram HUD flutuante por cima
/// do grid em vez de texto empilhado abaixo dele; zoom inicial preenche o viewport disponível.
export function WorldMapView({ snapshot, onSelectCity }: WorldMapViewProps) {
  const [zoom, setZoom] = useState(() =>
    computeFitZoom(snapshot.width, snapshot.height, window.innerWidth - 40, window.innerHeight - 60),
  );
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
    <div className="map-fullscreen" data-testid="world-map-view">
      <div className="map-hud map-hud-top-left">
        <h2>Mapa-múndi</h2>
        <LayerLegend layers={snapshot.layers} />
      </div>

      <GridCanvas
        width={snapshot.width}
        height={snapshot.height}
        cellColor={terrainColor}
        overlayPoints={riverPoints}
        markers={markers}
        zoom={zoom}
        onZoomChange={setZoom}
        fillContainer
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
  );
}
