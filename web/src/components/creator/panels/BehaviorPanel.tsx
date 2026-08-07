// Fase 15.1, T26: bloco "Comportamento" do antigo `CreateWorldForm`, portado sem perder campo.
// `routineSlots.professionId` ganha o catálogo real quando disponível (T26 — o único id com
// nome de verdade além de skill).
import { ACTION_TYPES, type RoutineSlotRow } from "../../../scenarioDefaults";
import { ObjectListEditor, type FieldSpec } from "../../formFields";
import type { PanelProps } from "./types";

export function BehaviorPanel({ form, set, professionNames }: PanelProps) {
  const routineSlotFields: readonly FieldSpec<RoutineSlotRow>[] = [
    { name: "professionId", label: "profissão (vazio=qualquer)", type: "nullable-number", labels: professionNames },
    { name: "stage", label: "estágio", type: "select", options: ["Child", "Adult", "Elder"] },
    { name: "hourStart", label: "hora início", type: "number" },
    { name: "hourEnd", label: "hora fim", type: "number" },
    { name: "action", label: "ação", type: "select", options: ACTION_TYPES },
  ];

  return (
    <div>
      <div className="form-row">
        <label>
          Decaimento fome/h:{" "}
          <input
            type="number"
            step="any"
            value={form.hungerDecayPerHour}
            onChange={(e) => set("hungerDecayPerHour", Number(e.target.value))}
          />
        </label>
        <label>
          Decaimento sede/h:{" "}
          <input
            type="number"
            step="any"
            value={form.thirstDecayPerHour}
            onChange={(e) => set("thirstDecayPerHour", Number(e.target.value))}
          />
        </label>
        <label>
          Decaimento sono/h:{" "}
          <input
            type="number"
            step="any"
            value={form.sleepDecayPerHour}
            onChange={(e) => set("sleepDecayPerHour", Number(e.target.value))}
          />
        </label>
        <label>
          Decaimento social/h:{" "}
          <input
            type="number"
            step="any"
            value={form.socialDecayPerHour}
            onChange={(e) => set("socialDecayPerHour", Number(e.target.value))}
          />
        </label>
      </div>
      <div className="form-row">
        <label>
          Histerese:{" "}
          <input
            type="checkbox"
            checked={form.hysteresisEnabled}
            onChange={(e) => set("hysteresisEnabled", e.target.checked)}
          />
        </label>
        <label>
          Ação default:{" "}
          <select
            value={form.defaultAction}
            onChange={(e) => set("defaultAction", e.target.value as typeof form.defaultAction)}
          >
            {ACTION_TYPES.map((a) => (
              <option key={a} value={a}>
                {a}
              </option>
            ))}
          </select>
        </label>
      </div>

      <details>
        <summary>Avançado (limiares de seleção de ação, duração por ação, slots de rotina)</summary>
        <div className="form-row">
          <label>
            Limiar de urgência:{" "}
            <input
              type="number"
              value={form.urgencyThreshold}
              onChange={(e) => set("urgencyThreshold", Number(e.target.value))}
            />
          </label>
          <label>
            Passos máx. de seleção de ação:{" "}
            <input
              type="number"
              value={form.maxActionSelectionSteps}
              onChange={(e) => set("maxActionSelectionSteps", Number(e.target.value))}
            />
          </label>
          <label>
            Bônus de continuidade:{" "}
            <input
              type="number"
              step="any"
              value={form.continuityBonus}
              onChange={(e) => set("continuityBonus", Number(e.target.value))}
            />
          </label>
          <label>
            Eficiência de sono sem-teto:{" "}
            <input
              type="number"
              step="any"
              value={form.homelessSleepEfficiency}
              onChange={(e) => set("homelessSleepEfficiency", Number(e.target.value))}
            />
          </label>
        </div>

        <fieldset>
          <legend>Duração máx. por ação (horas)</legend>
          {ACTION_TYPES.map((action) => (
            <label key={action}>
              {action}:{" "}
              <input
                type="number"
                aria-label={`max-duration-${action}`}
                value={form.maxDurationHours[action]}
                onChange={(e) =>
                  set("maxDurationHours", { ...form.maxDurationHours, [action]: Number(e.target.value) })
                }
              />
            </label>
          ))}
        </fieldset>

        <ObjectListEditor
          label="Slots de rotina"
          fields={routineSlotFields}
          rows={form.routineSlots}
          emptyRow={{ professionId: null, stage: "Adult", hourStart: 0, hourEnd: 0, action: "Idle" }}
          onChange={(rows) => set("routineSlots", rows)}
        />
      </details>
    </div>
  );
}
