import type { WorldDraft } from "../repository/types";

/** Doc §42-43 — summarizes the chosen config; never claims a simulation result that doesn't exist. */
export function ReviewSection({ draft }: { draft: WorldDraft }) {
  const { world } = draft;
  const nameMissing = world.name.trim() === "";

  return (
    <section data-testid="review-section">
      <h2>World</h2>
      <dl>
        <div>
          <dt>Size</dt>
          <dd>{world.size}</dd>
        </div>
        <div>
          <dt>Era</dt>
          <dd>{world.era}</dd>
        </div>
        <div>
          <dt>Preset</dt>
          <dd>{world.preset}</dd>
        </div>
        <div>
          <dt>Ocean coverage</dt>
          <dd>{world.oceanCoverage}%</dd>
        </div>
        <div>
          <dt>Terrain</dt>
          <dd>{world.terrainStyle}</dd>
        </div>
      </dl>

      <h2>Climate</h2>
      <dl>
        <div>
          <dt>Zone</dt>
          <dd>{world.climateZone}</dd>
        </div>
        <div>
          <dt>Seasonal intensity</dt>
          <dd>{world.seasonalIntensity}</dd>
        </div>
        <div>
          <dt>Rainfall</dt>
          <dd>{world.rainfall}</dd>
        </div>
      </dl>

      <h2>Resources</h2>
      <dl>
        <div>
          <dt>Minerals</dt>
          <dd>{world.mineralAbundance}</dd>
        </div>
        <div>
          <dt>Soil fertility</dt>
          <dd>{world.fertility}</dd>
        </div>
      </dl>

      <h2>Population</h2>
      <dl>
        <div>
          <dt>Initial population</dt>
          <dd>{world.initialPopulation.toLocaleString()}</dd>
        </div>
      </dl>

      <h2>History</h2>
      <dl>
        <div>
          <dt>Length</dt>
          <dd>{world.historyLengthYears} years</dd>
        </div>
      </dl>

      <h2>Extraordinary</h2>
      <dl>
        <div>
          <dt>Prevalence</dt>
          <dd>{world.extraordinary}</dd>
        </div>
      </dl>

      <p data-testid="review-disclaimer">
        This is the configuration you chose, not a simulation result — actual outcomes are decided by the world engine during
        generation.
      </p>

      {nameMissing && <p data-testid="review-blocked">Add a World Name before generating.</p>}
    </section>
  );
}
