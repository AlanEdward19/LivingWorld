// Fase 15.1, T15: inspector de NPC — campos do snapshot já carregado, sem disparar nenhuma
// fonte de detalhe extra na seleção. No motor real, `NpcInspectionQuery.Inspect` materializa o
// NPC (`NpcInspectionQuery.cs:17`) — é mutação, não leitura pura (context.md gap 10) — então o
// detalhe completo (idade, profissão, família, needs) só pode vir de uma ação EXPLÍCITA, nunca
// automaticamente ao selecionar. Aqui esse detalhe ainda não existe (nenhuma fonte mock o
// modela) — "Ver detalhes" existe e é testado, mas revela um aviso honesto, não dado inventado.
import { useState } from "react";
import { FollowButton } from "./FollowButton";
import type { SimulationStore } from "../../state/simulationStore";
import type { ViewStore } from "../../state/viewStore";
import type { EntityRef } from "../../map-engine/types";
import type { CityResidentMarker, GlobalNpcMarker } from "../../types";
import { NpcTokenSvg } from "../NpcTokenSvg";

export interface NpcInspectorProps {
  entityRef: EntityRef;
  simulationStore: SimulationStore;
  viewStore: ViewStore;
}

interface NpcPayloadShape {
  residents?: CityResidentMarker[];
  externalNpcs?: GlobalNpcMarker[];
}

function findMarker(payload: unknown, npcId: number): CityResidentMarker | GlobalNpcMarker | undefined {
  const candidate = payload as NpcPayloadShape | null;
  return (candidate?.residents ?? candidate?.externalNpcs)?.find((m) => m.id.value === npcId);
}

export function NpcInspector({ entityRef, simulationStore, viewStore }: NpcInspectorProps) {
  const [detailsOpen, setDetailsOpen] = useState(false);
  const npcId = Number(entityRef.id);
  const payload = simulationStore.currentPayload<unknown>(entityRef.space);
  const marker = findMarker(payload, npcId);
  const entity = simulationStore.entitiesOf(entityRef.space).find((e) => e.ref.id === entityRef.id);
  const currentAction = marker && "currentAction" in marker ? marker.currentAction : null;

  return (
    <div>
      <div className="npc-inspector-identity">
        <NpcTokenSvg npcId={entityRef.id} currentAction={currentAction} className="npc-inspector-pawn" />
        <div>
          <small>Personagem observado</small>
          <h3>NPC {entityRef.id}</h3>
        </div>
      </div>

      <dl>
        <dt>Posição</dt>
        <dd>
          {entity ? `(${entity.position.x}, ${entity.position.y})` : "—"}
        </dd>

        {currentAction !== null && currentAction !== undefined && (
          <>
            <dt>Ação atual</dt>
            <dd>{currentAction}</dd>
          </>
        )}
      </dl>

      <div className="entity-inspector-actions">
        <FollowButton entityRef={entityRef} viewStore={viewStore} />
        <button type="button" onClick={() => setDetailsOpen((v) => !v)}>
          {detailsOpen ? "Ocultar detalhes" : "Ver detalhes"}
        </button>
      </div>

      {detailsOpen && (
        <p role="note">
          Detalhe completo (idade, profissão, família, needs) ainda não modelado no cliente.
        </p>
      )}
    </div>
  );
}
