// Fase 15.1, T14: mapa-múndi como configuração de `MapView` (design.md; master prompt §30-32) —
// não instancia canvas próprio, não guarda seleção local nem zoom local. Cidades entram como
// `staticEntities` (não têm delta de tick — `SimulationStore.entitiesOf` só extrai NPC externo,
// que já vem dinamicamente); double-click numa cidade resolve `{kind:"City"}` e o próprio
// `MapView` chama `ViewStore.enter`.
import { useMemo, useState, useSyncExternalStore } from "react";
import { MapView } from "./MapView";
import { LayerPanel } from "./LayerPanel";
import { EntityLegend } from "./EntityLegend";
import { FloorSelector } from "./FloorSelector";
import { terrainColorLookup, terrainDetailLookup, riverOverlayPoints } from "../worldMapData";
import { sortActiveLayers } from "../map-engine/layers";
import { mergeWorldCityMarkers } from "../map-engine/worldCityMarkers";
import type { Viewport } from "../map-engine/Camera";
import type { ActiveLayer } from "../map-engine/renderer";
import type { LodThresholds } from "../map-engine/lod";
import type { AuthoritativeEntity, EntityRef, SpaceId } from "../map-engine/types";
import type { SimulationStore } from "../state/simulationStore";
import type { ViewStore } from "../state/viewStore";
import type { SelectionStore } from "../state/selectionStore";
import type { FutureGlobalSnapshot } from "../data/contracts";
import type { VisualLayerName } from "../types";

export interface WorldMapViewProps {
  snapshot: FutureGlobalSnapshot;
  viewport: Viewport;
  simulationStore: SimulationStore;
  viewStore: ViewStore;
  selectionStore: SelectionStore;
}

const WORLD: SpaceId = { kind: "World" };
const LOD_THRESHOLDS: LodThresholds = { aggregate: 4, token: 10, detail: 18 };

function resolveNavigationTarget(ref: EntityRef): SpaceId | null {
  return ref.kind === "city" ? { kind: "City", cityId: ref.id } : null;
}

// Feedback do usuário (2026-08-07, segunda rodada): "o Z não é só em prédio, é em tudo" — mundo
// não tem nenhum dado de camada subterrânea/aérea (context.md gap 5), e ao contrário de
// prédio/cidade não dá pra "reformular" o terreno real sem fabricar dado de simulação que não
// existe. Efeito honesto aqui: tint visual sobre o terreno de verdade; muralha e portão mantêm
// identidade estável em qualquer Z — nunca finge um bioma ou uma construção nova.
function worldFloorLabel(floor: number): string {
  if (floor === 0) {
    return "Superfície";
  }
  return floor > 0 ? `${floor}º nível aéreo` : `${Math.abs(floor)}º nível subterrâneo`;
}

// T18: só as camadas que o cliente sabe desenhar entram no toggle real — Terrain é a base
// (`cells`), Rivers é overlay de pontos. As demais aparecem no painel só como NotYetModeled
// (`LayerPanel`), mesmo com `isModeled` verdadeiro na fixture (ex.: Biome — modelado no motor,
// mas sem consumidor no renderer ainda; toggle dela seria um checkbox que não muda nada).
const DEFAULT_ACTIVE_LAYERS: VisualLayerName[] = ["Terrain", "Rivers"];

export function WorldMapView({ snapshot, viewport, simulationStore, viewStore, selectionStore }: WorldMapViewProps) {
  const [floor, setFloor] = useState(0);
  const [activeLayers, setActiveLayers] = useState<ReadonlySet<VisualLayerName>>(
    () => new Set(DEFAULT_ACTIVE_LAYERS),
  );

  function toggleLayer(name: VisualLayerName): void {
    setActiveLayers((prev) => {
      const next = new Set(prev);
      if (next.has(name)) {
        next.delete(name);
      } else {
        next.add(name);
      }
      return next;
    });
  }

  const terrainLookup = useMemo(() => terrainColorLookup(snapshot), [snapshot]);
  const terrainDetail = useMemo(() => terrainDetailLookup(snapshot), [snapshot]);
  const cells = useMemo(
    () => ({
      width: snapshot.width,
      height: snapshot.height,
      backgroundColor: "#7fa8b2",
      atmosphereSeed: "world",
      colorAt: activeLayers.has("Terrain") ? terrainLookup : () => undefined,
      detailAt: activeLayers.has("Terrain") ? terrainDetail : undefined,
    }),
    [snapshot.width, snapshot.height, terrainLookup, terrainDetail, activeLayers],
  );

  const layers: ActiveLayer[] = useMemo(() => {
    const active: ActiveLayer[] = [];
    if (activeLayers.has("Rivers")) {
      active.push({ id: "Rivers", overlayPoints: riverOverlayPoints(snapshot) });
    }
    return sortActiveLayers(active);
  }, [snapshot, activeLayers]);

  // Feedback do usuário (2026-08-07): cidade não pode ser um círculo num ponto — ocupa a área
  // real do footprint (`bounds`) no grid, como o master prompt §6 sempre pediu. `position` é o
  // canto superior-esquerdo do footprint, `size` são as dimensões reais — o renderer desenha
  // qualquer entidade com `size.w>1 || size.h>1` como área, não como marcador circular.
  // Feedback do usuário (2026-08-07, rodada 2): cidade também não pode ficar só um retângulo —
  // mesma técnica do prédio (buildingFootprint.ts): muralha com portão em vez de preenchimento.
  const livingCities = useSyncExternalStore(
    (onStoreChange) => simulationStore.subscribe(onStoreChange),
    () => simulationStore.livingStateOf(WORLD).cities,
  );

  const cityEntities: AuthoritativeEntity[] = useMemo(
    () => mergeWorldCityMarkers(snapshot.cities, livingCities.values(), floor),
    [snapshot.cities, livingCities, floor],
  );

  return (
    <div className="map-fullscreen" data-testid="world-map-view">
      <div className="map-hud map-hud-top-left">
        <h2>Mapa-múndi</h2>
        <FloorSelector floor={floor} label={worldFloorLabel(floor)} onChange={setFloor} />
        <LayerPanel layers={snapshot.layers} active={activeLayers} onToggle={toggleLayer} />
        <EntityLegend />
      </div>

      {floor !== 0 && <div className={`z-layer-tint ${floor < 0 ? "z-layer-tint-below" : "z-layer-tint-above"}`} />}

      <MapView
        space={WORLD}
        viewport={viewport}
        cells={cells}
        layers={layers}
        lodThresholds={LOD_THRESHOLDS}
        simulationStore={simulationStore}
        viewStore={viewStore}
        selectionStore={selectionStore}
        staticEntities={cityEntities}
        resolveNavigationTarget={resolveNavigationTarget}
      />
    </div>
  );
}
