import type { WorldDraft } from "../repository/types";
import type { DraftAction } from "./draftState";
import { createFieldBinder } from "./fieldBinding";

/** Doc §32 "World" group — Geography = the real map fields (`MapScenarioLoader`: Width, Height,
    RegionSize). Overview's World Size picks a preset for these; this is where Advanced edits the
    raw numbers directly. No ocean-coverage/terrain-style knobs — the real map generator has
    neither (uniform-random terrain draw, fixed 10%-per-cell water chance, not configurable). */
export function GeographySection({ draft, dispatch }: { draft: WorldDraft; dispatch: (action: DraftAction) => void }) {
  const { world } = draft;
  const field = createFieldBinder(draft, dispatch);
  const width = field("width");
  const height = field("height");
  const regionSize = field("regionSize");

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
    </section>
  );
}
