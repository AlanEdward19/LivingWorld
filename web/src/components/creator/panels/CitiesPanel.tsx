// Fase 15.1, T26: bloco "Cidades" do antigo `CreateWorldForm`, portado sem perder campo.
import { ObjectListEditor } from "../../formFields";
import type { PanelProps } from "./types";

export function CitiesPanel({ form, set }: PanelProps) {
  return (
    <div>
      <div className="form-row">
        <label>
          Habilitadas:{" "}
          <input
            type="checkbox"
            checked={form.citiesEnabled}
            onChange={(e) => set("citiesEnabled", e.target.checked)}
          />
        </label>
        <label>
          Ticks de organização:{" "}
          <input
            type="number"
            value={form.organizationTicks}
            onChange={(e) => set("organizationTicks", Number(e.target.value))}
          />
        </label>
        <label>
          Ticks ociosos até elegível p/ materialização:{" "}
          <input
            type="number"
            value={form.materializationIdleTicksBeforeEligible}
            onChange={(e) => set("materializationIdleTicksBeforeEligible", Number(e.target.value))}
          />
        </label>
      </div>

      <details>
        <summary>
          Avançado (limiares de escassez/migração/fundação, receitas de construção, cidades
          iniciais)
        </summary>
        <div className="form-row">
          <label>
            Limiar de escassez de comida:{" "}
            <input
              type="number"
              step="any"
              value={form.foodShortageThreshold}
              onChange={(e) => set("foodShortageThreshold", Number(e.target.value))}
            />
          </label>
          <label>
            Limiar de escassez de moradia:{" "}
            <input
              type="number"
              step="any"
              value={form.housingShortageThreshold}
              onChange={(e) => set("housingShortageThreshold", Number(e.target.value))}
            />
          </label>
          <label>
            Limiar de escassez de segurança:{" "}
            <input
              type="number"
              step="any"
              value={form.securityShortageThreshold}
              onChange={(e) => set("securityShortageThreshold", Number(e.target.value))}
            />
          </label>
          <label>
            Taxa de emigração por déficit:{" "}
            <input
              type="number"
              step="any"
              value={form.emigrationRatePerDeficitUnit}
              onChange={(e) => set("emigrationRatePerDeficitUnit", Number(e.target.value))}
            />
          </label>
        </div>
        <div className="form-row">
          <label>
            Peso migração - emprego:{" "}
            <input
              type="number"
              step="any"
              value={form.migrationEmploymentWeight}
              onChange={(e) => set("migrationEmploymentWeight", Number(e.target.value))}
            />
          </label>
          <label>
            Peso migração - comida:{" "}
            <input
              type="number"
              step="any"
              value={form.migrationFoodWeight}
              onChange={(e) => set("migrationFoodWeight", Number(e.target.value))}
            />
          </label>
          <label>
            Peso migração - segurança:{" "}
            <input
              type="number"
              step="any"
              value={form.migrationSecurityWeight}
              onChange={(e) => set("migrationSecurityWeight", Number(e.target.value))}
            />
          </label>
          <label>
            Peso migração - laços familiares:{" "}
            <input
              type="number"
              step="any"
              value={form.migrationFamilyTiesWeight}
              onChange={(e) => set("migrationFamilyTiesWeight", Number(e.target.value))}
            />
          </label>
        </div>
        <div className="form-row">
          <label>
            Limiar fundação - concentração:{" "}
            <input
              type="number"
              step="any"
              value={form.foundingConcentrationThreshold}
              onChange={(e) => set("foundingConcentrationThreshold", Number(e.target.value))}
            />
          </label>
          <label>
            Limiar fundação - recurso:{" "}
            <input
              type="number"
              step="any"
              value={form.foundingResourceThreshold}
              onChange={(e) => set("foundingResourceThreshold", Number(e.target.value))}
            />
          </label>
          <label>
            Limiar fundação - rota:{" "}
            <input
              type="number"
              step="any"
              value={form.foundingRouteThreshold}
              onChange={(e) => set("foundingRouteThreshold", Number(e.target.value))}
            />
          </label>
          <label>
            Limiar fundação - defensabilidade:{" "}
            <input
              type="number"
              step="any"
              value={form.foundingDefensibilityThreshold}
              onChange={(e) => set("foundingDefensibilityThreshold", Number(e.target.value))}
            />
          </label>
          <label>
            Limiar fundação - liderança:{" "}
            <input
              type="number"
              step="any"
              value={form.foundingLeadershipThreshold}
              onChange={(e) => set("foundingLeadershipThreshold", Number(e.target.value))}
            />
          </label>
        </div>

        <ObjectListEditor
          label="Receitas de construção"
          fields={
            [
              { name: "buildingTypeId", label: "tipo de construção (id)", type: "number" },
              { name: "inputs", label: "insumos (resId:qtd,...)", type: "text" },
              { name: "ticksToBuild", label: "ticks p/ construir", type: "number" },
              { name: "housingCapacityProvided", label: "capacidade de moradia", type: "number" },
            ] as const
          }
          rows={form.buildingRecipes}
          emptyRow={{ buildingTypeId: 0, inputs: "", ticksToBuild: 1, housingCapacityProvided: 0 }}
          onChange={(rows) => set("buildingRecipes", rows)}
        />

        <ObjectListEditor
          label="Cidades iniciais"
          fields={[
            { name: "x", label: "x", type: "number" },
            { name: "y", label: "y", type: "number" },
            { name: "foundedAtTick", label: "fundada no tick", type: "number" },
            { name: "count", label: "população", type: "number" },
            { name: "wealthSum", label: "riqueza total", type: "number" },
            { name: "healthSum", label: "saúde total", type: "number" },
          ]}
          rows={form.cities}
          emptyRow={{ x: 0, y: 0, foundedAtTick: 0, count: 0, wealthSum: 0, healthSum: 0 }}
          onChange={(rows) => set("cities", rows)}
        />
      </details>
    </div>
  );
}
