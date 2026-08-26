import type { WorldFixture } from "../fixture/types";
import type { NavigationStore } from "../nav/NavigationStore";
import { NpcToken } from "../npc/NpcToken";
import { FollowButton } from "../components/FollowButton";

export interface HouseholdViewProps {
  fixture: WorldFixture;
  nav: NavigationStore;
  householdId: string;
}

/**
 * Membros, árvore familiar simples, estoque e eventos recentes de um household (doc#124).
 */
export function HouseholdView({ fixture, nav, householdId }: HouseholdViewProps) {
  const household = fixture.households.find((h) => h.id === householdId);
  if (!household) return null;

  const members = household.memberIds
    .map((id) => fixture.agents.find((a) => a.id === id))
    .filter((agent): agent is NonNullable<typeof agent> => agent !== undefined);
  const head = members.find((m) => m.id === household.headId);
  const spouse = members.find((m) => m.relationships.some((r) => r.label === "spouse"));
  const children = members.filter((m) => m.id !== household.headId && m.id !== spouse?.id);

  const recentEvents = fixture.events.filter((e) => e.affectedHouseholdIds.includes(householdId));

  return (
    <div data-testid="household-view">
      <h1>{household.name}</h1>
      <FollowButton entityId={household.id} />

      <ul data-testid="household-members">
        {members.map((member) => (
          <li key={member.id}>
            <button type="button" onClick={() => nav.push({ kind: "agent", id: member.id })}>
              <NpcToken id={member.id} size={48} />
              {member.name}
            </button>
          </li>
        ))}
      </ul>

      <div data-testid="family-tree">
        {head && <p data-testid="family-tree-head">{head.name}</p>}
        {spouse && <p data-testid="family-tree-spouse">{spouse.name}</p>}
        {children.map((child) => (
          <p key={child.id} data-testid="family-tree-child">
            {child.name}
          </p>
        ))}
      </div>

      <dl data-testid="household-stock">
        {Object.entries(household.stock).map(([resource, amount]) => (
          <div key={resource}>
            <dt>{resource}</dt>
            <dd>{amount}</dd>
          </div>
        ))}
      </dl>

      <ul data-testid="household-recent-events">
        {recentEvents.map((event) => (
          <li key={event.eventId}>{event.summary}</li>
        ))}
      </ul>
    </div>
  );
}
