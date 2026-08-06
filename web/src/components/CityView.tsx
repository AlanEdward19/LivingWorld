import type { CitySnapshot } from "../types";

export interface CityViewProps {
  snapshot: CitySnapshot;
  onSelectBuilding: (buildingId: string) => void;
  onBack: () => void;
}

/// Fase 15, T8 (VTT-03, VTT-05, VTT-11): foco de cidade — moradores materializados com
/// posição/atividade (FOW já aplicado pelo servidor em T7, o cliente só renderiza o que recebeu),
/// prédios clicáveis (drill-down pra interior) e pool agregado como resumo do resto da população.
export function CityView({ snapshot, onSelectBuilding, onBack }: CityViewProps) {
  return (
    <div data-testid="city-view">
      <button type="button" onClick={onBack}>
        ← mapa-múndi
      </button>
      <h2>Cidade {snapshot.id.value.slice(0, 8)}</h2>
      <p>
        Pool agregado: {snapshot.aggregatePool.count} habitantes não materializados (riqueza{" "}
        {snapshot.aggregatePool.wealthSum}, saúde {snapshot.aggregatePool.healthSum})
      </p>

      <h3>Moradores visíveis ({snapshot.residents.length})</h3>
      <ul aria-label="moradores">
        {snapshot.residents.map((resident) => (
          <li key={resident.id.value}>
            npc {resident.id.value} em ({resident.location.x},{resident.location.y})
            {resident.currentAction !== null && ` — ação ${resident.currentAction}`}
          </li>
        ))}
      </ul>

      <h3>Prédios ({snapshot.buildings.length})</h3>
      <ul aria-label="predios">
        {snapshot.buildings.map((building) => (
          <li key={building.id.value}>
            <button type="button" onClick={() => onSelectBuilding(String(building.id.value))}>
              prédio {building.id.value} (tipo {building.buildingTypeId})
            </button>
          </li>
        ))}
      </ul>
    </div>
  );
}
