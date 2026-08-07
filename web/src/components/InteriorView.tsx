// Fase 15.1, T22 (adiantado por feedback do usuário 2026-08-07 — "quero resolver a forma do
// prédio no frontend agora, motor depois"): BuildingSpace real. Mesma planta determinística de
// `buildingFootprint.ts` que `CityView` já usa pro footprint visto de fora, agora desenhada em
// coordenadas LOCAIS do prédio (canto em (0,0)) — mesmo componente de mapa (`MapView`), fonte
// de entidade diferente. Ocupação (moradores dentro do prédio) continua não modelada
// (`InteriorProjector.OccupancyModeled`) — isso não muda; o que muda é que a FORMA do prédio
// deixa de ser um placeholder textual e passa a ser a mesma planta wireframe do CityView.
//
// Andar (Z): estado local deste componente, não do `SpaceId`/`ViewStore` — não existe dado de
// andar no motor (nem CellCoord de prédio — context.md gap 5), então não há nada pra
// `SimulationStore` observar por andar; cada andar é a MESMA planta gerada com uma seed
// diferente (buildingId+floor), puramente client-side. Se o motor um dia modelar andares como
// dado real, isso sobe pra `SpaceId`/fonte de snapshot — hoje seria estado inventado.
import { useMemo, useState } from "react";
import { MapView } from "./MapView";
import { EntityLegend } from "./EntityLegend";
import { generateBuildingFootprint, MATERIAL_COLOR } from "../map-engine/buildingFootprint";
import type { Viewport } from "../map-engine/Camera";
import type { LodThresholds } from "../map-engine/lod";
import type { AuthoritativeEntity, SpaceId } from "../map-engine/types";
import type { SimulationStore } from "../state/simulationStore";
import type { ViewStore } from "../state/viewStore";
import type { SelectionStore } from "../state/selectionStore";
import type { InteriorSnapshot } from "../types";

export interface InteriorViewProps {
  snapshot: InteriorSnapshot;
  viewport: Viewport;
  simulationStore: SimulationStore;
  viewStore: ViewStore;
  selectionStore: SelectionStore;
}

const LOD_THRESHOLDS: LodThresholds = { aggregate: 4, token: 10, detail: 18 };
const FLOOR_PLAN_SCALE = 32; // px/tile — a planta é pequena (4-6 tiles), zoom fixo generoso

function floorLabel(floor: number): string {
  if (floor === 0) {
    return "Térreo";
  }
  return floor > 0 ? `${floor}º andar acima` : `${Math.abs(floor)}º subsolo`;
}

export function InteriorView({ snapshot, viewport, simulationStore, viewStore, selectionStore }: InteriorViewProps) {
  const [floor, setFloor] = useState(0);
  const buildingId = String(snapshot.id.value);
  const space: SpaceId = useMemo(
    () => ({ kind: "Building" as const, buildingId, cityId: snapshot.city.value }),
    [buildingId, snapshot.city.value],
  );

  const footprintCells = useMemo(
    () => generateBuildingFootprint(buildingId, snapshot.buildingTypeId, floor),
    [buildingId, snapshot.buildingTypeId, floor],
  );
  const width = Math.max(...footprintCells.map((c) => c.x)) + 1;
  const height = Math.max(...footprintCells.map((c) => c.y)) + 1;

  const cells = useMemo(() => ({ width, height, colorAt: () => undefined }), [width, height]);
  const initialCamera = useMemo(
    () => ({ center: { x: width / 2, y: height / 2 }, scale: FLOOR_PLAN_SCALE }),
    [width, height],
  );

  // BUG real corrigido ao vivo (2026-08-07): a planta não pode usar `kind: "building"` — esse
  // kind é reservado pra "o prédio visto de fora" (`BuildingInspector` assume
  // `entityRef.space.kind === "City"`, o prédio-onde-ele-está). Aqui dentro o `space` já É o
  // Building, o que quebrava essa suposição e derrubava o app ao entrar. `"cell"` evita a
  // ambiguidade — clicar na própria planta não abre um inspector de "prédio".
  const floorPlanEntity: AuthoritativeEntity = useMemo(
    () => ({
      ref: { kind: "cell" as const, id: buildingId, space },
      position: { x: 0, y: 0 },
      size: { w: width, h: height },
      sizeIsDerived: true,
      color: "#8a8f9c",
      footprintCells: footprintCells.map((c) => ({ x: c.x, y: c.y, color: MATERIAL_COLOR[c.material] })),
    }),
    [buildingId, space, width, height, footprintCells],
  );

  return (
    <div className="map-fullscreen" data-testid="interior-view">
      <div className="map-hud map-hud-top-left">
        <h2>Prédio {snapshot.id.value}</h2>
        <p>Tipo: {snapshot.buildingTypeId}</p>
        {!snapshot.occupancyModeled && <p role="note">Ocupação por interior ainda não é modelada.</p>}

        <div className="floor-selector">
          <button type="button" aria-label="andar-abaixo" onClick={() => setFloor((f) => f - 1)}>
            ▼
          </button>
          <span data-testid="floor-label">{floorLabel(floor)}</span>
          <button type="button" aria-label="andar-acima" onClick={() => setFloor((f) => f + 1)}>
            ▲
          </button>
        </div>

        <EntityLegend />
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
        staticEntities={[floorPlanEntity]}
        initialCamera={initialCamera}
      />
    </div>
  );
}
