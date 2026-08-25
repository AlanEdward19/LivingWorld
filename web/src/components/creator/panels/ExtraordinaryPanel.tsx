import { ObjectListEditor, type FieldSpec } from "../../formFields";
import type {
  ExtraordinaryCulturalResponseRow,
  ExtraordinaryDescriptorRow,
} from "../../../scenarioDefaults";
import type { PanelProps } from "./types";
import { useState } from "react";
import { EXTRAORDINARY_TEMPLATES } from "../../../extraordinaryTemplates";
import { PowerBuilder } from "./PowerBuilder";
import { parseEffects, STAT_KEYS, STAT_LABELS } from "./powerBuilderVocab";

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

function descriptorSummary(row: ExtraordinaryDescriptorRow): string {
  const { stats, flight, speed, construct } = parseEffects(row.effects);
  const bits: string[] = [];
  for (const key of STAT_KEYS) {
    const value = stats[key];
    if (value !== undefined) bits.push(`${value > 0 ? "+" : ""}${value} ${STAT_LABELS[key].toLowerCase()}`);
  }
  if (flight) bits.push("voo");
  if (speed !== null) bits.push(`${speed}× vel.`);
  if (construct) bits.push("constructos");
  return bits.length ? bits.join(", ") : "sem efeitos definidos ainda";
}

export function ExtraordinaryPanel({ form, set }: PanelProps) {
  const [templateIndex, setTemplateIndex] = useState(0);
  const [editingIndex, setEditingIndex] = useState<number | "new" | null>(null);
  const [newRowSeed, setNewRowSeed] = useState<ExtraordinaryDescriptorRow>(EMPTY_DESCRIPTOR);
  const template = EXTRAORDINARY_TEMPLATES[templateIndex];

  function saveDescriptor(row: ExtraordinaryDescriptorRow) {
    if (editingIndex === "new") {
      set("extraordinaryEnabled", true);
      set("extraordinaryDescriptors", [...form.extraordinaryDescriptors, row]);
    } else if (typeof editingIndex === "number") {
      set("extraordinaryDescriptors", form.extraordinaryDescriptors.map((r, i) => (i === editingIndex ? row : r)));
    }
    setEditingIndex(null);
  }

  function removeDescriptor(index: number) {
    set("extraordinaryDescriptors", form.extraordinaryDescriptors.filter((_, i) => i !== index));
  }

  if (editingIndex !== null) {
    const row = editingIndex === "new" ? newRowSeed : form.extraordinaryDescriptors[editingIndex];
    return <PowerBuilder row={row} onSave={saveDescriptor} onCancel={() => setEditingIndex(null)} />;
  }

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

      <fieldset className="extraordinary-template-picker">
        <legend>Começar por um template</legend>
        <label>
          Modelo:{" "}
          <select aria-label="Template de poder" value={templateIndex} onChange={(event) => setTemplateIndex(Number(event.target.value))}>
            {EXTRAORDINARY_TEMPLATES.map((item, index) => <option key={item.name} value={index}>{item.name}</option>)}
          </select>
        </label>
        <p>{template.description}</p>
        <button
          type="button"
          className="ui-btn ui-btn--primary"
          onClick={() => {
            set("extraordinaryEnabled", true);
            setNewRowSeed({ ...template.descriptor });
            setEditingIndex("new");
          }}
        >
          Adicionar e personalizar
        </button>
      </fieldset>

      <div className="power-list">
        <div className="power-list-header">
          <h4>Poderes deste mundo</h4>
          <button type="button" className="ui-btn ui-btn--primary" onClick={() => { setNewRowSeed(EMPTY_DESCRIPTOR); setEditingIndex("new"); }}>+ Adicionar poder</button>
        </div>
        {form.extraordinaryDescriptors.length === 0 ? (
          <p className="npc-command-empty">Nenhum poder criado ainda.</p>
        ) : (
          <ul className="power-list-items">
            {form.extraordinaryDescriptors.map((row, index) => (
              <li key={index} className="power-list-card">
                <div>
                  <strong>{row.id || "(sem identificador)"}</strong>
                  <small>{descriptorSummary(row)}</small>
                </div>
                <div className="power-list-actions">
                  <button type="button" className="ui-btn" onClick={() => setEditingIndex(index)}>Editar</button>
                  <button type="button" className="ui-btn ui-btn--danger" onClick={() => removeDescriptor(index)}>Remover</button>
                </div>
              </li>
            ))}
          </ul>
        )}
      </div>

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
