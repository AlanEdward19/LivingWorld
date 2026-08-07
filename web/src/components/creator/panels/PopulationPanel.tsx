// Fase 15.1, T26: bloco "População" do antigo `CreateWorldForm`, portado sem perder campo.
import { ObjectListEditor } from "../../formFields";
import type { PanelProps } from "./types";

export function PopulationPanel({ form, set }: PanelProps) {
  return (
    <div>
      <div className="form-row">
        <label>
          População inicial:{" "}
          <input
            type="number"
            aria-label="population-initial"
            value={form.initialPopulation}
            onChange={(e) => set("initialPopulation", Number(e.target.value))}
          />
        </label>
        <label>
          Cultura:{" "}
          <input
            type="number"
            aria-label="population-culture"
            value={form.culture}
            onChange={(e) => set("culture", Number(e.target.value))}
          />
        </label>
        <label>
          Vila X:{" "}
          <input type="number" value={form.villageX} onChange={(e) => set("villageX", Number(e.target.value))} />
        </label>
        <label>
          Vila Y:{" "}
          <input type="number" value={form.villageY} onChange={(e) => set("villageY", Number(e.target.value))} />
        </label>
      </div>
      <div className="form-row">
        <label>
          Culturas (ids, csv):{" "}
          <input type="text" value={form.cultureIds} onChange={(e) => set("cultureIds", e.target.value)} />
        </label>
        <label>
          Profissões (ids, csv):{" "}
          <input
            type="text"
            value={form.professionIds}
            onChange={(e) => set("professionIds", e.target.value)}
          />
        </label>
        <label>
          Tipos de local (ids, csv):{" "}
          <input
            type="text"
            value={form.locationTypeIds}
            onChange={(e) => set("locationTypeIds", e.target.value)}
          />
        </label>
      </div>
      <div className="form-row">
        <label>
          Longevidade máxima (anos):{" "}
          <input
            type="number"
            value={form.maxLongevityYears}
            onChange={(e) => set("maxLongevityYears", Number(e.target.value))}
          />
        </label>
        <label>
          Idade mínima de fertilidade:{" "}
          <input
            type="number"
            value={form.fertilityMinAge}
            onChange={(e) => set("fertilityMinAge", Number(e.target.value))}
          />
        </label>
        <label>
          Idade máxima de fertilidade:{" "}
          <input
            type="number"
            value={form.fertilityMaxAge}
            onChange={(e) => set("fertilityMaxAge", Number(e.target.value))}
          />
        </label>
        <label>
          Chance anual de concepção:{" "}
          <input
            type="number"
            step="any"
            value={form.annualConceptionChance}
            onChange={(e) => set("annualConceptionChance", Number(e.target.value))}
          />
        </label>
        <label>
          Gestação (dias):{" "}
          <input
            type="number"
            value={form.gestationDays}
            onChange={(e) => set("gestationDays", Number(e.target.value))}
          />
        </label>
      </div>

      <details>
        <summary>Avançado (tabela de mortalidade, orçamento de bytes)</summary>
        <label>
          Bytes máx. por NPC/ano:{" "}
          <input
            type="number"
            value={form.maxBytesPerNpcPerYear}
            onChange={(e) => set("maxBytesPerNpcPerYear", Number(e.target.value))}
          />
        </label>
        <ObjectListEditor
          label="Tabela de mortalidade"
          fields={[
            { name: "minAgeYears", label: "idade min", type: "number" },
            { name: "maxAgeYears", label: "idade max", type: "number" },
            { name: "baseAnnualMortality", label: "mortalidade anual", type: "number" },
          ]}
          rows={form.lifeTableBrackets}
          emptyRow={{ minAgeYears: 0, maxAgeYears: 0, baseAnnualMortality: 0 }}
          onChange={(rows) => set("lifeTableBrackets", rows)}
        />
      </details>
    </div>
  );
}
