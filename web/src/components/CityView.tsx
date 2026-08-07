// Fase 15.1, T14: cidade como configuração de `MapView` — mesmo componente de mapa do
// mapa-múndi, tools/fonte de entidades diferentes. Moradores vêm dinamicamente de
// `SimulationStore.entitiesOf` (o payload usa `residents`, já reconhecido por lá); prédios não
// têm posição real (`Building` sem `CellCoord` — context.md gap 5), então continuam com o
// layout de anel aproximado calculado aqui, marcado `sizeIsDerived: true` (traço tracejado no
// renderer em vez do preenchimento sólido de um morador).
import { useMemo } from "react";
import { MapView } from "./MapView";
import { colorById } from "../colorById";
import type { Viewport } from "../map-engine/Camera";
import type { LodThresholds } from "../map-engine/lod";
import type { AuthoritativeEntity, EntityRef, SpaceId } from "../map-engine/types";
import type { SimulationStore } from "../state/simulationStore";
import type { ViewStore } from "../state/viewStore";
import type { SelectionStore } from "../state/selectionStore";
import type { CitySnapshot } from "../types";

export interface CityViewProps {
  snapshot: CitySnapshot;
  viewport: Viewport;
  simulationStore: SimulationStore;
  viewStore: ViewStore;
  selectionStore: SelectionStore;
}

const BUILDING_RING_RADIUS = 6;
const LOD_THRESHOLDS: LodThresholds = { aggregate: 4, token: 10, detail: 18 };
/** Sem um "tamanho de grid local" mais (coordenadas de cidade agora são absolutas, iguais às
 * do mundo) — 16px/tile é o equivalente ao zoom antigo de fit-to-screen num grid local de 21x21. */
const DEFAULT_CITY_ZOOM_SCALE = 16;

function resolveNavigationTarget(cityId: string): (ref: EntityRef) => SpaceId | null {
  return (ref) => (ref.kind === "building" ? { kind: "Building", buildingId: ref.id, cityId } : null);
}

export function CityView({ snapshot, viewport, simulationStore, viewStore, selectionStore }: CityViewProps) {
  const space: SpaceId = useMemo(() => ({ kind: "City", cityId: snapshot.id.value }), [snapshot.id.value]);

  // Sem terreno/grid local próprio de cidade ainda — cobre uma janela ampla em torno da cidade
  // pra `visibleWorldRect` não colidir com um grid degenerado de 0x0.
  const cells = useMemo(() => ({ width: 100000, height: 100000, colorAt: () => undefined }), []);

  const initialCamera = useMemo(
    () => ({ center: { ...snapshot.location }, scale: DEFAULT_CITY_ZOOM_SCALE }),
    [snapshot.location],
  );

  const buildingEntities: AuthoritativeEntity[] = useMemo(
    () =>
      snapshot.buildings.map((building, i) => {
        const angle = (i / Math.max(1, snapshot.buildings.length)) * Math.PI * 2;
        return {
          ref: { kind: "building" as const, id: String(building.id.value), space },
          position: {
            x: snapshot.location.x + Math.cos(angle) * BUILDING_RING_RADIUS,
            y: snapshot.location.y + Math.sin(angle) * BUILDING_RING_RADIUS,
          },
          size: { w: 1, h: 1 },
          sizeIsDerived: true, // layout de anel client-side, não posição real (context.md gap 5)
          color: colorById(building.buildingTypeId, 40, 55),
        };
      }),
    [snapshot.buildings, snapshot.location, space],
  );

  return (
    <div className="map-fullscreen" data-testid="city-view">
      <div className="map-hud map-hud-top-left">
        <h2>Cidade {snapshot.id.value.slice(0, 8)}</h2>
        <p>
          Pool agregado: {snapshot.aggregatePool.count} habitantes não materializados (riqueza{" "}
          {snapshot.aggregatePool.wealthSum}, saúde {snapshot.aggregatePool.healthSum})
        </p>
      </div>

      <MapView
        space={space}
        viewport={viewport}
        cells={cells}
        layers={[]}
        lodThresholds={LOD_THRESHOLDS}
        simulationStore={simulationStore}
        viewStore={viewStore}
        selectionStore={selectionStore}
        staticEntities={buildingEntities}
        resolveNavigationTarget={resolveNavigationTarget(snapshot.id.value)}
        initialCamera={initialCamera}
      />
    </div>
  );
}
