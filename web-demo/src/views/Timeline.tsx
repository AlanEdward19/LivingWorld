import { useState } from "react";
import type { WorldEventFixture, WorldFixture } from "../fixture/types";

export interface TimelineScope {
  type: "world" | "settlement" | "household" | "agent";
  id?: string;
}

export interface TimelineProps {
  fixture: WorldFixture;
  scope: TimelineScope;
}

function matchesScope(event: WorldEventFixture, scope: TimelineScope): boolean {
  switch (scope.type) {
    case "world":
      return true;
    case "settlement":
      return event.settlementId === scope.id;
    case "household":
      return event.affectedHouseholdIds.includes(scope.id ?? "");
    case "agent":
      return event.affectedAgentIds.includes(scope.id ?? "");
  }
}

/**
 * Eventos do fixture em ordem cronológica, filtráveis por escopo (World/Settlement/Household/
 * Agent — vindo da `Route`) e por tipo de evento (doc#121, filtro local nesta view).
 */
export function Timeline({ fixture, scope }: TimelineProps) {
  const [kindFilter, setKindFilter] = useState<string>("all");

  const scoped = fixture.events.filter((event) => matchesScope(event, scope));
  const kinds = Array.from(new Set(scoped.map((event) => event.kind)));
  const filtered = kindFilter === "all" ? scoped : scoped.filter((event) => event.kind === kindFilter);

  return (
    <div data-testid="timeline-view">
      <select
        data-testid="timeline-kind-filter"
        value={kindFilter}
        onChange={(e) => setKindFilter(e.target.value)}
      >
        <option value="all">All types</option>
        {kinds.map((kind) => (
          <option key={kind} value={kind}>
            {kind}
          </option>
        ))}
      </select>

      <ul data-testid="timeline-events">
        {filtered.map((event) => (
          <li key={event.eventId} data-severity={event.severity}>
            {event.severity === "critical" && <span data-testid="timeline-critical-marker">●</span>} {event.tick} — {event.summary}
          </li>
        ))}
      </ul>
    </div>
  );
}
