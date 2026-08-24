import { useEffect, useMemo, useState } from "react";
import type { AuthoringSource, PersonalityValues, PowerCatalogItem } from "../../data/sources";

const TRAITS: Array<[keyof PersonalityValues, string]> = [
  ["extroversion", "Extroversão"], ["agreeableness", "Amabilidade"],
  ["conscientiousness", "Conscienciosidade"], ["emotionalStability", "Estabilidade emocional"],
  ["openness", "Abertura"], ["ambition", "Ambição"], ["loyalty", "Lealdade"],
  ["altruism", "Altruísmo"], ["impulsivity", "Impulsividade"],
  ["riskAversion", "Aversão a risco"],
];

const ACTIONS = ["Comer", "Dormir", "Trabalhar", "Socializar", "Viajar", "Ficar ocioso", "Comprar"];

const PERSONALITY_PRESETS: Record<string, Partial<PersonalityValues>> = {
  Raivoso: { agreeableness: 10, emotionalStability: 10, impulsivity: 90 },
  Calmo: { agreeableness: 70, emotionalStability: 90, impulsivity: 15 },
  Sociável: { extroversion: 90, agreeableness: 80, altruism: 70 },
  Ambicioso: { ambition: 95, conscientiousness: 80, riskAversion: 25 },
};

function personalityOf(value: unknown): PersonalityValues {
  const source = typeof value === "object" && value !== null ? value as Record<string, unknown> : {};
  return Object.fromEntries(TRAITS.map(([key]) => [key, Number(source[key] ?? 50)])) as unknown as PersonalityValues;
}

export function NpcAuthoringControls({
  npcId, source, powerIds, personality, location, onRefresh,
}: {
  npcId: number;
  source: AuthoringSource;
  powerIds: string[];
  personality: unknown;
  location: { x: number; y: number };
  onRefresh: () => Promise<void>;
}) {
  const [catalog, setCatalog] = useState<PowerCatalogItem[]>([]);
  const [selectedPower, setSelectedPower] = useState("");
  const [targetNpcId, setTargetNpcId] = useState(npcId);
  const [targetX, setTargetX] = useState(location.x + 1);
  const [targetY, setTargetY] = useState(location.y);
  const [otherNpcId, setOtherNpcId] = useState(npcId);
  const [action, setAction] = useState(5);
  const [traits, setTraits] = useState(() => personalityOf(personality));
  const [status, setStatus] = useState("");
  const [busy, setBusy] = useState(false);

  useEffect(() => {
    let cancelled = false;
    void source.powerCatalog().then((items) => {
      if (!cancelled) {
        setCatalog(items);
        setSelectedPower((current) => current || items[0]?.id || "");
      }
    }).catch((error: unknown) => !cancelled && setStatus(error instanceof Error ? error.message : "Catálogo indisponível"));
    return () => { cancelled = true; };
  }, [source]);

  useEffect(() => setTraits(personalityOf(personality)), [personality]);
  const selected = useMemo(() => catalog.find((item) => item.id === selectedPower), [catalog, selectedPower]);

  async function run(label: string, command: () => Promise<void>) {
    setBusy(true);
    setStatus("");
    try {
      await command();
      await onRefresh();
      setStatus(`${label} concluído.`);
    } catch (error) {
      setStatus(error instanceof Error ? error.message : `${label} recusado.`);
    } finally {
      setBusy(false);
    }
  }

  return (
    <section aria-labelledby="npc-authoring-title" className="npc-authoring-controls">
      <h4 id="npc-authoring-title">Intervenções de autoria</h4>
      <p className="approximate-note">Comandos explícitos: o motor valida e registra; a rotina autônoma continua depois.</p>
      {status && <p role="status">{status}</p>}

      <details open>
        <summary>Poderes e constructos</summary>
        {catalog.length === 0 ? <p>Nenhum poder configurado neste mundo.</p> : (
          <>
            <label>Poder:{" "}<select aria-label="Poder para autoria" value={selectedPower} onChange={(e) => setSelectedPower(e.target.value)}>
              {catalog.map((item) => <option key={item.id} value={item.id}>{item.id}</option>)}
            </select></label>
            {selected && <p><small>{selected.source} · {selected.mode} · {selected.effects.join(", ")}</small></p>}
            {selected?.reliability === "ResolutionCheck" && <p><small>O motor fará a resolução determinística.</small></p>}
            {!powerIds.includes(selectedPower) ? (
              <button type="button" disabled={busy || !selectedPower} onClick={() => run("Concessão", () => source.grantPower(npcId, selectedPower))}>Conceder poder</button>
            ) : (
              <button type="button" disabled={busy} onClick={() => run("Revogação", () => source.revokePower(npcId, selectedPower))}>Revogar poder</button>
            )}
            <div>
              <label>NPC alvo: <input aria-label="NPC alvo do poder" type="number" value={targetNpcId} onChange={(e) => setTargetNpcId(Number(e.target.value))} /></label>
              <button type="button" disabled={busy || !powerIds.includes(selectedPower) || selected?.mode === "Passive" || selected?.mode === "Triggered"} onClick={() => run("Invocação", () => source.invokePower(npcId, selectedPower, targetNpcId, undefined))}>Usar no NPC</button>
            </div>
            <div>
              <label>X: <input aria-label="Célula X do constructo" type="number" value={targetX} onChange={(e) => setTargetX(Number(e.target.value))} /></label>
              <label>Y: <input aria-label="Célula Y do constructo" type="number" value={targetY} onChange={(e) => setTargetY(Number(e.target.value))} /></label>
              <button type="button" disabled={busy || !powerIds.includes(selectedPower) || selected?.mode === "Passive" || selected?.mode === "Triggered"} onClick={() => run("Criação no mapa", () => source.invokePower(npcId, selectedPower, npcId, { x: targetX, y: targetY }))}>Criar/usar na célula</button>
            </div>
          </>
        )}
      </details>

      <details>
        <summary>Personalidade</summary>
        <div aria-label="Perfis rápidos de personalidade">
          {Object.entries(PERSONALITY_PRESETS).map(([name, patch]) => (
            <button key={name} type="button" onClick={() => setTraits({ ...traits, ...patch })}>{name}</button>
          ))}
        </div>
        {TRAITS.map(([key, label]) => <label key={key}>{label}: <input aria-label={label} type="range" min="0" max="100" value={traits[key]} onChange={(e) => setTraits({ ...traits, [key]: Number(e.target.value) })} /> {traits[key]}</label>)}
        <button type="button" disabled={busy} onClick={() => run("Personalidade", () => source.rewritePersonality(npcId, traits))}>Salvar personalidade</button>
      </details>

      <details>
        <summary>Relações e comportamento</summary>
        <label>Outro NPC: <input aria-label="Outro NPC da relação" type="number" value={otherNpcId} onChange={(e) => setOtherNpcId(Number(e.target.value))} /></label>
        <button type="button" disabled={busy || otherNpcId === npcId} onClick={() => run("Rompimento", () => source.breakRelationships(npcId, otherNpcId))}>Romper relação entre os dois</button>
        <label>Ação imediata:{" "}<select aria-label="Ação forçada" value={action} onChange={(e) => setAction(Number(e.target.value))}>{ACTIONS.map((label, value) => <option key={label} value={value}>{label}</option>)}</select></label>
        <button type="button" disabled={busy} onClick={() => run("Ordem", () => source.forceAction(npcId, action))}>Dar ordem agora</button>
      </details>
    </section>
  );
}
