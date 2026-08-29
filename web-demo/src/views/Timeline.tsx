import { useState } from "react";
import type { WorldEventFixture, WorldFixture } from "../fixture/types";
import { Popup } from "../components/ContextOverlay";

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

// Redesign (pedido do usuário 2026-08-27: "estilo branch do git, porém em horizontal, bem
// dinâmico e estiloso") — trunk horizontal, agrupado por Year·Season, em vez da lista vertical
// anterior. Posições vêm do ÍNDICE do evento na lista já ordenada cronologicamente
// (`fixture.events` — doc: "ordem cronológica"), nunca de um timestamp real (o fixture não tem
// um, só o rótulo `tick` legível). Curvas de causalidade (SVG) foram removidas a pedido do
// usuário: viravam uma "teia" ilegível e ficaram redundantes assim que o hover passou a
// destacar a cadeia de causas diretamente nos nós (ver `ancestorIds` abaixo).
const NODE_SPACING = 132;
const GRAPH_PADDING = 56;
const GRAPH_HEIGHT = 220;
const TRUNK_Y = 118;

function groupKey(tick: string): string {
  const [year, season] = tick.split(" · ");
  return [year, season].filter(Boolean).join(" · ");
}

/** Última parte do `tick` ("Year 312 · Spring · 09" → "Day 09") — doc §21: "não repetir a data
 * inteira em cada linha", o Year/Season já aparece uma vez só no divisor do grupo. */
function dayLabel(tick: string): string {
  const parts = tick.split(" · ");
  return parts.length > 2 ? `Day ${parts[2]}` : tick;
}

/** Cadeia de causas (ids), da mais próxima à mais antiga — anda `causeEventId` pra trás no
 * fixture INTEIRO (não só nos eventos filtrados), igual `ancestorChain` do CausalExplorer, pois
 * uma causa real pode ter ficado de fora do filtro de tipo atual mesmo assim "levou a" o evento. */
function ancestorIds(events: WorldEventFixture[], eventId: string): string[] {
  const ids: string[] = [];
  let current = events.find((e) => e.eventId === eventId);
  while (current?.causeEventId) {
    const cause: WorldEventFixture | undefined = events.find((e) => e.eventId === current!.causeEventId);
    if (!cause) break;
    ids.push(cause.eventId);
    current = cause;
  }
  return ids;
}

/**
 * Eventos do fixture em ordem cronológica, filtráveis por escopo (World/Settlement/Household/
 * Agent — vindo da `Route`) e por tipo de evento (doc#121, filtro local nesta view).
 */
