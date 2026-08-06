import type { GlobalSnapshot } from "../types";

export interface WorldMapViewProps {
  snapshot: GlobalSnapshot;
  onSelectCity: (cityId: string) => void;
}

/// Fase 15, T8 (VTT-01, VTT-04, VTT-06): mapa-múndi simplificado — cidades clicáveis (drill-down
/// pra T5), NPCs externos como marcadores, e legenda de camadas mostrando o que já é dado real
/// vs ainda não modelado (T4's LayerBuildResult.NotYetModeled).
export function WorldMapView({ snapshot, onSelectCity }: WorldMapViewProps) {
  return (
    <div data-testid="world-map-view">
      <h2>Mapa-múndi</h2>
      <ul aria-label="cidades">
        {snapshot.cities.map((city) => (
          <li key={city.id.value}>
            <button type="button" onClick={() => onSelectCity(city.id.value)}>
              cidade {city.id.value.slice(0, 8)} — pop. {city.population} — ({city.location.x},{city.location.y})
            </button>
          </li>
        ))}
      </ul>

      <h3>NPCs externos ({snapshot.externalNpcs.length})</h3>
      <ul aria-label="npcs-externos">
        {snapshot.externalNpcs.map((npc) => (
          <li key={npc.id.value}>
            npc {npc.id.value} em ({npc.location.x},{npc.location.y})
          </li>
        ))}
      </ul>

      <h3>Camadas</h3>
      <ul aria-label="camadas-globais">
        {Object.entries(snapshot.layers).map(([name, layer]) => (
          <li key={name}>
            {name}: {layer.isModeled ? "disponível" : "ainda não modelada"}
          </li>
        ))}
      </ul>
    </div>
  );
}
