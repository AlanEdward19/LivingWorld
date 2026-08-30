import type { WorldDraft } from "../repository/types";
import type { DraftAction } from "./draftState";
import { createFieldBinder } from "./fieldBinding";

/** Doc §32 "World" group — Geography = the real map fields (`MapScenarioLoader`): Width, Height,
    RegionSize, and `CostWeights` (Base + AltitudeWeight, required; per-terrain-id TerrainWeight,
    optional). Overview's World Size picks a preset for Width/Height/RegionSize; this is where
    Advanced edits the raw numbers, plus the travel-cost weights that only live here. No
    ocean-coverage/terrain-style knobs — the real map generator has neither (uniform-random
    terrain draw, fixed 10%-per-cell water chance, not configurable). */
export function GeographySection({ draft, dispatch }: { draft: WorldDraft; dispatch: (action: DraftAction) => void }) {
  const { world } = draft;
  const field = createFieldBinder(draft, dispatch);
  const width = field("width");
  const height = field("height");
  const regionSize = field("regionSize");
  const costBase = field("costBase");
  const costAltitudeWeight = field("costAltitudeWeight");
  const terrainWeight1 = field("terrainWeight1");
  const terrainWeight2 = field("terrainWeight2");
  const terrainWeight3 = field("terrainWeight3");

  return (
    <section data-testid="geography-section">
      <p className="inspector-empty-note">
        Raw map dimensions — World Size on Overview fills these in from a preset; edit directly here for a custom map.
      </p>

      <label data-testid="field-width">
        Width (cells)
        <input type="number" min={1} value={world.width} onChange={(e) => width.update(Number(e.target.value))} disabled={width.locked} />
        <button type="button" aria-pressed={width.locked} onClick={width.toggleLock}>
          {width.locked ? "🔒" : "🔓"}
        </button>
      </label>

      <label data-testid="field-height">
        Height (cells)
        <input type="number" min={1} value={world.height} onChange={(e) => height.update(Number(e.target.value))} disabled={height.locked} />
        <button type="button" aria-pressed={height.locked} onClick={height.toggleLock}>
          {height.locked ? "🔒" : "🔓"}
        </button>
      </label>

      <label data-testid="field-region-size">
        Region Size
        <input
          type="number"
          min={1}
          value={world.regionSize}
          onChange={(e) => regionSize.update(Number(e.target.value))}
          disabled={regionSize.locked}
        />
        <button type="button" aria-pressed={regionSize.locked} onClick={regionSize.toggleLock}>
          {regionSize.locked ? "🔒" : "🔓"}
        </button>
      </label>

      <h3>Travel Cost</h3>
      <p className="inspector-empty-note">
        How expensive it is for NPCs to path across the map — required by the engine (`CostWeights`), not cosmetic.
      </p>

      <label data-testid="field-cost-base">
        Base cost (per step)
        <input
          type="number"
          min={0}
          step={0.1}
          value={world.costBase}
          onChange={(e) => costBase.update(Number(e.target.value))}
          disabled={costBase.locked}
        />
        <button type="button" aria-pressed={costBase.locked} onClick={costBase.toggleLock}>
          {costBase.locked ? "🔒" : "🔓"}
        </button>
      </label>

      <label data-testid="field-cost-altitude-weight">
        Altitude weight
        <input
          type="number"
          min={0}
          step={0.1}
          value={world.costAltitudeWeight}
          onChange={(e) => costAltitudeWeight.update(Number(e.target.value))}
          disabled={costAltitudeWeight.locked}
        />
        <button type="button" aria-pressed={costAltitudeWeight.locked} onClick={costAltitudeWeight.toggleLock}>
          {costAltitudeWeight.locked ? "🔒" : "🔓"}
        </button>
      </label>

      <label data-testid="field-terrain-weight-1">
        Terrain 1 weight
        <input
          type="number"
          min={0}
          step={0.1}
          value={world.terrainWeight1}
          onChange={(e) => terrainWeight1.update(Number(e.target.value))}
          disabled={terrainWeight1.locked}
        />
      </label>

      <label data-testid="field-terrain-weight-2">
        Terrain 2 weight
        <input
          type="number"
          min={0}
          step={0.1}
          value={world.terrainWeight2}
          onChange={(e) => terrainWeight2.update(Number(e.target.value))}
          disabled={terrainWeight2.locked}
        />
      </label>

      <label data-testid="field-terrain-weight-3">
        Terrain 3 weight
        <input
          type="number"
          min={0}
          step={0.1}
          value={world.terrainWeight3}
          onChange={(e) => terrainWeight3.update(Number(e.target.value))}
          disabled={terrainWeight3.locked}
        />
      </label>
    </section>
  );
}
