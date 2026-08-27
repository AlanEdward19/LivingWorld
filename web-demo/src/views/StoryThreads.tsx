import type { WorldFixture } from "../fixture/types";
import type { NavigationStore } from "../nav/NavigationStore";

export interface StoryThreadsProps {
  fixture: WorldFixture;
  nav: NavigationStore;
}

/**
 * Story Threads (doc#126) — "The Oakbridge Food Crisis" como card clicável, números exatos do
 * fixture (18 events · 4 households · 11 Agents · 6 systems). Clique abre o Causal Explorer no
 * evento raiz do thread (o evento sem `causeEventId` dentro da cadeia).
 */
export function StoryThreads({ fixture, nav }: StoryThreadsProps) {
  return (
    <ul data-testid="story-threads">
      {fixture.storyThreads.map((thread) => {
        const rootEventId =
          fixture.events.find((e) => thread.eventIds.includes(e.eventId) && e.causeEventId === null)?.eventId ??
          thread.eventIds[0];
        return (
          <li key={thread.id}>
            <button type="button" data-testid="story-thread-card" onClick={() => nav.push({ kind: "causal", eventId: rootEventId })}>
              <h2>{thread.title}</h2>
              <p data-testid="story-thread-stats">
                {thread.eventIds.length} events · {thread.householdIds.length} households · {thread.agentIds.length} Agents ·{" "}
                {thread.systemsTouched.length} systems
              </p>
            </button>
          </li>
        );
      })}
    </ul>
  );
}
