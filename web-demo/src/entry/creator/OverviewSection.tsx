import type { WorldConfig, WorldDraft } from "../repository/types";
import type { DraftAction } from "./draftState";
import { SIZE_PRESETS } from "./draftState";
import { createFieldBinder } from "./fieldBinding";
import { FieldRow } from "./FieldRow";

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
    // Re-centers Village X/Y too: leaving them at the old size's center could place the real
    // (required) VillageX/VillageY outside the new, possibly-smaller map bounds.
    const preset = SIZE_PRESETS[next];
    dispatch({
      type: "update-fields",
      values: { size: next, ...preset, villageX: Math.floor(preset.width / 2), villageY: Math.floor(preset.height / 2) },
    });
  }

  return (
    <section data-testid="overview-section">
      <button type="button" data-testid="randomize-world" onClick={() => dispatch({ type: "randomize-all" })}>
        Randomize World
      </button>

      <FieldRow testId="field-name" label="World Name" hint="Just a label for your world — shown in the library, doesn't affect generation." locked={name.locked} onToggleLock={name.toggleLock}>
        <input type="text" value={world.name} onChange={(e) => name.update(e.target.value)} disabled={name.locked} />
      </FieldRow>

      <FieldRow
        testId="field-seed"
        label="Seed"
        hint="The number that determines everything random about this world — terrain, water, who's born where. Same seed + same settings always produces the exact same world. Leave it random unless you want a specific one to share or recreate."
        locked={seed.locked}
        onToggleLock={seed.toggleLock}
      >
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
      </FieldRow>

      <FieldRow
        testId="field-size"
        label="World Size"
        hint="How big the map is. Bigger worlds support more settlements and population but take longer to generate. You can fine-tune the exact dimensions in Geography."
      >
        <select value={world.size} onChange={(e) => onSizeChange(e.target.value as WorldConfig["size"])} disabled={size.locked}>
          {SIZES.map((s) => (
            <option key={s} value={s}>
              {s} ({SIZE_PRESETS[s].width}×{SIZE_PRESETS[s].height})
            </option>
          ))}
        </select>
      </FieldRow>

      <FieldRow
        testId="field-period"
        label="Period"
        hint="Which era's content the world uses — its buildings, professions, and starting technology. Doesn't affect map size or population count."
        locked={period.locked}
        onToggleLock={period.toggleLock}
      >
        <select value={world.period} onChange={(e) => period.update(e.target.value as WorldConfig["period"])} disabled={period.locked}>
          {PERIODS.map((p) => (
            <option key={p} value={p}>
              {p}
            </option>
          ))}
        </select>
      </FieldRow>

      <FieldRow
        testId="field-population"
        label="Initial Population"
        hint="How many NPCs the world starts with. Larger populations are more lively but heavier to simulate — for a Small map, keep this under ~20,000 (see the warning below if you go over)."
        locked={population.locked}
        onToggleLock={population.toggleLock}
      >
        <input
          type="number"
          min={0}
          value={world.initialPopulation}
          onChange={(e) => population.update(Number(e.target.value))}
          disabled={population.locked}
        />
      </FieldRow>

      <FieldRow
        testId="field-extraordinary-enabled"
        label="Extraordinary"
        hint="Whether any NPCs can be born with extraordinary abilities (magic, powers — depends on the period). Off means a fully mundane world."
      >
        <input
          type="checkbox"
          checked={world.extraordinaryEnabled}
          onChange={(e) => extraordinaryEnabled.update(e.target.checked)}
          disabled={extraordinaryEnabled.locked}
        />
      </FieldRow>

      {world.extraordinaryEnabled && (
        <FieldRow
          testId="field-extraordinary-prevalence"
          label={`Prevalence (${world.extraordinaryPrevalence}%)`}
          hint="How common the extraordinary is among the population — low keeps it rare and special, high makes it a normal part of society."
          locked={extraordinaryPrevalence.locked}
          onToggleLock={extraordinaryPrevalence.toggleLock}
        >
          <input
            type="range"
            min={0}
            max={100}
            value={world.extraordinaryPrevalence}
            onChange={(e) => extraordinaryPrevalence.update(Number(e.target.value))}
            disabled={extraordinaryPrevalence.locked}
          />
        </FieldRow>
      )}

      {/* Doc §44 — frontend-only structural validation, separate from simulation prediction. */}
      {world.name.trim() === "" && <p data-testid="validation-name-required">World Name is required.</p>}
      {world.initialPopulation > 0 && world.size === "Small" && world.initialPopulation > 20_000 && (
        <p data-testid="validation-population-warning">Initial population is very high relative to the selected world size.</p>
      )}
    </section>
  );
}
