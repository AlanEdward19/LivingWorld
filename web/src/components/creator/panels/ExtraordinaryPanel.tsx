import { ObjectListEditor, type FieldSpec } from "../../formFields";
import type { ExtraordinaryDescriptorRow } from "../../../scenarioDefaults";
import type { PanelProps } from "./types";

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
  { name: "mode", label: "modo", type: "text" },
  { name: "costs", label: "custos (csv)", type: "text" },
  { name: "reliability", label: "confiabilidade", type: "text" },
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

export function ExtraordinaryPanel({ form, set }: PanelProps) {
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
      <p className="approximate-note">
        Cada capacidade usa dados livres. Aparência, metabolismo, senescência e condição de
        manifestação têm campos próprios, sem criar um tipo nominal por capacidade.
      </p>
      <ObjectListEditor
        label="Descritores extraordinários"
        fields={DESCRIPTOR_FIELDS}
        rows={form.extraordinaryDescriptors}
        emptyRow={EMPTY_DESCRIPTOR}
        onChange={(rows) => set("extraordinaryDescriptors", rows)}
      />
    </div>
  );
}
