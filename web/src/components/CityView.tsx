// Fase 15.1, T14 + T18: cidade como configuração de `MapView`. Moradores vêm de
// `SimulationStore.entitiesOf`; prédios concluídos usam `location` da API (origem do footprint).
import { useMemo, useState, useSyncExternalStore } from "react";
import { MapView } from "./MapView";
import { EntityLegend } from "./EntityLegend";
import { FloorSelector } from "./FloorSelector";
import { CATEGORY_COLOR } from "../map-engine/categoryColors";
import { mergeCityBuildingMarkers } from "../map-engine/cityBuildingPlacement";
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

// T50: anel próprio (raio menor, mais perto do centro) pros tokens do pool agregado — não
// competem visualmente com prédios autoritativos, e o traço tracejado (sizeIsDerived) já sinaliza
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

  // Casas/locais de trabalho concluídos após o snapshot inicial chegam via `buildingUpserts`
  // no delta de tick, não em `snapshot.buildings` (estático) — assinatura própria igual à do
  // mapa-múndi para `livingCities` (WorldMapView.tsx), senão o prédio nunca aparece.
  const livingBuildings = useSyncExternalStore(
    (onStoreChange) => simulationStore.subscribe(onStoreChange),
    () => simulationStore.livingStateOf(space).buildings,
  );

  const buildingEntities: AuthoritativeEntity[] = useMemo(
    () => mergeCityBuildingMarkers(snapshot.buildings, livingBuildings.values(), space, floor),
    [snapshot.buildings, livingBuildings, space, floor],
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
