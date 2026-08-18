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
import { generateBuildingFootprint, MATERIAL_COLOR, roofColorFor } from "../map-engine/buildingFootprint";
import type { Viewport } from "../map-engine/Camera";
import type { LodThresholds } from "../map-engine/lod";
import type { AuthoritativeEntity, EntityRef, SpaceId } from "../map-engine/types";
import type { SimulationStore } from "../state/simulationStore";
import type { ViewStore } from "../state/viewStore";
import type { SelectionStore } from "../state/selectionStore";
import type { CitySnapshot } from "../types";
import { cityGroundAt } from "../map-engine/worldVisuals";
import { computeFitZoom } from "../gridFit";

export interface CityViewProps {
  snapshot: CitySnapshot;
  viewport: Viewport;
  simulationStore: SimulationStore;
  viewStore: ViewStore;
  selectionStore: SelectionStore;
}

const BUILDING_RING_RADIUS = 6;
// T50: anel próprio (raio menor, mais perto do centro) pros tokens do pool agregado — não
// competem visualmente com o anel de prédios, e o traço tracejado (sizeIsDerived) já sinaliza
// "posição sintética, ainda não materializado".
const PENDING_RESIDENT_RING_RADIUS = 3;
// No zoom inicial da cidade o morador já deve ser um pawn legível, não um ponto de mapa-múndi.
const LOD_THRESHOLDS: LodThresholds = { aggregate: 4, token: 6, detail: 18 };

function resolveNavigationTarget(cityId: string): (ref: EntityRef) => SpaceId | null {
  return (ref) => (ref.kind === "building" ? { kind: "Building", buildingId: ref.id, cityId } : null);
}

// Feedback do usuário (2026-08-07, segunda rodada): "o Z não é só em prédio, é em tudo" — mesmo
// gap 5 do context.md (nenhum dado de camada/subsolo de cidade no motor), mesmo espírito
// honesto: nível vira estado local e tint atmosférico. Footprint e porta não mudam, porque Z
// observado não pode reformular a identidade física da construção.
function cityFloorLabel(floor: number): string {
  if (floor === 0) {
    return "Superfície";
  }
  return floor > 0 ? `${floor}º nível elevado` : `${Math.abs(floor)}º subsolo`;
}

export function CityView({ snapshot, viewport, simulationStore, viewStore, selectionStore }: CityViewProps) {
  const [floor, setFloor] = useState(0);
  const space: SpaceId = useMemo(() => ({ kind: "City", cityId: snapshot.id.value }), [snapshot.id.value]);

  // Mesmo footprint que o marcador do mapa-múndi desenha (SpatialBoundsResolver, cresce com a
  // população) — não um envelope fixo desconectado (LIVE-POLISH: usuário via um tamanho de
  // cidade lá fora e outro, sempre igual, aqui dentro).
  const cells = useMemo(
    () => ({
      width: snapshot.bounds.width,
      height: snapshot.bounds.height,
      minX: snapshot.bounds.x,
      minY: snapshot.bounds.y,
      showGrid: false,
      backgroundColor: "#7fa8b2",
      atmosphereSeed: `city:${snapshot.id.value}`,
      colorAt: (x: number, y: number) => cityGroundAt(snapshot.id.value, x, y).color,
      detailAt: (x: number, y: number) => cityGroundAt(snapshot.id.value, x, y),
    }),
    [snapshot.id.value, snapshot.bounds],
  );

  // Fit-to-screen no footprint real (mesma função que o mapa-múndi usa via `Camera.initial`,
  // T44b) — sem isso a cidade sempre abria em 8px/tile fixo, então uma cidade pequena (footprint
  // mínimo 3x3) ficava minúscula/vazia no centro da tela em vez de preencher o espaço.
  const initialCamera = useMemo(
    () => ({
      center: { ...snapshot.location },
      scale: computeFitZoom(snapshot.bounds.width, snapshot.bounds.height, viewport.width, viewport.height),
    }),
    [snapshot.location, snapshot.bounds, viewport.width, viewport.height],
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
          footprintCells: footprintCells.map((c) => ({
            x: c.x,
            y: c.y,
            color: c.material === "door" ? MATERIAL_COLOR.door : roofColorFor(`${buildingId}:${building.buildingTypeId}`),
            material: c.material === "door" ? "door" as const : "roof" as const,
          })),
        };
      }),
    [snapshot.buildings, snapshot.location, space, floor],
  );

  // T50: membro do pool agregado com id reservado (City.PoolNpcIds) — clicável, sem posição real
  // (não existe até materializar), então anel client-side igual ao dos prédios. Clique seleciona
  // -> NpcInspector chama inspectNpc -> backend devolve Lod.Pooled com opção de materializar.
  const pendingResidentEntities: AuthoritativeEntity[] = useMemo(
    () => {
      const pendingResidentIds = snapshot.pendingResidentIds ?? [];
      return pendingResidentIds.map((id, i) => {
        const angle = (i / Math.max(1, pendingResidentIds.length)) * Math.PI * 2;
        return {
          ref: { kind: "npc" as const, id: String(id), space },
          position: {
            x: snapshot.location.x + Math.cos(angle) * PENDING_RESIDENT_RING_RADIUS - 0.5,
            y: snapshot.location.y + Math.sin(angle) * PENDING_RESIDENT_RING_RADIUS - 0.5,
          },
          size: { w: 1, h: 1 },
          sizeIsDerived: true,
          color: CATEGORY_COLOR.npc,
        };
      });
    },
    [snapshot.pendingResidentIds, snapshot.location, space],
  );

  return (
    <div className="map-fullscreen" data-testid="city-view">
      <div className="map-hud map-hud-top-left">
        <h2>Cidade {snapshot.name || snapshot.id.value.slice(0, 8)}</h2>
        <p>{snapshot.residents.length} habitantes materializados</p>
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
        staticEntities={[...buildingEntities, ...pendingResidentEntities]}
        resolveNavigationTarget={resolveNavigationTarget(snapshot.id.value)}
        initialCamera={initialCamera}
      />
    </div>
  );
}
