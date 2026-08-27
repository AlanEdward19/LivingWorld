import { useState, useSyncExternalStore } from "react";
import type { WorldFixture } from "../fixture/types";
import type { NavigationStore } from "../nav/NavigationStore";
import { NpcToken } from "../npc/NpcToken";
import { WhyPanel } from "./WhyPanel";
import { FollowButton } from "../components/FollowButton";
import { Popup } from "../components/ContextOverlay";
import { EntityRow, MetricRow, SectionHeader, SectionLink, StatusChips } from "../components/InspectorPrimitives";
import { modeStore } from "../state/modeStore";

export interface AgentViewProps {
  fixture: WorldFixture;
  nav: NavigationStore;
  agentId: string;
}

type OpenPopup = "body" | "relationships" | "why" | null;

/**
 * Agent Inspector (redesign doc §13) — CURRENTLY/STATUS/BODY/HOUSEHOLD/RELATIONSHIPS/RECENT/WHY,
 * cada seção compacta (2-3 linhas + link), conteúdo profundo (physical details, relationships
 * completas, "why") vira Popup (Nível 3, doc §14/§19) em vez de ficar permanentemente expandido
 * na sidebar.
 */
export function AgentView({ fixture, nav, agentId }: AgentViewProps) {
  const [openPopup, setOpenPopup] = useState<OpenPopup>(null);
  const mode = useSyncExternalStore(
    (listener) => modeStore.subscribe(listener),
    () => modeStore.currentMode(),
  );
  const agent = fixture.agents.find((a) => a.id === agentId);
  if (!agent) return null;

  const settlement = fixture.settlements.find((s) => s.id === agent.settlementId);
  const household = agent.householdId ? fixture.households.find((h) => h.id === agent.householdId) : undefined;
  const relationships = agent.relationships.map((relationship) => ({
    ...relationship,
    name: fixture.agents.find((a) => a.id === relationship.withAgentId)?.name ?? relationship.withAgentId,
  }));

  return (
    <div data-testid="agent-view">
      <NpcToken id={agent.id} size={64} />
      <h1>{agent.name}</h1>
      <p data-testid="agent-age-profession">
        {agent.age} · {agent.profession}
      </p>
      <p data-testid="agent-location">{settlement?.name}</p>
      <FollowButton entityId={agent.id} />

      <SectionHeader title="Currently" />
      <p data-testid="agent-intent">{agent.currentIntent}</p>

      <SectionHeader title="Status" />
      <StatusChips testId="agent-condition" items={agent.condition} />

      <SectionHeader title="Body" />
      <p data-testid="agent-body">{agent.bodySummary.build}</p>
      <SectionLink onClick={() => setOpenPopup("body")}>View physical details →</SectionLink>

      {household && (
        <>
          <SectionHeader title="Household" />
          <EntityRow
            testId="agent-household"
            title={household.name}
            meta={`${household.memberIds.length} members`}
            onClick={() => nav.replace({ kind: "household", id: household.id })}
          />
        </>
      )}

      <SectionHeader title="Relationships" />
      <ul data-testid="agent-relationships">
        {relationships.slice(0, 2).map((relationship) => (
          <li key={relationship.withAgentId}>
            {relationship.name} · {relationship.label}
          </li>
        ))}
      </ul>
      {relationships.length > 0 && <SectionLink onClick={() => setOpenPopup("relationships")}>View relationships →</SectionLink>}

      <SectionHeader title="Recent" />
      <ul data-testid="agent-recent-events">
        {agent.recentLifeEvents.slice(0, 3).map((event, index) => (
          <li key={index}>{event}</li>
        ))}
      </ul>
      <SectionLink testId="view-full-life" onClick={() => nav.push({ kind: "life", agentId })}>
        View life timeline →
      </SectionLink>
      <button
        type="button"
        data-testid="view-timeline"
        onClick={() => nav.push({ kind: "timeline", scope: { type: "agent", id: agentId } })}
      >
        View Timeline
      </button>

      {agent.whyFactors.length > 0 && (
        <>
          <SectionHeader title="Why?" />
          <p data-testid="why-summary">{agent.whyFactors.length} contributing factors</p>
          <SectionLink onClick={() => setOpenPopup("why")}>Explain decision →</SectionLink>
        </>
      )}

      <button type="button" data-testid="toggle-mode" onClick={() => modeStore.toggleMode()}>
        {mode === "debug" ? "Switch to Experience Mode" : "Switch to Debug Mode"}
      </button>

      {openPopup === "body" && (
        <Popup title="Physical details" onClose={() => setOpenPopup(null)}>
          <div data-testid="agent-body-detail">
            <dl>
              <div>
                <dt>Height</dt>
                <dd>{agent.bodyDetail.height}</dd>
              </div>
              <div>
                <dt>Weight</dt>
                <dd>{agent.bodyDetail.weight}</dd>
              </div>
              <div>
                <dt>Muscle mass</dt>
                <dd>{agent.bodyDetail.muscleMass}</dd>
              </div>
              <div>
                <dt>Fat mass</dt>
                <dd>{agent.bodyDetail.fatMass}</dd>
              </div>
              <div>
                <dt>Physical strength</dt>
                <dd>{agent.bodyDetail.physicalStrength}</dd>
              </div>
              <div>
                <dt>Endurance</dt>
                <dd>{agent.bodyDetail.endurance}</dd>
              </div>
              <div>
                <dt>Mobility</dt>
                <dd>{agent.bodyDetail.mobility}</dd>
              </div>
            </dl>
            <p>Current injuries: {agent.bodyDetail.currentInjuries.length > 0 ? agent.bodyDetail.currentInjuries.join(", ") : "None"}</p>
            <p>Diseases: {agent.bodyDetail.diseases.length > 0 ? agent.bodyDetail.diseases.join(", ") : "None"}</p>
            <p>Conditions: {agent.bodyDetail.conditions.length > 0 ? agent.bodyDetail.conditions.join(", ") : "None"}</p>

            {agent.bodyDetail.affects.length > 0 && (
              <div data-testid="agent-body-affects">
                <h3>What this affects</h3>
                {agent.bodyDetail.affects.map((affect) => (
                  <div key={affect.trait}>
                    <strong>{affect.trait}</strong>
                    <ul>
                      {affect.effects.map((effect) => (
                        <li key={effect}>{effect}</li>
                      ))}
                    </ul>
                  </div>
                ))}
              </div>
            )}
          </div>
        </Popup>
      )}

      {openPopup === "relationships" && (
        <Popup title={`${agent.name}'s relationships`} onClose={() => setOpenPopup(null)}>
          <ul>
            {relationships.map((relationship) => (
              <li key={relationship.withAgentId}>
                <MetricRow label={relationship.name} value={relationship.label} />
              </li>
            ))}
          </ul>
        </Popup>
      )}

      {openPopup === "why" && (
        <Popup title={`Why is ${agent.name} ${agent.currentIntent.charAt(0).toLowerCase() + agent.currentIntent.slice(1)}?`} onClose={() => setOpenPopup(null)}>
          <WhyPanel
            factors={agent.whyFactors}
            onFactorClick={(eventId) => {
              setOpenPopup(null);
              nav.push({ kind: "causal", eventId });
            }}
            debug={mode === "debug"}
            events={fixture.events}
          />
        </Popup>
      )}
    </div>
  );
}
