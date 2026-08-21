import { useEffect, useState, useSyncExternalStore } from "react";
import { FollowButton } from "./FollowButton";
import type { SimulationStore } from "../../state/simulationStore";
import type { ViewStore } from "../../state/viewStore";
import type { EntityRef } from "../../map-engine/types";
import type { ConversationTurn, NpcInspection } from "../../data/contracts";
import type { NarrativeSources } from "../../data/sources";
import { materializeNpc } from "../../api";
import { ACTION_LABELS } from "../../map-engine/actionVisuals";
import { NpcTokenSvg } from "../NpcTokenSvg";

const POOLED_LOD = 2;

export interface NpcInspectorProps {
  entityRef: EntityRef;
  simulationStore: SimulationStore;
  viewStore: ViewStore;
  /** T7 (LWV-05): biografia + conversa. Opcional — ausente em contextos que ainda não têm essas
   * fontes (ex.: testes de T5/T6 focados só em identidade/vida). */
  narrativeSources?: NarrativeSources;
}

const CONVERSATION_REJECTION_LABELS: Record<string, string> = {
  "npc-dead": "Esta pessoa já morreu — a conversa foi encerrada.",
  "session-not-found": "Sessão de conversa não encontrada.",
  "session-ended": "Esta conversa já terminou.",
};

function NpcBiography({ npcId, source }: { npcId: number; source: NarrativeSources["biography"] }) {
  const [prose, setProse] = useState<string | null>();

  useEffect(() => {
    setProse(undefined);
    let cancelled = false;
    void source.load(npcId).then((result) => {
      if (!cancelled) setProse(result?.prose ?? null);
    });
    return () => { cancelled = true; };
  }, [npcId, source]);

  return (
    <section aria-labelledby="npc-biography-title">
      <h4 id="npc-biography-title">Biografia</h4>
      {prose === undefined && <p role="status">Carregando biografia…</p>}
      {prose === null && <p>Nenhum evento registrado ainda.</p>}
      {typeof prose === "string" && <p>{prose}</p>}
    </section>
  );
}

function NpcConversation({ npcId, source }: { npcId: number; source: NarrativeSources["conversation"] }) {
  const [sessionId, setSessionId] = useState<number | null>(null);
  const [rejection, setRejection] = useState<string | null>(null);
  const [turns, setTurns] = useState<ConversationTurn[]>([]);
  const [message, setMessage] = useState("");
  const [busy, setBusy] = useState(false);

  async function handleStart() {
    setBusy(true);
    try {
      const outcome = await source.start(npcId);
      if (outcome.accepted) {
        setSessionId(outcome.sessionId);
        setRejection(null);
        setTurns([]);
      } else {
        setRejection(outcome.reason);
      }
    } finally {
      setBusy(false);
    }
  }

  async function handleSend() {
    if (sessionId === null || !message.trim()) return;
    setBusy(true);
    try {
      const outcome = await source.send(sessionId, message);
      if (outcome.ok) {
        setTurns((previous) => [...previous, outcome.turn]);
        setMessage("");
      } else {
        setRejection(outcome.reason);
        setSessionId(null);
      }
    } finally {
      setBusy(false);
    }
  }

  async function handleEnd() {
    if (sessionId === null) return;
    await source.end(sessionId);
    setSessionId(null);
  }

  return (
    <section aria-labelledby="npc-conversation-title">
      <h4 id="npc-conversation-title">Conversa</h4>
      {rejection && <p role="note">{CONVERSATION_REJECTION_LABELS[rejection] ?? "Conversa indisponível."}</p>}
      {sessionId === null ? (
        <button type="button" onClick={handleStart} disabled={busy}>Iniciar conversa</button>
      ) : (
        <>
          <ul className="npc-conversation-turns">
            {turns.map((turn, index) => <li key={index}>{turn.dialogue}</li>)}
          </ul>
          <input
            aria-label="Mensagem"
            value={message}
            onChange={(event) => setMessage(event.target.value)}
          />
          <button type="button" onClick={handleSend} disabled={busy || !message.trim()}>Enviar</button>
          <button type="button" onClick={handleEnd} disabled={busy}>Encerrar</button>
        </>
      )}
    </section>
  );
}

const TARGET_LABELS: Record<string, string> = {
  workplace: "Local de trabalho",
  household: "Domicílio",
  npc: "Pessoa",
};

function idValue(value: { value: number } | null): string {
  return value ? String(value.value) : "—";
}

function targetLabel(target: NpcInspection["actionTarget"]): string {
  if (!target) return "Sem alvo definido";
  return `${TARGET_LABELS[target.kind] ?? target.kind} ${target.id}`;
}

function NeedMeter({ label, value }: { label: string; value: number }) {
  return (
    <div className="npc-need">
      <span>{label}</span>
      <progress aria-label={label} max={100} value={value} />
      <strong>{value}</strong>
    </div>
  );
}

