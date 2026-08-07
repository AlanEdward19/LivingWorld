// Fase 15.1, T26: bloco "Economia" do antigo `CreateWorldForm`, portado sem perder campo.
// `wageByProfession`/`locationTypeByProfession` ganham o catálogo real de profissão quando
// disponível.
import { KeyNumberListEditor, ObjectListEditor } from "../../formFields";
import type { PanelProps } from "./types";

export function EconomyPanel({ form, set, professionNames }: PanelProps) {
  return (
    <div>
      <div className="form-row">
        <label>
          Habilitada:{" "}
          <input
            type="checkbox"
            checked={form.economyEnabled}
            onChange={(e) => set("economyEnabled", e.target.checked)}
          />
        </label>
        <label>
          Recurso comida (id):{" "}
          <input
            type="number"
            value={form.foodResourceId}
            onChange={(e) => set("foodResourceId", Number(e.target.value))}
          />
        </label>
        <label>
          Recurso água (id):{" "}
          <input
            type="number"
            value={form.waterResourceId}
            onChange={(e) => set("waterResourceId", Number(e.target.value))}
          />
        </label>
        <label>
          Sensibilidade de preço:{" "}
          <input
            type="number"
            step="any"
            value={form.priceSensitivity}
            onChange={(e) => set("priceSensitivity", Number(e.target.value))}
          />
        </label>
      </div>

      <details>
        <summary>Avançado (capacidade, preços, salários, receitas, locais de trabalho)</summary>
        <label>
          Tipos de local de mercado (ids, csv):{" "}
          <input
            type="text"
            value={form.marketLocationTypeIds}
            onChange={(e) => set("marketLocationTypeIds", e.target.value)}
          />
        </label>

        <KeyNumberListEditor
          label="Capacidade por recurso+local (chave: resourceId,locationTypeId)"
          keyLabel="resourceId,locationTypeId"
          rows={form.capacityByResourceLocation}
          onChange={(rows) => set("capacityByResourceLocation", rows)}
        />
        <KeyNumberListEditor
          label="Deterioração por dia por recurso"
          keyLabel="resourceId"
          rows={form.spoilagePerDayByResource}
          onChange={(rows) => set("spoilagePerDayByResource", rows)}
        />
        <KeyNumberListEditor
          label="Salário por profissão"
          keyLabel="professionId"
          rows={form.wageByProfession}
          onChange={(rows) => set("wageByProfession", rows)}
          labels={professionNames}
        />
        <KeyNumberListEditor
          label="Preço mínimo por recurso"
          keyLabel="resourceId"
          rows={form.priceFloor}
          onChange={(rows) => set("priceFloor", rows)}
        />
        <KeyNumberListEditor
          label="Preço máximo por recurso"
          keyLabel="resourceId"
          rows={form.priceCeiling}
          onChange={(rows) => set("priceCeiling", rows)}
        />
        <KeyNumberListEditor
          label="Demanda base por NPC por recurso"
          keyLabel="resourceId"
          rows={form.demandBaselinePerNpc}
          onChange={(rows) => set("demandBaselinePerNpc", rows)}
        />
        <KeyNumberListEditor
          label="Tipo de local por profissão"
          keyLabel="professionId"
          rows={form.locationTypeByProfession}
          onChange={(rows) => set("locationTypeByProfession", rows)}
          labels={professionNames}
        />

        <ObjectListEditor
          label="Receitas (por tipo de local)"
          fields={
            [
              { name: "locationTypeId", label: "tipo de local (id)", type: "number" },
              { name: "inputs", label: "insumos (resId:qtd,...)", type: "text" },
              { name: "outputs", label: "produtos (resId:qtd,...)", type: "text" },
              { name: "maxWorkersPerCycle", label: "trabalhadores máx./ciclo", type: "number" },
              { name: "requiresCellResource", label: "exige recurso de célula (id)", type: "nullable-number" },
            ] as const
          }
          rows={form.recipes}
          emptyRow={{ locationTypeId: 0, inputs: "", outputs: "", maxWorkersPerCycle: 1, requiresCellResource: null }}
          onChange={(rows) => set("recipes", rows)}
        />

        <ObjectListEditor
          label="Locais de trabalho"
          fields={
            [
              { name: "locationTypeId", label: "tipo de local (id)", type: "number" },
              { name: "x", label: "x", type: "number" },
              { name: "y", label: "y", type: "number" },
              { name: "maxVacancies", label: "vagas máx.", type: "number" },
              { name: "treasury", label: "tesouro", type: "number" },
              { name: "stock", label: "estoque (resId:qtd,...)", type: "text" },
              { name: "prices", label: "preços (resId:qtd,...)", type: "text" },
            ] as const
          }
          rows={form.workplaces}
          emptyRow={{ locationTypeId: 0, x: 0, y: 0, maxVacancies: 1, treasury: 0, stock: "", prices: "" }}
          onChange={(rows) => set("workplaces", rows)}
        />
      </details>
    </div>
  );
}
