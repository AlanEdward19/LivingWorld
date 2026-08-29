import type { WorldConfig, WorldDraft } from "../repository/types";
import type { DraftAction } from "./draftState";
import { SIZE_PRESETS } from "./draftState";
import { createFieldBinder } from "./fieldBinding";

const SIZES: WorldConfig["size"][] = ["Small", "Medium", "Large", "Huge"];
const PERIODS: WorldConfig["period"][] = ["Medieval", "Modern", "Futuristic", "Prehistoric", "Creatures"];

/** Doc §33-36 — Overview: Quick Create fields, seed edit/randomize/copy, per-field lock + randomize.
    Fields mirror the real backend's world-creation input (see `WorldConfig` in `repository/types.ts`
    for the source mapping) — no invented knobs here. */
export function OverviewSection({ draft, dispatch }: { draft: WorldDraft; dispatch: (action: DraftAction) => void }) {
  const { world } = draft;
  const field = createFieldBinder(draft, dispatch);

  const name = field("name");
  const seed = field("seed");
  const size = field("size");
  const period = field("period");
  const population = field("initialPopulation");
  const extraordinaryEnabled = field("extraordinaryEnabled");
  const extraordinaryPrevalence = field("extraordinaryPrevalence");

  function onSizeChange(next: WorldConfig["size"]) {
    // One undo step, not three — size is a UI preset for the real Width/Height/RegionSize fields.
    dispatch({ type: "update-fields", values: { size: next, ...SIZE_PRESETS[next] } });
  }

  return (
    <section data-testid="overview-section">
      <button type="button" data-testid="randomize-world" onClick={() => dispatch({ type: "randomize-all" })}>
        Randomize World
      </button>

      <label data-testid="field-name">
        World Name
        <input type="text" value={world.name} onChange={(e) => name.update(e.target.value)} disabled={name.locked} />
        <button type="button" aria-pressed={name.locked} onClick={name.toggleLock}>
          {name.locked ? "🔒" : "🔓"}
        </button>
      </label>

      <label data-testid="field-seed">
        Seed
        <input
          type="text"
          inputMode="numeric"
          value={world.seed}
          onChange={(e) => seed.update(e.target.value.replace(/[^0-9]/g, ""))}
          disabled={seed.locked}
        />
        <button type="button" onClick={seed.randomize} disabled={seed.locked}>
          Randomize
        </button>
        <button type="button" onClick={() => navigator.clipboard?.writeText(world.seed)}>
          Copy
        </button>
        <button type="button" aria-pressed={seed.locked} onClick={seed.toggleLock}>
          {seed.locked ? "🔒" : "🔓"}
        </button>
      </label>

      <label data-testid="field-size">
        World Size
        <select value={world.size} onChange={(e) => onSizeChange(e.target.value as WorldConfig["size"])} disabled={size.locked}>
          {SIZES.map((s) => (
            <option key={s} value={s}>
              {s} ({SIZE_PRESETS[s].width}×{SIZE_PRESETS[s].height})
            </option>
          ))}
        </select>
      </label>

      <label data-testid="field-period">
        Period
        <select value={world.period} onChange={(e) => period.update(e.target.value as WorldConfig["period"])} disabled={period.locked}>
          {PERIODS.map((p) => (
            <option key={p} value={p}>
              {p}
            </option>
          ))}
        </select>
        <button type="button" aria-pressed={period.locked} onClick={period.toggleLock}>
          {period.locked ? "🔒" : "🔓"}
        </button>
      </label>

      <label data-testid="field-population">
        Initial Population
        <input
          type="number"
          min={0}
          value={world.initialPopulation}
          onChange={(e) => population.update(Number(e.target.value))}
          disabled={population.locked}
        />
        <button type="button" aria-pressed={population.locked} onClick={population.toggleLock}>
          {population.locked ? "🔒" : "🔓"}
        </button>
      </label>

      <label data-testid="field-extraordinary-enabled">
        Extraordinary
        <input
          type="checkbox"
          checked={world.extraordinaryEnabled}
          onChange={(e) => extraordinaryEnabled.update(e.target.checked)}
          disabled={extraordinaryEnabled.locked}
        />
      </label>

      {world.extraordinaryEnabled && (
        <label data-testid="field-extraordinary-prevalence">
          Prevalence ({world.extraordinaryPrevalence}%)
          <input
            type="range"
            min={0}
            max={100}
            value={world.extraordinaryPrevalence}
            onChange={(e) => extraordinaryPrevalence.update(Number(e.target.value))}
            disabled={extraordinaryPrevalence.locked}
          />
        </label>
      )}

      {/* Doc §44 — frontend-only structural validation, separate from simulation prediction. */}
      {world.name.trim() === "" && <p data-testid="validation-name-required">World Name is required.</p>}
      {world.initialPopulation > 0 && world.size === "Small" && world.initialPopulation > 20_000 && (
        <p data-testid="validation-population-warning">Initial population is very high relative to the selected world size.</p>
      )}
    </section>
  );
}
