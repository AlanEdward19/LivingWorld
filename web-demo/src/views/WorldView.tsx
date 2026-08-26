import type { WorldFixture } from "../fixture/types";
import type { NavigationStore } from "../nav/NavigationStore";
import { SemanticZoomMap } from "../map/SemanticZoomMap";

export interface WorldViewProps {
  fixture: WorldFixture;
  nav: NavigationStore;
}

/**
 * Tela raiz (doc#107) — mapa nível "mundo" + resumo do que está acontecendo, derivado do
 * fixture. Clique num assentamento — mapa OU lista — navega pra Settlement View, mesmo
 * comportamento nos dois caminhos (spec P1b AC4, wiring de mapa em T18).
 */
export function WorldView({ fixture, nav }: WorldViewProps) {
  return (
    <div data-testid="world-view">
      <h1>{fixture.world.name}</h1>
      <p data-testid="world-summary">{fixture.world.summary}</p>

      <ul data-testid="settlement-list">
        {fixture.settlements.map((settlement) => (
          <li key={settlement.id}>
            <button type="button" onClick={() => nav.push({ kind: "settlement", id: settlement.id })}>
              {settlement.name}
            </button>
          </li>
        ))}
      </ul>

      <SemanticZoomMap
        fixture={fixture}
        onSelectSettlement={(settlementId) => nav.push({ kind: "settlement", id: settlementId })}
        onSelectNpc={() => {}}
      />
    </div>
  );
}