export function Timeline({ fixture, scope }: TimelineProps) {
  const [kindFilter, setKindFilter] = useState<string>("all");
  const [hoveredEventId, setHoveredEventId] = useState<string | null>(null);
  const [selectedEventId, setSelectedEventId] = useState<string | null>(null);
  const [popupAnchor, setPopupAnchor] = useState<DOMRect | null>(null);

  const scoped = fixture.events.filter((event) => matchesScope(event, scope));
  const kinds = Array.from(new Set(scoped.map((event) => event.kind)));
  const filtered = kindFilter === "all" ? scoped : scoped.filter((event) => event.kind === kindFilter);

  const totalWidth = GRAPH_PADDING * 2 + Math.max(0, filtered.length - 1) * NODE_SPACING;

  const groups: { key: string; startIndex: number; endIndex: number }[] = [];
  filtered.forEach((event, index) => {
    const key = groupKey(event.tick);
    const current = groups[groups.length - 1];
    if (current && current.key === key) current.endIndex = index;
    else groups.push({ key, startIndex: index, endIndex: index });
  });

  const highlightedIds = new Set(hoveredEventId ? ancestorIds(fixture.events, hoveredEventId) : []);
  const selectedEvent = selectedEventId ? fixture.events.find((e) => e.eventId === selectedEventId) : undefined;
  const selectedChain = selectedEventId
    ? [...ancestorIds(fixture.events, selectedEventId)].reverse().map((id) => fixture.events.find((e) => e.eventId === id)!)
    : [];

  return (
    <div data-testid="timeline-view" className="timeline-view">
      <div className="timeline-filter">
        <label className="timeline-filter-label" htmlFor="timeline-kind-filter">
          Type
        </label>
        <select
          id="timeline-kind-filter"
          data-testid="timeline-kind-filter"
          className="timeline-filter-select"
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
      </div>

      {filtered.length === 0 ? (
        <p className="timeline-empty">No events yet.</p>
      ) : (
        <div className="timeline-graph" data-testid="timeline-graph">
          <svg className="timeline-graph-lines" width={totalWidth} height={GRAPH_HEIGHT} aria-hidden="true">
            <line x1={GRAPH_PADDING} y1={TRUNK_Y} x2={GRAPH_PADDING + Math.max(0, filtered.length - 1) * NODE_SPACING} y2={TRUNK_Y} className="timeline-trunk" />
            {groups.slice(1).map((group) => {
              const dividerX = GRAPH_PADDING + group.startIndex * NODE_SPACING - NODE_SPACING / 2;
              return <line key={group.key + group.startIndex} x1={dividerX} y1={16} x2={dividerX} y2={GRAPH_HEIGHT - 16} className="timeline-group-divider" />;
            })}
            {groups.map((group) => (
              <text key={group.key} x={GRAPH_PADDING + ((group.startIndex + group.endIndex) / 2) * NODE_SPACING} y={16} textAnchor="middle" className="timeline-group-label">
                {group.key}
              </text>
            ))}
          </svg>

          <ul data-testid="timeline-events" className="timeline-nodes" style={{ width: totalWidth, height: GRAPH_HEIGHT }}>
            {filtered.map((event, index) => {
              const x = GRAPH_PADDING + index * NODE_SPACING;
              const above = index % 2 === 0;
              return (
                <li
                  key={event.eventId}
                  data-severity={event.severity}
                  data-testid="timeline-node"
                  className={`timeline-node ${above ? "timeline-node--above" : "timeline-node--below"} ${
                    highlightedIds.has(event.eventId) ? "timeline-node--highlighted" : ""
                  }`}
                  style={above ? { left: x, bottom: GRAPH_HEIGHT - TRUNK_Y } : { left: x, top: TRUNK_Y }}
                  title={`${dayLabel(event.tick)} — ${event.summary}`}
                  onMouseEnter={() => setHoveredEventId(event.eventId)}
                  onMouseLeave={() => setHoveredEventId(null)}
                  onClick={(e) => {
                    setSelectedEventId(event.eventId);
                    setPopupAnchor(e.currentTarget.getBoundingClientRect());
                  }}
                >
                  <span className="timeline-node-dot" />
                  <span className="timeline-node-label">
                    {event.severity === "critical" && <span data-testid="timeline-critical-marker">●</span>} <strong>{dayLabel(event.tick)}</strong> — {event.summary}
                  </span>
                </li>
              );
            })}
          </ul>
        </div>
      )}

      {selectedEvent && (
        <Popup title="Event timeline" onClose={() => setSelectedEventId(null)} anchorRect={popupAnchor}>
          <ol data-testid="timeline-event-chain" className="timeline-event-chain">
            {selectedChain.length === 0 && <li className="timeline-event-chain-empty" data-testid="timeline-no-known-cause">No known earlier cause.</li>}
            {selectedChain.map((event) => (
              <li key={event.eventId}>
                <strong>{dayLabel(event.tick)}</strong> — {event.summary}
              </li>
            ))}
            <li className="timeline-event-chain-current">
              <strong>{dayLabel(selectedEvent.tick)}</strong> — {selectedEvent.summary}
            </li>
          </ol>
        </Popup>
      )}
    </div>
  );
}
