import { useState } from "react";
import type { WorldFixture } from "../fixture/types";
import { Timeline } from "../views/Timeline";

export interface TimelineBarProps {
  fixture: WorldFixture;
}

/**
 * Timeline inferior (doc §105-107) — colapsada por padrão (rótulo com a data mais recente do
 * fixture), expande pra mostrar a `Timeline` (escopo mundo) inline. Reusa o componente
 * `Timeline` já existente em vez de duplicar a lógica de filtro/agrupamento.
 */
export function TimelineBar({ fixture }: TimelineBarProps) {
  const [expanded, setExpanded] = useState(false);
  const lastEvent = fixture.events[fixture.events.length - 1];

  return (
    <footer data-testid="timeline-bar">
      <button type="button" data-testid="timeline-bar-toggle" onClick={() => setExpanded((e) => !e)}>
        {expanded ? "▾" : "▴"} {lastEvent?.tick}
      </button>
      {expanded && (
        <div data-testid="timeline-bar-content">
          <Timeline fixture={fixture} scope={{ type: "world" }} />
        </div>
      )}
    </footer>
  );
}
