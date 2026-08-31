import type { WorldDraft } from "../repository/types";
import type { DraftAction } from "./draftState";
import { createFieldBinder } from "./fieldBinding";
import { FieldRow } from "./FieldRow";

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

      <FieldRow testId="field-width" label="Width (cells)" hint="How wide the map is, in tiles. Bigger = more room, slower to generate. Typical: 64–512." locked={width.locked} onToggleLock={width.toggleLock}>
        <input type="number" min={1} value={world.width} onChange={(e) => width.update(Number(e.target.value))} disabled={width.locked} />
      </FieldRow>

      <FieldRow
        testId="field-height"
        label="Height (cells)"
        hint="How tall the map is, in tiles. Typical: 64–512."
        locked={height.locked}
        onToggleLock={height.toggleLock}
      >
        <input type="number" min={1} value={world.height} onChange={(e) => height.update(Number(e.target.value))} disabled={height.locked} />
      </FieldRow>

      <FieldRow
        testId="field-region-size"
        label="Region Size"
        hint="Groups tiles into regions of this many cells per side, for the engine's internal bookkeeping — doesn't change how the map looks. Typical: 8–32, roughly Width/4."
        locked={regionSize.locked}
        onToggleLock={regionSize.toggleLock}
      >
        <input
          type="number"
          min={1}
          value={world.regionSize}
          onChange={(e) => regionSize.update(Number(e.target.value))}
          disabled={regionSize.locked}
        />
      </FieldRow>

      <h3>Travel Cost</h3>
      <p className="inspector-empty-note">
        How expensive it is for NPCs to path across the map — the engine requires these to generate a world at all, they're
        not cosmetic. Leave at the defaults unless you specifically want travel to feel faster or slower.
      </p>

      <FieldRow
        testId="field-cost-base"
        label="Base cost (per step)"
        hint="What it costs an NPC to move one tile, before any terrain/altitude penalty. Higher = everyone travels slower everywhere. Default 1, typical range 0.5–3."
        locked={costBase.locked}
        onToggleLock={costBase.toggleLock}
      >
        <input
          type="number"
          min={0}
          step={0.1}
          value={world.costBase}
          onChange={(e) => costBase.update(Number(e.target.value))}
          disabled={costBase.locked}
        />
      </FieldRow>

      <FieldRow
        testId="field-cost-altitude-weight"
        label="Altitude weight"
        hint="Extra cost added per unit of altitude an NPC climbs. Higher = steep terrain slows travel down more. 0 = altitude doesn't matter. Default 0.5, typical range 0–2."
        locked={costAltitudeWeight.locked}
        onToggleLock={costAltitudeWeight.toggleLock}
      >
        <input
          type="number"
          min={0}
          step={0.1}
          value={world.costAltitudeWeight}
          onChange={(e) => costAltitudeWeight.update(Number(e.target.value))}
          disabled={costAltitudeWeight.locked}
        />
      </FieldRow>

      <p className="inspector-empty-note">
        The engine sorts every map tile into 3 anonymous terrain types (it doesn't name them — no "grass" or "mountain",
        just "type A/B/C") and assigns each tile one at random. These 3 weights say how much slower each type is to cross,
        as a multiplier on the base cost above: 1.0 = normal speed, 2.0 = twice as slow, 0.5 = twice as fast.
      </p>

      <FieldRow
        testId="field-terrain-weight-1"
        label="Terrain type A weight"
        hint="Default 1.0 — the shipped scenarios treat this as the easiest terrain to cross."
        locked={terrainWeight1.locked}
        onToggleLock={terrainWeight1.toggleLock}
      >
        <input
          type="number"
          min={0}
          step={0.1}
          value={world.terrainWeight1}
          onChange={(e) => terrainWeight1.update(Number(e.target.value))}
          disabled={terrainWeight1.locked}
        />
      </FieldRow>

      <FieldRow
        testId="field-terrain-weight-2"
        label="Terrain type B weight"
        hint="Default 1.5 — moderately slower to cross than type A."
        locked={terrainWeight2.locked}
        onToggleLock={terrainWeight2.toggleLock}
      >
        <input
          type="number"
          min={0}
          step={0.1}
          value={world.terrainWeight2}
          onChange={(e) => terrainWeight2.update(Number(e.target.value))}
          disabled={terrainWeight2.locked}
        />
      </FieldRow>

      <FieldRow
        testId="field-terrain-weight-3"
        label="Terrain type C weight"
        hint="Default 3.0 — the shipped scenarios treat this as the hardest terrain to cross."
        locked={terrainWeight3.locked}
        onToggleLock={terrainWeight3.toggleLock}
      >
        <input
          type="number"
          min={0}
          step={0.1}
          value={world.terrainWeight3}
          onChange={(e) => terrainWeight3.update(Number(e.target.value))}
          disabled={terrainWeight3.locked}
        />
      </FieldRow>
    </section>
  );
}
