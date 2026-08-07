// Fase 15.1, T15: inspector de prédio — id/tipo reais, posição marcada como aproximada (layout
// de anel client-side, `CityView.tsx` — `Building` não tem `CellCoord` no domínio, context.md
// gap 5). Sem catálogo de tipo de prédio no projeto: o id cru é o único rótulo disponível hoje
// (spec.md AC4 — "id cru apenas em modo avançado" só se aplica quando existe catálogo).
import { FollowButton } from "./FollowButton";
import type { SimulationStore } from "../../state/simulationStore";
import type { ViewStore } from "../../state/viewStore";
import type { EntityRef } from "../../map-engine/types";
import type { CityBuildingMarker } from "../../types";

export interface BuildingInspectorProps {
  /** `entityRef.space` é a CIDADE onde o prédio está (um prédio nunca está "dentro" de si mesmo). */
  entityRef: EntityRef;
  simulationStore: SimulationStore;
  viewStore: ViewStore;
}

export function BuildingInspector({ entityRef, simulationStore, viewStore }: BuildingInspectorProps) {
  if (entityRef.space.kind !== "City") {
    throw new Error(`building EntityRef must sit in a City space, got "${entityRef.space.kind}"`);
  }
  const cityId = entityRef.space.cityId;
  const citySnapshot = simulationStore.currentPayload<{ buildings: CityBuildingMarker[] }>({
    kind: "City",
    cityId,
  });
  const marker = citySnapshot?.buildings.find((b) => String(b.id.value) === entityRef.id);

  return (
    <div>
      <h3>Prédio {entityRef.id}</h3>

      <dl>
        <dt>Tipo</dt>
        <dd>{marker ? marker.buildingTypeId : "—"}</dd>
      </dl>

      <p role="note" className="approximate-note">
        posição no mapa é layout aproximado (sem dado real)
      </p>

      <div className="entity-inspector-actions">
        <FollowButton entityRef={entityRef} viewStore={viewStore} />
        <button
          type="button"
          onClick={() => viewStore.enter({ kind: "Building", buildingId: entityRef.id, cityId })}
        >
          Abrir
        </button>
      </div>
    </div>
  );
}
