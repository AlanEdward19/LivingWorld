import type { WorldFixture } from "../fixture/types";
import type { NavigationStore } from "../nav/NavigationStore";
import { EntityRow, MetricRow, SectionHeader, SectionLink } from "../components/InspectorPrimitives";

export interface WorldViewProps {
  fixture: WorldFixture;
  nav: NavigationStore;
}

/**
 * World Inspector — pedido do usuário 2026-08-27: "clicar em qualquer coisa que não seja casas
 * ou NPC dentro de uma cidade mostra detalhes da cidade [no Inspector]... quero o equivalente no
 * mapa mundi". Mesmo padrão do `SettlementView` (KEY METRICS/lista/RECENT com os primitives do
 * Inspector), abre quando `WorldStage.onBackgroundClick` dispara (clicou em terreno vazio, nem
 * settlement nem agent) — o "container" no nível mundo é o mundo em si.
 */
export function WorldView({ fixture, nav }: WorldViewProps) {
  const population = fixture.settlements.reduce((sum, s) => sum + s.population, 0);
  const migrationActive = fixture.settlements.some((s) => s.migration !== "stable");
  const notableEventIds = new Set(fixture.storyThreads.flatMap((t) => t.eventIds));
  const recentEvents = fixture.events.slice(-4).reverse();

  return (
    <div data-testid="world-view">
      <h1>{fixture.world.name}</h1>
      <p data-testid="world-summary">{fixture.world.summary}</p>

      <SectionHeader title="Key metrics" />
      <dl data-testid="world-pulse">
        <MetricRow label="Population" value={<span data-testid="world-pulse-population">{population}</span>} />
        <MetricRow label="Settlements" value={fixture.settlements.length} />
        <MetricRow label="Migration" value={migrationActive ? "Active" : "Stable"} />
        <MetricRow label="Notable events" value={notableEventIds.size} />
      </dl>

      <SectionHeader title="Settlements" trailing={fixture.settlements.length} />
      <ul data-testid="world-settlements">
        {fixture.settlements.map((settlement) => (
          <li key={settlement.id}>
            <EntityRow
              title={settlement.name}
              meta={`${settlement.population} residents`}
              onClick={() => nav.replace({ kind: "settlement", id: settlement.id })}
            />
          </li>
        ))}
      </ul>

      <SectionHeader title="Recent" />
      <ul data-testid="world-recent-events">
        {recentEvents.map((event) => (
          <li key={event.eventId}>{event.summary}</li>
        ))}
      </ul>
      <SectionLink testId="view-timeline" onClick={() => nav.push({ kind: "timeline", scope: { type: "world" } })}>
        Open world timeline →
      </SectionLink>
    </div>
  );
}
