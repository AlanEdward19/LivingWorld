import { useState } from "react";
import type { WorldFixture } from "../fixture/types";
import type { NavigationStore } from "../nav/NavigationStore";
import { SemanticZoomMap, type ZoomLevel } from "../map/SemanticZoomMap";

export interface SettlementViewProps {
  fixture: WorldFixture;
  nav: NavigationStore;
  settlementId: string;
}

/**
 * Settlement Pulse (doc#108/#125) — população, food/employment/migration/construction,
 * eventos recentes — + mapa com zoom "distrito"/"agente" (toggle local, spec P1b AC2-4).
 * Clique num NPC no mapa nível "agente" navega pra Agent View — mesmo comportamento de
 * clicar num membro na lista da Household View (T18, wiring de mapa).
 */
export function SettlementView({ fixture, nav, settlementId }: SettlementViewProps) {
  const [mapLevel, setMapLevel] = useState<Extract<ZoomLevel, "district" | "agent">>("district");
  const settlement = fixture.settlements.find((s) => s.id === settlementId);
  if (!settlement) return null;

  const households = fixture.households.filter((h) => h.settlementId === settlementId);
  const recentEvents = fixture.events.filter((e) => e.settlementId === settlementId);

  return (
    <div data-testid="settlement-view">
      <h1>{settlement.name}</h1>

      <dl data-testid="settlement-pulse">
        <dt>Population</dt>
        <dd data-testid="pulse-population">{settlement.population}</dd>
        <dt>Population trend</dt>
        <dd data-testid="pulse-population-trend">{settlement.populationTrend}</dd>
        <dt>Food</dt>
        <dd data-testid="pulse-food">{settlement.food}</dd>
        <dt>Employment</dt>
        <dd data-testid="pulse-employment">{settlement.employment}</dd>
        <dt>Migration</dt>
        <dd data-testid="pulse-migration">{settlement.migration}</dd>
        <dt>Construction</dt>
        <dd data-testid="pulse-construction">{settlement.construction}</dd>
      </dl>

      <ul data-testid="household-list">
        {households.map((household) => (
          <li key={household.id}>
            <button type="button" onClick={() => nav.push({ kind: "household", id: household.id })}>
              {household.name}
            </button>
          </li>
        ))}
      </ul>

      <ul data-testid="settlement-recent-events">
        {recentEvents.map((event) => (
          <li key={event.eventId}>{event.summary}</li>
        ))}
      </ul>

      <div data-testid="map-level-toggle">
        <button type="button" onClick={() => setMapLevel("district")} aria-pressed={mapLevel === "district"}>
          District view
        </button>
        <button type="button" onClick={() => setMapLevel("agent")} aria-pressed={mapLevel === "agent"}>
          Agent view
        </button>
      </div>

      <SemanticZoomMap
        fixture={fixture}
        level={mapLevel}
        settlementId={settlementId}
        onSelectSettlement={() => {}}
        onSelectNpc={(agentId) => nav.push({ kind: "agent", id: agentId })}
      />
    </div>
  );
}
