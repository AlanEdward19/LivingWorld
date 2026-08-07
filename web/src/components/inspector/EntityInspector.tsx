// Fase 15.1, T15: inspector flutuante universal (design.md; master prompt §14/§30) — absorve o
// papel de `SidePanel.tsx` (removido: uma única entidade selecionada por vez, um único painel,
// múltiplas ações por tipo). Dispatcher puro: lê `SelectionStore` e escolhe o conteúdo por
// `EntityRef.kind`; trocar de seleção troca o conteúdo sem desmontar o `<aside>`.
import { useSyncExternalStore } from "react";
import { CityInspector } from "./CityInspector";
import { NpcInspector } from "./NpcInspector";
import { BuildingInspector } from "./BuildingInspector";
import type { SelectionStore } from "../../state/selectionStore";
import type { SimulationStore } from "../../state/simulationStore";
import type { ViewStore } from "../../state/viewStore";

export interface EntityInspectorProps {
  selectionStore: SelectionStore;
  simulationStore: SimulationStore;
  viewStore: ViewStore;
}

export function EntityInspector({ selectionStore, simulationStore, viewStore }: EntityInspectorProps) {
  const selection = useSyncExternalStore(
    (onStoreChange) => selectionStore.subscribe(onStoreChange),
    () => selectionStore.current(),
  );

  if (!selection) {
    return null;
  }

  return (
    <aside className="side-panel" data-testid="entity-inspector">
      <button
        type="button"
        className="side-panel-close"
        aria-label="fechar-painel"
        onClick={() => selectionStore.clear()}
      >
        ×
      </button>
      <div className="side-panel-content">
        {selection.kind === "city" && (
          <CityInspector cityId={selection.id} simulationStore={simulationStore} viewStore={viewStore} />
        )}
        {selection.kind === "npc" && <NpcInspector entityRef={selection} simulationStore={simulationStore} />}
        {selection.kind === "building" && (
          <BuildingInspector entityRef={selection} simulationStore={simulationStore} viewStore={viewStore} />
        )}
      </div>
    </aside>
  );
}
