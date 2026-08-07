// Fase 15.1, T26: bloco "Dinâmica" do antigo `CreateWorldForm`, portado sem perder campo.
// `professionBiases`/`skillBiases` ganham o catálogo real quando disponível.
import { ObjectListEditor } from "../../formFields";
import type { PanelProps } from "./types";

export function DynamicsPanel({ form, set, professionNames, skillNames }: PanelProps) {
  return (
    <div>
      <p className="approximate-note">
        Opcional — sem nada aqui, o mundo roda com o catálogo de profissões/habilidades fixo do
        resto do formulário, sem viés nem transformação ao longo do tempo.
      </p>
      <ObjectListEditor
        label="Vieses de profissão"
        fields={[
          { name: "professionId", label: "profissão (id)", type: "number", labels: professionNames },
          { name: "weight", label: "peso", type: "number" },
          { name: "name", label: "nome (opcional)", type: "text" },
        ]}
        rows={form.professionBiases}
        emptyRow={{ professionId: 0, weight: 1, name: "" }}
        onChange={(rows) => set("professionBiases", rows)}
      />
      <ObjectListEditor
        label="Vieses de habilidade"
        fields={[
          { name: "skillId", label: "habilidade (id)", type: "number", labels: skillNames },
          { name: "weight", label: "peso", type: "number" },
          { name: "name", label: "nome (opcional)", type: "text" },
        ]}
        rows={form.skillBiases}
        emptyRow={{ skillId: 0, weight: 1, name: "" }}
        onChange={(rows) => set("skillBiases", rows)}
      />
      <ObjectListEditor
        label="Regras de transformação"
        fields={
          [
            {
              name: "kind",
              label: "tipo",
              type: "select",
              options: ["Emerge", "Merge", "Split", "Disappear"],
            },
            { name: "sourceProfessionIds", label: "profissões origem (ids, csv)", type: "text" },
            { name: "targetProfessionIds", label: "profissões destino (ids, csv)", type: "text" },
            { name: "triggerTick", label: "tick de disparo (vazio=imediato)", type: "nullable-number" },
          ] as const
        }
        rows={form.transformationRules}
        emptyRow={{ kind: "Emerge", sourceProfessionIds: "", targetProfessionIds: "", triggerTick: null }}
        onChange={(rows) => set("transformationRules", rows)}
      />
    </div>
  );
}
