import type { WorldDraft } from "../repository/types";
import type { DraftAction } from "./draftState";
import { createFieldBinder } from "./fieldBinding";
import { FieldRow } from "./FieldRow";

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
      <FieldRow
        testId="field-culture"
        label="Culture"
        hint="Which starting culture NPCs belong to, by internal id. Every shipped scenario only defines one (id 1) — leave this as 1 unless you know a scenario with more."
        locked={culture.locked}
        onToggleLock={culture.toggleLock}
      >
        <input type="number" min={1} value={world.culture} onChange={(e) => culture.update(Number(e.target.value))} disabled={culture.locked} />
      </FieldRow>

      <FieldRow
        testId="field-village-x"
        label="Village X"
        hint={`Where the starting village sits on the map, left-to-right. 0 is the west edge, ${Math.max(0, world.width - 1)} is the east edge (must stay inside Geography's Width/Height — check the map preview).`}
      >
        <input
          type="number"
          min={0}
          max={Math.max(0, world.width - 1)}
          value={world.villageX}
          onChange={(e) => villageX.update(Number(e.target.value))}
          disabled={villageX.locked}
        />
      </FieldRow>

      <FieldRow
        testId="field-village-y"
        label="Village Y"
        hint={`Same idea, top-to-bottom: 0 is the north edge, ${Math.max(0, world.height - 1)} is the south edge.`}
      >
        <input
          type="number"
          min={0}
          max={Math.max(0, world.height - 1)}
          value={world.villageY}
          onChange={(e) => villageY.update(Number(e.target.value))}
          disabled={villageY.locked}
        />
      </FieldRow>

      {villageOutOfBounds && (
        <p data-testid="validation-village-out-of-bounds">
          Village X/Y must be within the map ({world.width} × {world.height}).
        </p>
      )}

      <h3>Life &amp; Fertility</h3>

      <FieldRow
        testId="field-max-longevity"
        label="Max longevity (years)"
        hint="The oldest age an NPC can reach through natural aging alone. Default 90, typical range 60–120."
        locked={maxLongevityYears.locked}
        onToggleLock={maxLongevityYears.toggleLock}
      >
        <input
          type="number"
          min={1}
          value={world.maxLongevityYears}
          onChange={(e) => maxLongevityYears.update(Number(e.target.value))}
          disabled={maxLongevityYears.locked}
        />
      </FieldRow>

      <FieldRow
        testId="field-fertility-min-age"
        label="Fertility min age"
        hint="Youngest age (years) an NPC can conceive. Must be ≤ max age below. Default 16."
      >
        <input
          type="number"
          min={0}
          value={world.fertilityMinAge}
          onChange={(e) => fertilityMinAge.update(Number(e.target.value))}
          disabled={fertilityMinAge.locked}
        />
      </FieldRow>

      <FieldRow
        testId="field-fertility-max-age"
        label="Fertility max age"
        hint="Oldest age (years) an NPC can conceive. Default 45."
      >
        <input
          type="number"
          min={0}
          value={world.fertilityMaxAge}
          onChange={(e) => fertilityMaxAge.update(Number(e.target.value))}
          disabled={fertilityMaxAge.locked}
        />
      </FieldRow>

      {fertilityRangeInvalid && (
        <p data-testid="validation-fertility-range">Fertility min age must not be greater than max age.</p>
      )}

      <FieldRow
        testId="field-conception-chance"
        label={`Annual conception chance (${Math.round(world.annualConceptionChance * 100)}%)`}
        hint="For an NPC in the fertile age range with a partner, the odds they conceive in a given year. Default 25%."
        locked={annualConceptionChance.locked}
        onToggleLock={annualConceptionChance.toggleLock}
      >
        <input
          type="range"
          min={0}
          max={100}
          value={Math.round(world.annualConceptionChance * 100)}
          onChange={(e) => annualConceptionChance.update(Number(e.target.value) / 100)}
          disabled={annualConceptionChance.locked}
        />
      </FieldRow>

      <FieldRow
        testId="field-gestation-days"
        label="Gestation (days)"
        hint="How many days pass between conception and birth. Default 270 (~9 months)."
      >
        <input
          type="number"
          min={1}
          value={world.gestationDays}
          onChange={(e) => gestationDays.update(Number(e.target.value))}
          disabled={gestationDays.locked}
        />
      </FieldRow>

      <h3>Engine Limits</h3>

      <FieldRow
        testId="field-max-bytes-per-npc"
        label="Max memory per NPC/year (bytes)"
        hint="Safety cap on how much history each NPC is allowed to accumulate per simulated year — keeps long-running worlds from growing without bound. Higher = richer memory, more storage used. Default 4000; you're unlikely to need to change this."
      >
        <input
          type="number"
          min={1}
          value={world.maxBytesPerNpcPerYear}
          onChange={(e) => maxBytesPerNpcPerYear.update(Number(e.target.value))}
          disabled={maxBytesPerNpcPerYear.locked}
        />
      </FieldRow>

      <FieldRow
        testId="field-max-alive-npcs-enabled"
        label="Cap max alive NPCs"
        hint="Off by default (no cap). Turn on to hard-limit how many NPCs can be alive at once — useful to keep a very high-population world running smoothly."
      >
        <input type="checkbox" checked={world.maxAliveNpcsEnabled} onChange={(e) => maxAliveNpcsEnabled.update(e.target.checked)} />
      </FieldRow>

      {world.maxAliveNpcsEnabled && (
        <FieldRow
          testId="field-max-alive-npcs"
          label="Max alive NPCs"
          hint="Once the population hits this number, growth stops being simulated further."
          locked={maxAliveNpcs.locked}
          onToggleLock={maxAliveNpcs.toggleLock}
        >
          <input
            type="number"
            min={1}
            value={world.maxAliveNpcs}
            onChange={(e) => maxAliveNpcs.update(Number(e.target.value))}
            disabled={maxAliveNpcs.locked}
          />
        </FieldRow>
      )}

      <p className="inspector-empty-note">
        Life table (age-bracketed mortality curve) isn't editable yet — generation uses the engine's default curve.
      </p>
    </section>
  );
}
