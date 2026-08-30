import type { WorldDraft } from "../repository/types";
import type { DraftAction } from "./draftState";
import { createFieldBinder } from "./fieldBinding";

/** Doc §32 "Life" group — Population = the real fields `PopulationScenarioLoader.cs` requires
    beyond Initial Population (already on Overview): Culture, Village X/Y, the fertility/mortality
    rules, and the per-NPC memory budget. `LifeTableBrackets[]` (the age-bracketed mortality
    curve) is ALSO real and required, but not editable here yet — an array editor for it is real
    scope, not invented, just deliberately deferred; it uses the engine's default curve either
    way (identical across every shipped period). */
export function PopulationSection({ draft, dispatch }: { draft: WorldDraft; dispatch: (action: DraftAction) => void }) {
  const { world } = draft;
  const field = createFieldBinder(draft, dispatch);
  const culture = field("culture");
  const villageX = field("villageX");
  const villageY = field("villageY");
  const maxLongevityYears = field("maxLongevityYears");
  const fertilityMinAge = field("fertilityMinAge");
  const fertilityMaxAge = field("fertilityMaxAge");
  const annualConceptionChance = field("annualConceptionChance");
  const gestationDays = field("gestationDays");
  const maxBytesPerNpcPerYear = field("maxBytesPerNpcPerYear");
  const maxAliveNpcsEnabled = field("maxAliveNpcsEnabled");
  const maxAliveNpcs = field("maxAliveNpcs");

  const villageOutOfBounds = world.villageX >= world.width || world.villageY >= world.height;
  const fertilityRangeInvalid = world.fertilityMinAge > world.fertilityMaxAge;

  return (
    <section data-testid="population-section">
      <label data-testid="field-culture">
        Culture
        <input type="number" min={1} value={world.culture} onChange={(e) => culture.update(Number(e.target.value))} disabled={culture.locked} />
      </label>

      <label data-testid="field-village-x">
        Village X
        <input
          type="number"
          min={0}
          max={Math.max(0, world.width - 1)}
          value={world.villageX}
          onChange={(e) => villageX.update(Number(e.target.value))}
          disabled={villageX.locked}
        />
      </label>

      <label data-testid="field-village-y">
        Village Y
        <input
          type="number"
          min={0}
          max={Math.max(0, world.height - 1)}
          value={world.villageY}
          onChange={(e) => villageY.update(Number(e.target.value))}
          disabled={villageY.locked}
        />
      </label>

      {villageOutOfBounds && (
        <p data-testid="validation-village-out-of-bounds">
          Village X/Y must be within the map ({world.width} × {world.height}).
        </p>
      )}

      <h3>Life &amp; Fertility</h3>

      <label data-testid="field-max-longevity">
        Max longevity (years)
        <input
          type="number"
          min={1}
          value={world.maxLongevityYears}
          onChange={(e) => maxLongevityYears.update(Number(e.target.value))}
          disabled={maxLongevityYears.locked}
        />
        <button type="button" aria-pressed={maxLongevityYears.locked} onClick={maxLongevityYears.toggleLock}>
          {maxLongevityYears.locked ? "🔒" : "🔓"}
        </button>
      </label>

      <label data-testid="field-fertility-min-age">
        Fertility min age
        <input
          type="number"
          min={0}
          value={world.fertilityMinAge}
          onChange={(e) => fertilityMinAge.update(Number(e.target.value))}
          disabled={fertilityMinAge.locked}
        />
      </label>

      <label data-testid="field-fertility-max-age">
        Fertility max age
        <input
          type="number"
          min={0}
          value={world.fertilityMaxAge}
          onChange={(e) => fertilityMaxAge.update(Number(e.target.value))}
          disabled={fertilityMaxAge.locked}
        />
      </label>

      {fertilityRangeInvalid && (
        <p data-testid="validation-fertility-range">Fertility min age must not be greater than max age.</p>
      )}

      <label data-testid="field-conception-chance">
        Annual conception chance ({Math.round(world.annualConceptionChance * 100)}%)
        <input
          type="range"
          min={0}
          max={100}
          value={Math.round(world.annualConceptionChance * 100)}
          onChange={(e) => annualConceptionChance.update(Number(e.target.value) / 100)}
          disabled={annualConceptionChance.locked}
        />
      </label>

      <label data-testid="field-gestation-days">
        Gestation (days)
        <input
          type="number"
          min={1}
          value={world.gestationDays}
          onChange={(e) => gestationDays.update(Number(e.target.value))}
          disabled={gestationDays.locked}
        />
      </label>

      <h3>Engine Limits</h3>

      <label data-testid="field-max-bytes-per-npc">
        Max memory per NPC/year (bytes)
        <input
          type="number"
          min={1}
          value={world.maxBytesPerNpcPerYear}
          onChange={(e) => maxBytesPerNpcPerYear.update(Number(e.target.value))}
          disabled={maxBytesPerNpcPerYear.locked}
        />
      </label>

      <label data-testid="field-max-alive-npcs-enabled">
        Cap max alive NPCs
        <input
          type="checkbox"
          checked={world.maxAliveNpcsEnabled}
          onChange={(e) => maxAliveNpcsEnabled.update(e.target.checked)}
        />
      </label>

      {world.maxAliveNpcsEnabled && (
        <label data-testid="field-max-alive-npcs">
          Max alive NPCs
          <input
            type="number"
            min={1}
            value={world.maxAliveNpcs}
            onChange={(e) => maxAliveNpcs.update(Number(e.target.value))}
            disabled={maxAliveNpcs.locked}
          />
        </label>
      )}

      <p className="inspector-empty-note">
        Life table (age-bracketed mortality curve) isn't editable yet — generation uses the engine's default curve.
      </p>
    </section>
  );
}
