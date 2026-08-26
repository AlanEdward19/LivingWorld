import type { WorldFixture } from "../fixture/types";
import type { NavigationStore } from "../nav/NavigationStore";
import { FollowButton } from "../components/FollowButton";

export interface SettlementViewProps {
  fixture: WorldFixture;
  nav: NavigationStore;
  settlementId: string;
}

/**
 * Settlement Pulse (doc#108/#125) — população, food/employment/migration/construction,
 * eventos recentes. Conteúdo do Inspector quando um settlement está selecionado; o mapa
 * (nível distrito/agente) mora no `CenterStage` (doc §5: Inspector é o painel contextual da
 * ENTIDADE selecionada, o mapa é o "World" central — os dois nunca se sobrepõem).
 */
export function SettlementView({ fixture, nav, settlementId }: SettlementViewProps) {
  const settlement = fixture.settlements.find((s) => s.id === settlementId);
  if (!settlement) return null;

  const households = fixture.households.filter((h) => h.settlementId === settlementId);
  const recentEvents = fixture.events.filter((e) => e.settlementId === settlementId);

  return (
    <div data-testid="settlement-view">
      <h1>{settlement.name}</h1>
      <FollowButton entityId={settlement.id} />

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
            <button type="button" onClick={() => nav.replace({ kind: "household", id: household.id })}>
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

      <button
        type="button"
        data-testid="view-timeline"
        onClick={() => nav.push({ kind: "timeline", scope: { type: "settlement", id: settlementId } })}
      >
        View Timeline
      </button>
    </div>
  );
}
