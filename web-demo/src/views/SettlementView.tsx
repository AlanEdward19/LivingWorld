import type { WorldFixture } from "../fixture/types";
import type { NavigationStore } from "../nav/NavigationStore";
import { EntityRow, MetricRow, SectionHeader, SectionLink } from "../components/InspectorPrimitives";

export interface SettlementViewProps {
  fixture: WorldFixture;
  nav: NavigationStore;
  settlementId: string;
}

/**
 * Settlement Inspector (redesign doc §16) — KEY METRICS/HOUSEHOLDS/PLACES/RECENT, cada seção
 * compacta com `MetricRow`/`EntityRow` em vez de `<dl>`/`<ul>` cru (bug real reportado pelo
 * usuário: "design bem bagunçado, muito texto jogado"). Sem `FollowButton` — "seguir" agora
 * significa a câmera acompanhar um agent se movendo (ver AD em STATE.md), e um settlement
 * inteiro não anda; o botão não fazia sentido aqui (outra queixa direta do usuário).
 */
export function SettlementView({ fixture, nav, settlementId }: SettlementViewProps) {
  const settlement = fixture.settlements.find((s) => s.id === settlementId);
  if (!settlement) return null;

  const households = fixture.households.filter((h) => h.settlementId === settlementId);
  const places = settlement.buildings;
  const recentEvents = fixture.events.filter((e) => e.settlementId === settlementId);

  return (
    <div data-testid="settlement-view">
      <h1>{settlement.name}</h1>

      <SectionHeader title="Key metrics" />
      <dl data-testid="settlement-pulse">
        <MetricRow label="Population" value={<span data-testid="pulse-population">{settlement.population}</span>} />
        <MetricRow label="Trend" value={<span data-testid="pulse-population-trend">{settlement.populationTrend}</span>} />
        <MetricRow label="Food" value={<span data-testid="pulse-food">{settlement.food}</span>} />
        <MetricRow label="Employment" value={<span data-testid="pulse-employment">{settlement.employment}</span>} />
        <MetricRow label="Migration" value={<span data-testid="pulse-migration">{settlement.migration}</span>} />
        <MetricRow label="Construction" value={<span data-testid="pulse-construction">{settlement.construction}</span>} />
      </dl>

      <SectionHeader title="Households" trailing={households.length} />
      <ul data-testid="household-list">
        {households.slice(0, 4).map((household) => (
          <li key={household.id}>
            <EntityRow
              title={household.name}
              meta={`${household.memberIds.length} members`}
              onClick={() => nav.replace({ kind: "household", id: household.id })}
            />
          </li>
        ))}
      </ul>

      <SectionHeader title="Places" trailing={places.length} />
      <ul data-testid="settlement-places">
        {places.slice(0, 4).map((building) => (
          <li key={building.id}>
            <EntityRow title={building.name} meta={building.kind} onClick={() => nav.push({ kind: "building", id: building.id })} />
          </li>
        ))}
      </ul>

      <SectionHeader title="Recent" />
      <ul data-testid="settlement-recent-events">
        {recentEvents.slice(0, 4).map((event) => (
          <li key={event.eventId}>{event.summary}</li>
        ))}
      </ul>
      <SectionLink testId="view-timeline" onClick={() => nav.push({ kind: "timeline", scope: { type: "settlement", id: settlementId } })}>
        Open settlement timeline →
      </SectionLink>
    </div>
  );
}
