// Fase 15.1, T14: cidade como configuração de `MapView` — mesmo componente de mapa do
// mapa-múndi, tools/fonte de entidades diferentes. Moradores vêm dinamicamente de
// `SimulationStore.entitiesOf` (o payload usa `residents`, já reconhecido por lá); prédios não
// têm posição real (`Building` sem `CellCoord` — context.md gap 5), então continuam com o
// layout de anel aproximado calculado aqui, marcado `sizeIsDerived: true` (traço tracejado no
// renderer em vez do preenchimento sólido de um morador).
import { useMemo, useState } from "react";
import { MapView } from "./MapView";
import { EntityLegend } from "./EntityLegend";
import { FloorSelector } from "./FloorSelector";
import { CATEGORY_COLOR } from "../map-engine/categoryColors";
import { generateBuildingFootprint, MATERIAL_COLOR } from "../map-engine/buildingFootprint";
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

// Feedback do usuário (2026-08-07, segunda rodada): "o Z não é só em prédio, é em tudo" — mesmo
// gap 5 do context.md (nenhum dado de camada/subsolo de cidade no motor), mesmo espírito
// honesto: nível vira estado local, reseed determinístico do footprint dos prédios (não
// fabrica prédio novo, só reformula a MESMA planta pra parecer outro nível).
function cityFloorLabel(floor: number): string {
  if (floor === 0) {
    return "Superfície";
  }
  return floor > 0 ? `${floor}º nível elevado` : `${Math.abs(floor)}º subsolo`;
}

export function CityView({ snapshot, viewport, simulationStore, viewStore, selectionStore }: CityViewProps) {
  const [floor, setFloor] = useState(0);
  const space: SpaceId = useMemo(() => ({ kind: "City", cityId: snapshot.id.value }), [snapshot.id.value]);

  // Sem terreno/grid local próprio de cidade ainda — cobre uma janela ampla em torno da cidade
  // pra `visibleWorldRect` não colidir com um grid degenerado de 0x0.
  const cells = useMemo(() => ({ width: 100000, height: 100000, colorAt: () => undefined }), []);

  const initialCamera = useMemo(
    () => ({ center: { ...snapshot.location }, scale: DEFAULT_CITY_ZOOM_SCALE }),
    [snapshot.location],
  );

  // Feedback do usuário (2026-08-07): prédio precisa de forma real, não círculo/retângulo
  // uniforme — `generateBuildingFootprint` dá a cada prédio uma planta determinística
  // (retângulo ou L) com parede/porta por material. Continua `sizeIsDerived: true`: é
  // client-side (domínio não tem `CellCoord` de prédio — context.md gap 5), só que agora é
  // uma forma real em vez de um placeholder uniforme.
  const buildingEntities: AuthoritativeEntity[] = useMemo(
    () =>
      snapshot.buildings.map((building, i) => {
        const angle = (i / Math.max(1, snapshot.buildings.length)) * Math.PI * 2;
        const ringCenter = {
          x: snapshot.location.x + Math.cos(angle) * BUILDING_RING_RADIUS,
          y: snapshot.location.y + Math.sin(angle) * BUILDING_RING_RADIUS,
        };
        const buildingId = String(building.id.value);
        const footprintCells = generateBuildingFootprint(buildingId, building.buildingTypeId, floor);
        const width = Math.max(...footprintCells.map((c) => c.x)) + 1;
        const height = Math.max(...footprintCells.map((c) => c.y)) + 1;

        return {
          ref: { kind: "building" as const, id: buildingId, space },
          // `position` é o canto superior-esquerdo do footprint — desloca do centro do anel
          // pro footprint ficar centrado no ponto calculado, não crescer só pra um lado.
          position: { x: ringCenter.x - width / 2, y: ringCenter.y - height / 2 },
          size: { w: width, h: height },
          sizeIsDerived: true, // layout de anel client-side, não posição real (context.md gap 5)
          color: CATEGORY_COLOR.building,
          footprintCells: footprintCells.map((c) => ({ x: c.x, y: c.y, color: MATERIAL_COLOR[c.material] })),
        };
      }),
    [snapshot.buildings, snapshot.location, space, floor],
  );

  return (
    <div className="map-fullscreen" data-testid="city-view">
      <div className="map-hud map-hud-top-left">
        <h2>Cidade {snapshot.id.value.slice(0, 8)}</h2>
        <p>
          Pool agregado: {snapshot.aggregatePool.count} habitantes não materializados (riqueza{" "}
          {snapshot.aggregatePool.wealthSum}, saúde {snapshot.aggregatePool.healthSum})
        </p>
        <FloorSelector floor={floor} label={cityFloorLabel(floor)} onChange={setFloor} />
        <EntityLegend />
      </div>

      {floor !== 0 && <div className={`z-layer-tint ${floor < 0 ? "z-layer-tint-below" : "z-layer-tint-above"}`} />}

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
