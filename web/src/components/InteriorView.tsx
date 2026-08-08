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
// `SimulationStore` observar por andar; cada andar mantém a MESMA planta e porta, com diferença
// apenas atmosférica no cliente. Se o motor um dia modelar andares como
// dado real, isso sobe pra `SpaceId`/fonte de snapshot — hoje seria estado inventado.
import { useMemo, useState } from "react";
import { MapView } from "./MapView";
import { EntityLegend } from "./EntityLegend";
import { FloorSelector } from "./FloorSelector";
import { generateBuildingFootprint, MATERIAL_COLOR } from "../map-engine/buildingFootprint";
import { SCALE } from "../map-engine/space";
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
// Feedback do usuário (2026-08-07, quarta leva — "não necessariamente 2x4 de um prédio na visão
// da cidade vão ser os mesmos 2x4 dentro da casa... precisamos de uma escala"): o footprint de
// `generateBuildingFootprint` é em tile de CIDADE (o que `CityView` desenha), mas o interior
// precisa de resolução mais fina pra caber móvel/escada/etc — exatamente a razão que `space.ts`
// já declara (`SCALE.cityTilesPerBuildingTile`, "quantos tiles de BuildingSpace cabem em 1 tile
// de CitySpace") e nunca tinha sido usada por nenhum consumidor até agora. Cada tile do
// footprint (visto de fora) vira um bloco `SCALE.cityTilesPerBuildingTile²` aqui dentro.
const INTERIOR_SCALE = SCALE.cityTilesPerBuildingTile;
// px/tile — grid agora ~6x mais denso; reduzido pra caber na tela, mas >= 10 (renderer.ts só
// desenha linha de grid a partir desse zoom).
const FLOOR_PLAN_SCALE = 14;
// Feedback do usuário (2026-08-07, terceira leva — "não preciso da planta no mapa, eu só quero
// o contorno transparente e o grid dentro deste contorno; ao entrar num prédio eu quero ver só
// os móveis, escada etc, não uma cópia do prédio"): as duas tentativas anteriores (réplica em
// escala; depois margem ao redor + planta sólida no meio) ainda desenhavam a planta como um
// bloco opaco de parede/piso/porta com rótulo "cell X" — igual a como um prédio aparece visto de
// FORA (CityView) — e por dentro isso lê como "tem outro prédio aqui dentro", não como "estou
// dentro dele". Removida a planta sólida por completo: só o CONTORNO das paredes (transparente,
// sem preencher piso, sem rótulo) marca onde ficam as paredes reais, e o grid de linhas cobre o
// espaço andável inteiro dentro dele — sem inventar móvel/escada ainda (não modelado, gap 5).
const CONTOUR_ALPHA_HEX = "cc"; // contorno legível sobre o piso interno
const INTERIOR_FLOOR_COLOR = "#3d382d";

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

  // Footprint em tile de CIDADE (o mesmo que `CityView` desenha visto de fora) — pequeno de
  // propósito lá (4-6 tiles). O grid do interior usa `INTERIOR_SCALE` mais fino (ver comentário
  // acima), então `cityFootprintCells` só serve pra saber onde ficam as paredes/porta; não é
  // desenhado nessa resolução.
  const cityFootprintCells = useMemo(
    () => generateBuildingFootprint(buildingId, snapshot.buildingTypeId, floor),
    [buildingId, snapshot.buildingTypeId, floor],
  );
  const cityWidth = Math.max(...cityFootprintCells.map((c) => c.x)) + 1;
  const cityHeight = Math.max(...cityFootprintCells.map((c) => c.y)) + 1;
  const width = cityWidth * INTERIOR_SCALE;
  const height = cityHeight * INTERIOR_SCALE;

  const cells = useMemo(() => ({
    width,
    height,
    backgroundColor: INTERIOR_FLOOR_COLOR,
    colorAt: () => INTERIOR_FLOOR_COLOR,
  }), [width, height]);
  const initialCamera = useMemo(
    () => ({ center: { x: width / 2, y: height / 2 }, scale: FLOOR_PLAN_SCALE }),
    [width, height],
  );

  // Só o contorno das paredes reais (sem piso preenchido, sem porta em destaque, sem rótulo) —
  // marca onde ficam as paredes, transparente, puramente decorativo (não é clicável/selecionável:
  // não há "planta" pra inspecionar, só o espaço andável que o grid acima já cobre). Cada tile
  // de cidade vira um bloco `INTERIOR_SCALE x INTERIOR_SCALE` aqui, então a parede continua com
  // a espessura de 1 tile de cidade — várias células de interior de largura, não 1.
  const contourEntity: AuthoritativeEntity = useMemo(() => {
    const wallCells = cityFootprintCells.filter((c) => c.material !== "floor");
    const cells: { x: number; y: number; color: string }[] = [];
    for (const c of wallCells) {
      const color = `${MATERIAL_COLOR[c.material]}${CONTOUR_ALPHA_HEX}`;
      for (let dy = 0; dy < INTERIOR_SCALE; dy++) {
        for (let dx = 0; dx < INTERIOR_SCALE; dx++) {
          cells.push({ x: c.x * INTERIOR_SCALE + dx, y: c.y * INTERIOR_SCALE + dy, color });
        }
      }
    }
    return {
      ref: { kind: "cell" as const, id: `${buildingId}-contour`, space },
      position: { x: 0, y: 0 },
      size: { w: width, h: height },
      sizeIsDerived: true,
      color: "#00000000",
      footprintCells: cells,
      decorative: true,
    };
  }, [buildingId, space, width, height, cityFootprintCells]);

  return (
    <div className="map-fullscreen" data-testid="interior-view">
      <div className="map-hud map-hud-top-left">
        <h2>Prédio {snapshot.id.value}</h2>
        <p>Tipo: {snapshot.buildingTypeId}</p>
        {!snapshot.occupancyModeled && <p role="note">Ocupação por interior ainda não é modelada.</p>}

        <FloorSelector floor={floor} label={floorLabel(floor)} onChange={setFloor} />

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
        staticEntities={[contourEntity]}
        initialCamera={initialCamera}
        resetCameraKey={floor}
      />
    </div>
  );
}
