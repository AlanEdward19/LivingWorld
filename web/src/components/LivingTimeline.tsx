import { useSyncExternalStore } from "react";
import type { SpaceId } from "../map-engine/types";
import type { SimulationStore } from "../state/simulationStore";

export function LivingTimeline({ space, simulationStore }: { space: SpaceId; simulationStore: SimulationStore }) {
  const state = useSyncExternalStore(
    (onStoreChange) => simulationStore.subscribe(onStoreChange),
    () => simulationStore.livingStateOf(space),
  );
  const events = state.events.slice(-8).reverse();

  return (
    <aside className="living-timeline" aria-label="Linha do tempo do mundo">
      <h3>Acontecimentos</h3>
      {events.length === 0 ? <p>Nenhum acontecimento recente.</p> : (
        <ol>
          {events.map((event, index) => (
            <li key={`${event.tick}:${event.kind}:${index}`}>
              <time>Tick {event.tick}</time>
              <span>{event.label}</span>
            </li>
          ))}
        </ol>
      )}
    </aside>
  );
}
