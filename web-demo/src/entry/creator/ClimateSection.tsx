import type { WorldConfig, WorldDraft } from "../repository/types";
import type { DraftAction } from "./draftState";
import { createFieldBinder } from "./fieldBinding";

const CLIMATE_ZONES: WorldConfig["climateZone"][] = ["Temperate", "Arid", "Tropical", "Polar", "Varied"];
const SEASONAL_INTENSITIES: WorldConfig["seasonalIntensity"][] = ["Mild", "Moderate", "Extreme"];
const RAINFALL_LEVELS: WorldConfig["rainfall"][] = ["Low", "Moderate", "High"];

/** Doc §32 "World" group — Climate. */
export function ClimateSection({ draft, dispatch }: { draft: WorldDraft; dispatch: (action: DraftAction) => void }) {
  const { world } = draft;
  const field = createFieldBinder(draft, dispatch);
  const zone = field("climateZone");
  const seasonal = field("seasonalIntensity");
  const rainfall = field("rainfall");

  return (
    <section data-testid="climate-section">
      <button type="button" data-testid="randomize-climate" onClick={() => dispatch({ type: "randomize-field", field: "climateZone" })}>
        Randomize Climate
      </button>

      <label data-testid="field-climate-zone">
        Climate Zone
        <select value={world.climateZone} onChange={(e) => zone.update(e.target.value as WorldConfig["climateZone"])} disabled={zone.locked}>
          {CLIMATE_ZONES.map((z) => (
            <option key={z} value={z}>
              {z}
            </option>
          ))}
        </select>
        <button type="button" aria-pressed={zone.locked} onClick={zone.toggleLock}>
          {zone.locked ? "🔒" : "🔓"}
        </button>
      </label>

      <label data-testid="field-seasonal-intensity">
        Seasonal Intensity
        <select
          value={world.seasonalIntensity}
          onChange={(e) => seasonal.update(e.target.value as WorldConfig["seasonalIntensity"])}
          disabled={seasonal.locked}
        >
          {SEASONAL_INTENSITIES.map((s) => (
            <option key={s} value={s}>
              {s}
            </option>
          ))}
        </select>
      </label>

      <label data-testid="field-rainfall">
        Rainfall
        <select value={world.rainfall} onChange={(e) => rainfall.update(e.target.value as WorldConfig["rainfall"])} disabled={rainfall.locked}>
          {RAINFALL_LEVELS.map((r) => (
            <option key={r} value={r}>
              {r}
            </option>
          ))}
        </select>
      </label>
    </section>
  );
}
