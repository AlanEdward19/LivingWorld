import type { WorldDraft } from "../repository/types";

/** Doc §42-43 — summarizes the chosen config; never claims a simulation result that doesn't exist.
    Fields match what the real backend actually takes to create a world (see `WorldConfig`). */
export function ReviewSection({ draft }: { draft: WorldDraft }) {
  const { world } = draft;
  const nameMissing = world.name.trim() === "";
  const villageOutOfBounds = world.villageX >= world.width || world.villageY >= world.height;
  const fertilityRangeInvalid = world.fertilityMinAge > world.fertilityMaxAge;

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
        <div>
          <dt>Travel cost</dt>
          <dd>
            base {world.costBase}, altitude ×{world.costAltitudeWeight}
          </dd>
        </div>
        <div>
          <dt>Terrain weights</dt>
          <dd>
            {world.terrainWeight1} / {world.terrainWeight2} / {world.terrainWeight3}
          </dd>
        </div>
      </dl>

      <h2>Population</h2>
      <dl>
        <div>
          <dt>Initial population</dt>
          <dd>{world.initialPopulation.toLocaleString()}</dd>
        </div>
        <div>
          <dt>Culture</dt>
          <dd>{world.culture}</dd>
        </div>
        <div>
          <dt>Village at</dt>
          <dd>
            ({world.villageX}, {world.villageY})
          </dd>
        </div>
        <div>
          <dt>Fertility</dt>
          <dd>
            ages {world.fertilityMinAge}–{world.fertilityMaxAge}, {Math.round(world.annualConceptionChance * 100)}%/yr,{" "}
            {world.gestationDays}d gestation
          </dd>
        </div>
        <div>
          <dt>Max longevity</dt>
          <dd>{world.maxLongevityYears} years</dd>
        </div>
        <div>
          <dt>Max alive NPCs</dt>
          <dd>{world.maxAliveNpcsEnabled ? world.maxAliveNpcs.toLocaleString() : "Unlimited"}</dd>
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
      {villageOutOfBounds && (
        <p data-testid="review-blocked-village">Village X/Y must be within the map before generating.</p>
      )}
      {fertilityRangeInvalid && (
        <p data-testid="review-blocked-fertility">Fertility min age must not be greater than max age before generating.</p>
      )}
    </section>
  );
}
