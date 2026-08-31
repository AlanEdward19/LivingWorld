import type { WorldFixture } from "../fixture/types";
import type { NavigationStore } from "../nav/NavigationStore";
import { NpcToken } from "../npc/NpcToken";
import { FollowButton } from "../components/FollowButton";
import { BackButton, MetricRow, SectionHeader, SectionLink } from "../components/InspectorPrimitives";
import { FamilyTree } from "./FamilyTree";

export interface HouseholdViewProps {
  fixture: WorldFixture;
  nav: NavigationStore;
  householdId: string;
}

/**
 * Household Inspector (redesign doc §15) — membros, árvore familiar de verdade (`FamilyTree`,
 * ancorada no head), estoque e eventos recentes, cada um com seus primitives em vez de `<dl>`/
 * `<ul>` cru.
 */
export function HouseholdView({ fixture, nav, householdId }: HouseholdViewProps) {
  const household = fixture.households.find((h) => h.id === householdId);
  if (!household) return null;

  const members = household.memberIds
    .map((id) => fixture.agents.find((a) => a.id === id))
    .filter((agent): agent is NonNullable<typeof agent> => agent !== undefined);

  const recentEvents = fixture.events.filter((e) => e.affectedHouseholdIds.includes(householdId));

  return (
    <div data-testid="household-view">
      {nav.canGoBack() && <BackButton onClick={() => nav.back()} />}
      <h1>{household.name}</h1>
      <FollowButton entityId={household.id} />

      <SectionHeader title="Members" trailing={members.length} />
      <ul data-testid="household-members">
        {members.map((member) => (
          <li key={member.id}>
            <button type="button" className="entity-row" onClick={() => nav.replace({ kind: "agent", id: member.id })}>
              <NpcToken id={member.id} size={40} />
              <span className="entity-row-text">
                <span className="entity-row-title">{member.name}</span>
                <span className="entity-row-meta"> · {member.age} · {member.profession}</span>
              </span>
            </button>
          </li>
        ))}
      </ul>

      <SectionHeader title="Family tree" />
      <FamilyTree fixture={fixture} agentId={household.headId} onSelectAgent={(id) => nav.replace({ kind: "agent", id })} />

      <SectionHeader title="Resources" />
      <dl>
        {Object.entries(household.stock).map(([resource, amount]) => (
          <MetricRow key={resource} label={resource} value={amount} />
        ))}
      </dl>

      <SectionHeader title="Recent" />
      <ul data-testid="household-recent-events">
        {recentEvents.map((event) => (
          <li key={event.eventId}>{event.summary}</li>
        ))}
      </ul>
      <SectionLink testId="view-timeline" onClick={() => nav.push({ kind: "timeline", scope: { type: "household", id: householdId } })}>
        View household timeline →
      </SectionLink>
    </div>
  );
}