export function NpcInspector({ entityRef, simulationStore, viewStore, narrativeSources }: NpcInspectorProps) {
  const npcId = Number(entityRef.id);
  const [materializing, setMaterializing] = useState(false);
  const inspection = useSyncExternalStore(
    (onStoreChange) => simulationStore.subscribe(onStoreChange),
    () => simulationStore.npcInspectionOf(npcId),
  );

  useEffect(() => {
    void simulationStore.inspectNpc(npcId);
  }, [npcId, simulationStore]);

  async function handleMaterialize() {
    setMaterializing(true);
    try {
      await materializeNpc(npcId);
    } finally {
      await simulationStore.inspectNpc(npcId);
      setMaterializing(false);
    }
  }

  if (inspection === undefined) {
    return (
      <div className="npc-living-inspector">
        <h3>NPC {entityRef.id}</h3>
        <p role="status">Carregando vida…</p>
      </div>
    );
  }
  if (inspection === null) {
    return <p role="note">Este habitante não está materializado ou não pôde ser inspecionado.</p>;
  }
  // T50: id reservado num pool agregado (City.PoolNpcIds) — sem atributos reais ainda (não
  // existem até sortear), só oferece a ação explícita de materializar.
  if (inspection.lod === POOLED_LOD) {
    return (
      <div className="npc-living-inspector" data-testid="npc-pooled-inspector">
        <h3>NPC {entityRef.id}</h3>
        <p role="note">Ainda não materializado — faz parte do pool agregado desta cidade.</p>
        <button type="button" onClick={handleMaterialize} disabled={materializing}>
          {materializing ? "Materializando…" : "Materializar"}
        </button>
      </div>
    );
  }

  const action = inspection.currentAction === null
    ? "Sem ação atual"
    : ACTION_LABELS[inspection.currentAction] ?? `Atividade ${inspection.currentAction}`;
  const skills = Object.entries(inspection.skills.values);

  return (
    <div className="npc-living-inspector">
      <div className="npc-inspector-identity">
        <NpcTokenSvg npcId={entityRef.id} currentAction={inspection.currentAction} className="npc-inspector-pawn" />
        <div>
          <small>{inspection.lod === 0 ? "Pessoa materializada" : "Registro histórico"}</small>
          <h3>{inspection.name}</h3>
          <span>{inspection.ageYears} anos · cultura {inspection.culture.id}</span>
        </div>
      </div>

      <section aria-labelledby="npc-activity-title">
        <h4 id="npc-activity-title">Agora</h4>
        <dl>
          <dt>Ação</dt><dd>{action}</dd>
          <dt>Alvo</dt><dd>{targetLabel(inspection.actionTarget)}</dd>
          <dt>Desde o tick</dt><dd>{inspection.actionStartedAtTick}</dd>
          <dt>Posição</dt><dd>({inspection.currentLocation.x}, {inspection.currentLocation.y})</dd>
          <dt>LOD</dt><dd>{inspection.lod === 0 ? "Materializado" : "Arquivado"}</dd>
        </dl>
      </section>

      <section aria-labelledby="npc-needs-title">
        <h4 id="npc-needs-title">Bem-estar</h4>
        <NeedMeter label="Saúde" value={inspection.health} />
        <NeedMeter label="Fome" value={inspection.hunger} />
        <NeedMeter label="Sede" value={inspection.thirst} />
        <NeedMeter label="Sono" value={inspection.sleep} />
        <NeedMeter label="Social" value={inspection.social} />
      </section>

      <section aria-labelledby="npc-family-title">
        <h4 id="npc-family-title">Família</h4>
        <dl>
          <dt>Domicílio</dt><dd>{idValue(inspection.household)}</dd>
          <dt>Mãe</dt><dd>{idValue(inspection.motherId)}</dd>
          <dt>Pai</dt><dd>{idValue(inspection.fatherId)}</dd>
          <dt>Cônjuge</dt><dd>{idValue(inspection.spouse)}</dd>
        </dl>
      </section>

      <section aria-labelledby="npc-work-title">
        <h4 id="npc-work-title">Trabalho e habilidades</h4>
        <dl>
          <dt>Profissão</dt><dd>{inspection.profession.id}</dd>
          <dt>Empregador</dt><dd>{idValue(inspection.employer)}</dd>
        </dl>
        {skills.length > 0 ? (
          <ul className="npc-skills">
            {skills.map(([skillId, value]) => <li key={skillId}>Habilidade {skillId}: {value.toFixed(1)}</li>)}
          </ul>
        ) : <p>Nenhuma habilidade desenvolvida.</p>}
      </section>

      <section aria-labelledby="npc-knowledge-title">
        <h4 id="npc-knowledge-title">O que esta pessoa acredita</h4>
        {inspection.beliefs.length > 0 ? (
          <ul className="npc-beliefs">
            {inspection.beliefs.map((belief, index) => <li key={`${index}:${belief}`}>{belief}</li>)}
          </ul>
        ) : <p>Nenhum relato conhecido.</p>}
      </section>

      {narrativeSources && <NpcBiography npcId={npcId} source={narrativeSources.biography} />}
      {narrativeSources && <NpcConversation npcId={npcId} source={narrativeSources.conversation} />}

      <div className="entity-inspector-actions">
        <FollowButton entityRef={entityRef} viewStore={viewStore} />
      </div>
    </div>
  );
}
