import { useState, useSyncExternalStore } from "react";
import type { WorldFixture } from "../fixture/types";
import type { NavigationStore } from "../nav/NavigationStore";
import { NpcToken } from "../npc/NpcToken";
import { WhyPanel } from "./WhyPanel";
import { FollowButton } from "../components/FollowButton";
import { modeStore } from "../state/modeStore";

export interface AgentViewProps {
  fixture: WorldFixture;
  nav: NavigationStore;
  agentId: string;
}

/**
 * Identidade/profissão/localização/intent/condição/corpo/household/relações/eventos recentes
 * (doc#109/#113) + botão Why? (doc#114).
 */
export function AgentView({ fixture, nav, agentId }: AgentViewProps) {
  const [whyOpen, setWhyOpen] = useState(false);
  const mode = useSyncExternalStore(
    (listener) => modeStore.subscribe(listener),
    () => modeStore.currentMode(),
  );
  const agent = fixture.agents.find((a) => a.id === agentId);
  if (!agent) return null;

  const settlement = fixture.settlements.find((s) => s.id === agent.settlementId);
  const household = agent.householdId ? fixture.households.find((h) => h.id === agent.householdId) : undefined;

  return (
    <div data-testid="agent-view">
      <NpcToken id={agent.id} size={64} />
      <h1>{agent.name}</h1>
      <FollowButton entityId={agent.id} />
      <p data-testid="agent-age-profession">
        {agent.age} · {agent.profession}
      </p>
      <p data-testid="agent-location">{settlement?.name}</p>
      <p data-testid="agent-intent">{agent.currentIntent}</p>
      <p data-testid="agent-condition">{agent.condition.join(" · ")}</p>
      <p data-testid="agent-body">{agent.bodySummary.build}</p>

      {household && (
        <button type="button" data-testid="agent-household" onClick={() => nav.push({ kind: "household", id: household.id })}>
          {household.name}
        </button>
      )}

      <ul data-testid="agent-relationships">
        {agent.relationships.map((relationship) => {
          const other = fixture.agents.find((a) => a.id === relationship.withAgentId);
          return (
            <li key={relationship.withAgentId}>
              {other?.name ?? relationship.withAgentId} · {relationship.label}
            </li>
          );
        })}
      </ul>

      <ul data-testid="agent-recent-events">
        {agent.recentLifeEvents.map((event, index) => (
          <li key={index}>{event}</li>
        ))}
      </ul>

      <button type="button" data-testid="view-full-life" onClick={() => nav.push({ kind: "life", agentId })}>
        View full life
      </button>

      <button type="button" onClick={() => setWhyOpen((open) => !open)}>
        Why?
      </button>
      {whyOpen && (
        <WhyPanel
          factors={agent.whyFactors}
          onFactorClick={(eventId) => nav.push({ kind: "causal", eventId })}
          debug={mode === "debug"}
          events={fixture.events}
        />
      )}

      <button type="button" data-testid="toggle-mode" onClick={() => modeStore.toggleMode()}>
        {mode === "debug" ? "Switch to Experience Mode" : "Switch to Debug Mode"}
      </button>

      <button
        type="button"
        data-testid="view-timeline"
        onClick={() => nav.push({ kind: "timeline", scope: { type: "agent", id: agentId } })}
      >
        View Timeline
      </button>
    </div>
  );
}
