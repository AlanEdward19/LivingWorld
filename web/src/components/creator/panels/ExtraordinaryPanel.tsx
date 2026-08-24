import { ObjectListEditor, type FieldSpec } from "../../formFields";
import type {
  ExtraordinaryCulturalResponseRow,
  ExtraordinaryDescriptorRow,
} from "../../../scenarioDefaults";
import type { PanelProps } from "./types";
import { useState } from "react";
import { EXTRAORDINARY_TEMPLATES } from "../../../extraordinaryTemplates";

const EMPTY_DESCRIPTOR: ExtraordinaryDescriptorRow = {
  id: "",
  source: "",
  effects: "",
  mode: "Active",
  costs: "",
  reliability: "Guaranteed",
  failureModes: "",
  intrinsicVulnerabilities: "",
  manifestations: "",
  acquisitionRules: "",
  appearanceScaleMultiplier: 1,
  appearanceSkinTint: "",
  appearanceMovementTrail: "",
  needSubstitutionReplacesNeed: "",
  needSubstitutionResourceId: null,
  needSubstitutionUnitsPerUse: 1,
  senescenceRateMultiplier: 1,
  manifestationCondition: "",
};

const DESCRIPTOR_FIELDS: readonly FieldSpec<ExtraordinaryDescriptorRow>[] = [
  { name: "id", label: "identificador", type: "text" },
  { name: "source", label: "fonte", type: "text" },
  { name: "effects", label: "efeitos alvo:magnitude (csv)", type: "text" },
  { name: "mode", label: "modo", type: "select", options: ["Active", "Passive", "Triggered", "Conditional"] },
  { name: "costs", label: "custos (csv)", type: "text" },
  { name: "reliability", label: "confiabilidade", type: "select", options: ["Guaranteed", "ResolutionCheck"] },
  { name: "failureModes", label: "falhas (csv)", type: "text" },
  { name: "intrinsicVulnerabilities", label: "vulnerabilidades (csv)", type: "text" },
  { name: "manifestations", label: "manifestações (csv)", type: "text" },
  { name: "acquisitionRules", label: "aquisição (csv)", type: "text" },
  { name: "appearanceScaleMultiplier", label: "escala visual", type: "number" },
  { name: "appearanceSkinTint", label: "tom/palidez", type: "text" },
  { name: "appearanceMovementTrail", label: "trilha de movimento", type: "text" },
  { name: "needSubstitutionReplacesNeed", label: "necessidade substituída", type: "text" },
  { name: "needSubstitutionResourceId", label: "recurso metabólico", type: "nullable-number" },
  { name: "needSubstitutionUnitsPerUse", label: "unidades por uso", type: "number" },
  { name: "senescenceRateMultiplier", label: "multiplicador de senescência", type: "number" },
  { name: "manifestationCondition", label: "condição de manifestação", type: "text" },
];

const EMPTY_CULTURAL_RESPONSE: ExtraordinaryCulturalResponseRow = {
  cultureId: 0,
  manifestation: "",
  response: "",
};

const CULTURAL_RESPONSE_FIELDS: readonly FieldSpec<ExtraordinaryCulturalResponseRow>[] = [
  { name: "cultureId", label: "cultura", type: "number" },
  { name: "manifestation", label: "manifestação observada", type: "text" },
  { name: "response", label: "resposta cultural", type: "text" },
];

export function ExtraordinaryPanel({ form, set }: PanelProps) {
  const [templateIndex, setTemplateIndex] = useState(0);
  const template = EXTRAORDINARY_TEMPLATES[templateIndex];
  return (
    <div>
      <label>
        <input
          type="checkbox"
          aria-label="Ativar extraordinário"
          checked={form.extraordinaryEnabled}
          onChange={(event) => set("extraordinaryEnabled", event.target.checked)}
        />{" "}
        Ativar extraordinário neste mundo
      </label>
      <label>
        Prevalência inicial (0–1):{" "}
        <input
          type="number"
          aria-label="Prevalência extraordinária"
          min={0}
          max={1}
          step={0.01}
          value={form.extraordinaryPrevalence}
          onChange={(event) => set("extraordinaryPrevalence", Number(event.target.value))}
        />
      </label>
      <p className="approximate-note">
        Cada capacidade usa dados livres. Aparência, metabolismo, senescência e condição de
        manifestação têm campos próprios, sem criar um tipo nominal por capacidade.
      </p>
      <fieldset className="extraordinary-template-picker">
        <legend>Começar por um template</legend>
        <label>
          Modelo:{" "}
          <select aria-label="Template de poder" value={templateIndex} onChange={(event) => setTemplateIndex(Number(event.target.value))}>
            {EXTRAORDINARY_TEMPLATES.map((item, index) => <option key={item.name} value={index}>{item.name}</option>)}
          </select>
        </label>
        <p>{template.description}</p>
        <button type="button" onClick={() => {
          set("extraordinaryEnabled", true);
          set("extraordinaryDescriptors", [...form.extraordinaryDescriptors, { ...template.descriptor }]);
        }}>Adicionar e personalizar</button>
      </fieldset>
      <ObjectListEditor
        label="Descritores extraordinários"
        fields={DESCRIPTOR_FIELDS}
        rows={form.extraordinaryDescriptors}
        emptyRow={EMPTY_DESCRIPTOR}
        onChange={(rows) => set("extraordinaryDescriptors", rows)}
      />
      <ObjectListEditor
        label="Respostas culturais"
        fields={CULTURAL_RESPONSE_FIELDS}
        rows={form.extraordinaryCulturalResponses}
        emptyRow={EMPTY_CULTURAL_RESPONSE}
        onChange={(rows) => set("extraordinaryCulturalResponses", rows)}
      />
    </div>
  );
}
