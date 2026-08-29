import type { WorldDraft } from "../repository/types";

/** Doc §42-43 — summarizes the chosen config; never claims a simulation result that doesn't exist.
    Fields match what the real backend actually takes to create a world (see `WorldConfig`). */
export function ReviewSection({ draft }: { draft: WorldDraft }) {
  const { world } = draft;
  const nameMissing = world.name.trim() === "";

  return (
    <section data-testid="review-section">
      <h2>World</h2>
      <dl>
        <div>
          <dt>Period</dt>
          <dd>{world.period}</dd>
        </div>
        <div>
          <dt>Map size</dt>
          <dd>
            {world.width} × {world.height} ({world.size})
          </dd>
        </div>
        <div>
          <dt>Region size</dt>
          <dd>{world.regionSize}</dd>
        </div>
        <div>
          <dt>Seed</dt>
          <dd>{world.seed}</dd>
        </div>
      </dl>

      <h2>Population</h2>
      <dl>
        <div>
          <dt>Initial population</dt>
          <dd>{world.initialPopulation.toLocaleString()}</dd>
        </div>
      </dl>

      <h2>Extraordinary</h2>
      <dl>
        <div>
          <dt>Enabled</dt>
          <dd>{world.extraordinaryEnabled ? "Yes" : "No"}</dd>
        </div>
        {world.extraordinaryEnabled && (
          <div>
            <dt>Prevalence</dt>
            <dd>{world.extraordinaryPrevalence}%</dd>
          </div>
        )}
      </dl>

      <p data-testid="review-disclaimer">
        This is the configuration you chose, not a simulation result — actual outcomes are decided by the world engine during
        generation.
      </p>

      {nameMissing && <p data-testid="review-blocked">Add a World Name before generating.</p>}
    </section>
  );
}
