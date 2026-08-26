import { useSyncExternalStore } from "react";
import type { WorldFixture, WorldEventFixture } from "../fixture/types";
import type { NavigationStore } from "../nav/NavigationStore";
import { modeStore } from "../state/modeStore";

export interface CausalExplorerProps {
  fixture: WorldFixture;
  nav: NavigationStore;
  eventId: string;
}

function ancestorChain(events: WorldEventFixture[], event: WorldEventFixture): WorldEventFixture[] {
  const chain: WorldEventFixture[] = [];
  let current = event;
  while (current.causeEventId) {
    const cause = events.find((e) => e.eventId === current.causeEventId);
    if (!cause) break;
    chain.push(cause);
    current = cause;
  }
  return chain;
}

function descendantsOf(events: WorldEventFixture[], eventId: string): WorldEventFixture[] {
  const direct = events.filter((e) => e.causeEventId === eventId);
  return direct.flatMap((child) => [child, ...descendantsOf(events, child.eventId)]);
}

interface ConsequenceTreeProps {
  events: WorldEventFixture[];
  rootId: string;
  onEventClick: (eventId: string) => void;
  debug: boolean;
}

function ConsequenceTree({ events, rootId, onEventClick, debug }: ConsequenceTreeProps) {
  const children = events.filter((e) => e.causeEventId === rootId);
  if (children.length === 0) return null;
  return (
    <ul>
      {children.map((child) => (
        <li key={child.eventId}>
          <button type="button" onClick={() => onEventClick(child.eventId)}>
            {debug ? (
              <span data-testid="consequence-debug">
                {child.eventId} · {child.kind} · {child.sourceSystem} · {child.tick}
              </span>
            ) : (
              child.summary
            )}
          </button>
          <ConsequenceTree events={events} rootId={child.eventId} onEventClick={onEventClick} debug={debug} />
        </li>
      ))}
    </ul>
  );
}

/**
 * `WHY? → causa` + `CONSEQUENCES → árvore ramificada` (doc#117-118), sistemas envolvidos
 * (união da cadeia ancestral + evento + toda a árvore de consequências).
 */
export function CausalExplorer({ fixture, nav, eventId }: CausalExplorerProps) {
  const mode = useSyncExternalStore(
    (listener) => modeStore.subscribe(listener),
    () => modeStore.currentMode(),
  );
  const event = fixture.events.find((e) => e.eventId === eventId);
  if (!event) return null;

  const cause = event.causeEventId ? fixture.events.find((e) => e.eventId === event.causeEventId) : undefined;
  const ancestors = ancestorChain(fixture.events, event);
  const descendants = descendantsOf(fixture.events, eventId);
  const systems = Array.from(new Set([event.sourceSystem, ...ancestors.map((e) => e.sourceSystem), ...descendants.map((e) => e.sourceSystem)]));

  const goToTimeline = (clickedEventId: string) => {
    const clicked = fixture.events.find((e) => e.eventId === clickedEventId);
    nav.push({ kind: "timeline", scope: clicked ? { type: "settlement", id: clicked.settlementId } : { type: "world" } });
  };

  const debug = mode === "debug";

  return (
    <div data-testid="causal-explorer">
      <h1>{debug ? `${event.eventId} · ${event.kind} · ${event.sourceSystem} · ${event.tick}` : event.summary}</h1>

      <section data-testid="why-section">
        <h2>WHY?</h2>
        {cause ? (
          <p>{debug ? `${cause.eventId} · ${cause.kind} · ${cause.sourceSystem} · ${cause.tick}` : cause.summary}</p>
        ) : (
          <p data-testid="no-known-cause">No known earlier cause.</p>
        )}
      </section>

      <section data-testid="consequences-section">
        <h2>CONSEQUENCES</h2>
        <ConsequenceTree events={fixture.events} rootId={eventId} onEventClick={goToTimeline} debug={debug} />
      </section>

      <ul data-testid="systems-involved">
        {systems.map((system) => (
          <li key={system}>{system}</li>
        ))}
      </ul>

      <button type="button" data-testid="toggle-mode" onClick={() => modeStore.toggleMode()}>
        {debug ? "Switch to Experience Mode" : "Switch to Debug Mode"}
      </button>
    </div>
  );
}
