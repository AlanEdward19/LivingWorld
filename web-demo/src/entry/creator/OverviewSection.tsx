import type { WorldConfig, WorldDraft } from "../repository/types";
import type { DraftAction } from "./draftState";

const SIZES: WorldConfig["size"][] = ["Small", "Medium", "Large", "Huge"];
const EXTRAORDINARY: WorldConfig["extraordinary"][] = ["None", "Rare", "Common", "Abundant"];
const PRESETS = ["Balanced", "Harsh Survival", "Golden Age", "Age of Strife"];

/** Doc §33-36 — Overview: Quick Create fields, seed edit/randomize/copy, per-field lock + randomize. */
export function OverviewSection({ draft, dispatch }: { draft: WorldDraft; dispatch: (action: DraftAction) => void }) {
  const { world, lockedFields } = draft;

  function field<K extends keyof WorldConfig>(key: K) {
    const locked = lockedFields.includes(key);
    return {
      locked,
      update: (value: WorldConfig[K]) => dispatch({ type: "update-field", field: key, value }),
      toggleLock: () => dispatch({ type: "toggle-lock", field: key }),
      randomize: () => dispatch({ type: "randomize-field", field: key }),
    };
  }

  const name = field("name");
  const seed = field("seed");
  const size = field("size");
  const era = field("era");
  const preset = field("preset");
  const history = field("historyLengthYears");
  const population = field("initialPopulation");
  const extraordinary = field("extraordinary");

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
        <input type="text" value={world.seed} onChange={(e) => seed.update(e.target.value)} disabled={seed.locked} />
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
        <select value={world.size} onChange={(e) => size.update(e.target.value as WorldConfig["size"])} disabled={size.locked}>
          {SIZES.map((s) => (
            <option key={s} value={s}>
              {s}
            </option>
          ))}
        </select>
      </label>

      <label data-testid="field-era">
        Era / Technology
        <input type="text" value={world.era} onChange={(e) => era.update(e.target.value)} disabled={era.locked} />
      </label>

      <label data-testid="field-preset">
        Simulation Preset
        <select value={world.preset} onChange={(e) => preset.update(e.target.value)} disabled={preset.locked}>
          {PRESETS.map((p) => (
            <option key={p} value={p}>
              {p}
            </option>
          ))}
        </select>
      </label>

      <label data-testid="field-history">
        History Length (years)
        <input
          type="number"
          min={0}
          value={world.historyLengthYears}
          onChange={(e) => history.update(Number(e.target.value))}
          disabled={history.locked}
        />
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

      <label data-testid="field-extraordinary">
        Extraordinary
        <select
          value={world.extraordinary}
          onChange={(e) => extraordinary.update(e.target.value as WorldConfig["extraordinary"])}
          disabled={extraordinary.locked}
        >
          {EXTRAORDINARY.map((x) => (
            <option key={x} value={x}>
              {x}
            </option>
          ))}
        </select>
      </label>

      {/* Doc §44 — frontend-only structural validation, separate from simulation prediction. */}
      {world.name.trim() === "" && <p data-testid="validation-name-required">World Name is required.</p>}
      {world.initialPopulation > 0 && world.size === "Small" && world.initialPopulation > 20_000 && (
        <p data-testid="validation-population-warning">Initial population is very high relative to the selected world size.</p>
      )}
    </section>
  );
}
