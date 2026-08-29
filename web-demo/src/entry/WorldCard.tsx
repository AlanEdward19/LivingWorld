import type { WorldSummary } from "./repository/types";

export function WorldCard({ world, selected, onSelect, onContinue }: {
  world: WorldSummary;
  selected: boolean;
  onSelect: () => void;
  onContinue: () => void;
}) {
  return (
    <li data-testid="world-card" data-selected={selected} className="entity-row">
      <button type="button" className="entity-row-text" data-testid="world-card-select" onClick={onSelect} style={{ background: "transparent", border: "none", padding: 0, textAlign: "left" }}>
        <div className="entity-row-title">{world.name}</div>
        <div className="entity-row-meta">
          Year {world.year} · {world.season} · Population {world.population.toLocaleString()}
          {world.status === "paused" && " · Paused"}
        </div>
      </button>
      <button type="button" data-testid="world-card-continue" onClick={onContinue}>
        Continue
      </button>
    </li>
  );
}
