import type { WorldConfig, WorldDraft } from "../repository/types";
import type { DraftAction } from "./draftState";
import { createFieldBinder } from "./fieldBinding";

const MINERAL_LEVELS: WorldConfig["mineralAbundance"][] = ["Scarce", "Balanced", "Abundant"];
const FERTILITY_LEVELS: WorldConfig["fertility"][] = ["Poor", "Moderate", "Rich"];

/** Doc §32 "World" group — Resources. */
export function ResourcesSection({ draft, dispatch }: { draft: WorldDraft; dispatch: (action: DraftAction) => void }) {
  const { world } = draft;
  const field = createFieldBinder(draft, dispatch);
  const minerals = field("mineralAbundance");
  const fertility = field("fertility");

  return (
    <section data-testid="resources-section">
      <button type="button" data-testid="randomize-resources" onClick={() => dispatch({ type: "randomize-field", field: "mineralAbundance" })}>
        Randomize Resources
      </button>

      <label data-testid="field-mineral-abundance">
        Mineral Abundance
        <select
          value={world.mineralAbundance}
          onChange={(e) => minerals.update(e.target.value as WorldConfig["mineralAbundance"])}
          disabled={minerals.locked}
        >
          {MINERAL_LEVELS.map((m) => (
            <option key={m} value={m}>
              {m}
            </option>
          ))}
        </select>
        <button type="button" aria-pressed={minerals.locked} onClick={minerals.toggleLock}>
          {minerals.locked ? "🔒" : "🔓"}
        </button>
      </label>

      <label data-testid="field-fertility">
        Soil Fertility
        <select value={world.fertility} onChange={(e) => fertility.update(e.target.value as WorldConfig["fertility"])} disabled={fertility.locked}>
          {FERTILITY_LEVELS.map((f) => (
            <option key={f} value={f}>
              {f}
            </option>
          ))}
        </select>
        <button type="button" aria-pressed={fertility.locked} onClick={fertility.toggleLock}>
          {fertility.locked ? "🔒" : "🔓"}
        </button>
      </label>
    </section>
  );
}
