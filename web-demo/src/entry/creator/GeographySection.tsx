import type { WorldConfig, WorldDraft } from "../repository/types";
import type { DraftAction } from "./draftState";
import { createFieldBinder } from "./fieldBinding";

const TERRAIN_STYLES: WorldConfig["terrainStyle"][] = ["Varied", "Mountainous", "Flatlands", "Archipelago", "Canyons"];

/** Doc §32 "World" group — Geography. Frontend-only config, not a claim about actual terrain
    (doc §97 — the frontend never computes geographic truth, only the chosen inputs for it). */
export function GeographySection({ draft, dispatch }: { draft: WorldDraft; dispatch: (action: DraftAction) => void }) {
  const { world } = draft;
  const field = createFieldBinder(draft, dispatch);
  const ocean = field("oceanCoverage");
  const terrain = field("terrainStyle");

  return (
    <section data-testid="geography-section">
      <button type="button" data-testid="randomize-geography" onClick={() => dispatch({ type: "randomize-field", field: "oceanCoverage" })}>
        Randomize Geography
      </button>

      <label data-testid="field-ocean-coverage">
        Ocean Coverage ({world.oceanCoverage}%)
        <input
          type="range"
          min={0}
          max={100}
          value={world.oceanCoverage}
          onChange={(e) => ocean.update(Number(e.target.value))}
          disabled={ocean.locked}
        />
        <button type="button" aria-pressed={ocean.locked} onClick={ocean.toggleLock}>
          {ocean.locked ? "🔒" : "🔓"}
        </button>
      </label>

      <label data-testid="field-terrain-style">
        Terrain Style
        <select value={world.terrainStyle} onChange={(e) => terrain.update(e.target.value as WorldConfig["terrainStyle"])} disabled={terrain.locked}>
          {TERRAIN_STYLES.map((t) => (
            <option key={t} value={t}>
              {t}
            </option>
          ))}
        </select>
        <button type="button" aria-pressed={terrain.locked} onClick={terrain.toggleLock}>
          {terrain.locked ? "🔒" : "🔓"}
        </button>
      </label>
    </section>
  );
}
