import { useMemo, useState } from "react";
import { GridCanvas, type GridMarker } from "./GridCanvas";
import { SidePanel } from "./SidePanel";
import { colorById } from "../colorById";
import { computeFitZoom } from "../gridFit";
import type { CitySnapshot } from "../types";

export interface CityViewProps {
  snapshot: CitySnapshot;
  onSelectBuilding: (buildingId: string) => void;
  onBack: () => void;
}

const LOCAL_SIZE = 21;
const CENTER = Math.floor(LOCAL_SIZE / 2);
const BUILDING_RING_RADIUS = 4;

type Selection = { kind: "resident"; id: string } | { kind: "building"; id: string } | null;

/// T12 (fase 15, UX pass 2): grid local da cidade — moradores plotados na posição real
/// (CellCoord relativo ao centro da cidade), prédios num layout de anel calculado no cliente
/// (domínio não guarda CellCoord de prédio hoje, ver design.md "Limitação conhecida"). O anel é
/// só disposição visual, não posição real — por isso o marcador de prédio usa um traço tracejado
/// em vez do preenchimento sólido dos moradores.
export function CityView({ snapshot, onSelectBuilding, onBack }: CityViewProps) {
  const [zoom, setZoom] = useState(() =>
    computeFitZoom(LOCAL_SIZE, LOCAL_SIZE, window.innerWidth - 40, window.innerHeight - 60),
  );
  const [selection, setSelection] = useState<Selection>(null);

  const residentMarkers: GridMarker[] = snapshot.residents.map((r) => ({
    id: `resident:${r.id.value}`,
    x: clampLocal(r.location.x - snapshot.location.x + CENTER),
    y: clampLocal(r.location.y - snapshot.location.y + CENTER),
    color: colorById(r.id.value),
  }));

  const buildingMarkers: GridMarker[] = useMemo(
    () =>
      snapshot.buildings.map((b, i) => {
        const angle = (i / Math.max(1, snapshot.buildings.length)) * Math.PI * 2;
        return {
          id: `building:${b.id.value}`,
          x: clampLocal(Math.round(CENTER + Math.cos(angle) * BUILDING_RING_RADIUS)),
          y: clampLocal(Math.round(CENTER + Math.sin(angle) * BUILDING_RING_RADIUS)),
          color: colorById(b.buildingTypeId, 40, 55),
        };
      }),
    [snapshot.buildings],
  );

  const selectedResident = selection?.kind === "resident"
    ? snapshot.residents.find((r) => String(r.id.value) === selection.id)
    : undefined;
  const selectedBuilding = selection?.kind === "building"
    ? snapshot.buildings.find((b) => String(b.id.value) === selection.id)
    : undefined;

  return (
    <div className="map-fullscreen" data-testid="city-view">
      <div className="map-hud map-hud-top-left">
        <button type="button" onClick={onBack}>
          ← mapa-múndi
        </button>
        <h2>Cidade {snapshot.id.value.slice(0, 8)}</h2>
        <p>
          Pool agregado: {snapshot.aggregatePool.count} habitantes não materializados (riqueza{" "}
          {snapshot.aggregatePool.wealthSum}, saúde {snapshot.aggregatePool.healthSum})
        </p>
      </div>

      <GridCanvas
        width={LOCAL_SIZE}
        height={LOCAL_SIZE}
        markers={[...residentMarkers, ...buildingMarkers]}
        zoom={zoom}
        onZoomChange={setZoom}
        fillContainer
        onMarkerClick={(id) => {
          const [kind, refId] = id.split(":");
          setSelection(kind === "resident" ? { kind: "resident", id: refId } : { kind: "building", id: refId });
        }}
      />

      {selectedResident && (
        <SidePanel title={`NPC ${selectedResident.id.value}`} onClose={() => setSelection(null)}>
          <p>
            Posição: ({selectedResident.location.x}, {selectedResident.location.y})
          </p>
          {selectedResident.currentAction !== null && <p>Ação: {selectedResident.currentAction}</p>}
        </SidePanel>
      )}

      {selectedBuilding && (
        <SidePanel
          title={`Prédio ${selectedBuilding.id.value}`}
          onClose={() => setSelection(null)}
          action={{
            label: "Entrar",
            onClick: () => onSelectBuilding(String(selectedBuilding.id.value)),
          }}
        >
          <p>Tipo: {selectedBuilding.buildingTypeId}</p>
          <p className="approximate-note">posição no mapa é layout aproximado (sem dado real)</p>
        </SidePanel>
      )}
    </div>
  );
}

function clampLocal(v: number): number {
  return Math.min(LOCAL_SIZE - 1, Math.max(0, v));
}
