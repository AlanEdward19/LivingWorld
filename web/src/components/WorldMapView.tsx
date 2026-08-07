// Fase 15.1, T14: mapa-múndi como configuração de `MapView` (design.md; master prompt §30-32) —
// não instancia canvas próprio, não guarda seleção local nem zoom local. Cidades entram como
// `staticEntities` (não têm delta de tick — `SimulationStore.entitiesOf` só extrai NPC externo,
// que já vem dinamicamente); double-click numa cidade resolve `{kind:"City"}` e o próprio
// `MapView` chama `ViewStore.enter`.
import { useMemo } from "react";
import { MapView } from "./MapView";
import { LayerLegend } from "./LayerLegend";
import { EntityLegend } from "./EntityLegend";
import { terrainColorLookup, riverOverlayPoints } from "../worldMapData";
import type { Viewport } from "../map-engine/Camera";
import type { ActiveLayer } from "../map-engine/renderer";
import type { LodThresholds } from "../map-engine/lod";
import type { AuthoritativeEntity, EntityRef, SpaceId } from "../map-engine/types";
import type { SimulationStore } from "../state/simulationStore";
import type { ViewStore } from "../state/viewStore";
import type { SelectionStore } from "../state/selectionStore";
import type { FutureGlobalSnapshot } from "../data/contracts";
import { CATEGORY_COLOR } from "../map-engine/categoryColors";

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

export function WorldMapView({ snapshot, viewport, simulationStore, viewStore, selectionStore }: WorldMapViewProps) {
  const cells = useMemo(
    () => ({ width: snapshot.width, height: snapshot.height, colorAt: terrainColorLookup(snapshot) }),
    [snapshot],
  );

  const layers: ActiveLayer[] = useMemo(
    () => [{ id: "Rivers", overlayPoints: riverOverlayPoints(snapshot) }],
    [snapshot],
  );

  // Feedback do usuário (2026-08-07): cidade não pode ser um círculo num ponto — ocupa a área
  // real do footprint (`bounds`) no grid, como o master prompt §6 sempre pediu. `position` é o
  // canto superior-esquerdo do footprint, `size` são as dimensões reais — o renderer desenha
  // qualquer entidade com `size.w>1 || size.h>1` como área, não como marcador circular.
  const cityEntities: AuthoritativeEntity[] = useMemo(
    () =>
      snapshot.cities.map((city) => ({
        ref: { kind: "city" as const, id: city.id.value, space: WORLD },
        position: { x: city.bounds.x, y: city.bounds.y },
        size: { w: city.bounds.width, h: city.bounds.height },
        sizeIsDerived: city.boundsAreDerived,
        color: CATEGORY_COLOR.city,
      })),
    [snapshot.cities],
  );

  return (
    <div className="map-fullscreen" data-testid="world-map-view">
      <div className="map-hud map-hud-top-left">
        <h2>Mapa-múndi</h2>
        <LayerLegend layers={snapshot.layers} />
        <EntityLegend />
      </div>

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
