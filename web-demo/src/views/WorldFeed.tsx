import type { WorldEventFixture, WorldFixture } from "../fixture/types";

export interface WorldFeedProps {
  fixture: WorldFixture;
}

function relevanceOf(event: WorldEventFixture): number {
  return event.affectedAgentIds.length + event.affectedHouseholdIds.length;
}

interface FeedGroup {
  tick: string;
  events: WorldEventFixture[];
}

function groupByTick(events: WorldEventFixture[]): FeedGroup[] {
  const groups: FeedGroup[] = [];
  for (const event of events) {
    const group = groups.find((g) => g.tick === event.tick);
    if (group) {
      group.events.push(event);
    } else {
      groups.push({ tick: event.tick, events: [event] });
    }
  }
  for (const group of groups) {
    group.events.sort((a, b) => relevanceOf(b) - relevanceOf(a));
  }
  return groups;
}

/**
 * Lista cronológica agrupada por timestamp (doc#129-130), com os eventos de cada grupo
 * ordenados por relevância (nº de agents/households afetados, proxy simples de prioridade).
 */
export function WorldFeed({ fixture }: WorldFeedProps) {
  const groups = groupByTick(fixture.events);

  return (
    <div data-testid="world-feed">
      {groups.map((group) => (
        <section key={group.tick} data-testid="world-feed-group">
          <h2>{group.tick}</h2>
          <ul>
            {group.events.map((event) => (
              <li key={event.eventId}>{event.summary}</li>
            ))}
          </ul>
        </section>
      ))}
    </div>
  );
}
