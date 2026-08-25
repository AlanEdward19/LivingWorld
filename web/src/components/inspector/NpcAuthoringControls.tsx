import { useEffect, useMemo, useRef, useState } from "react";
import type { AuthoringSource, PersonalityValues, PowerCatalogItem } from "../../data/sources";

type AdminSection = "powers" | "personality" | "behavior";

const ADMIN_SECTIONS: ReadonlyArray<{ id: AdminSection; icon: string; label: string }> = [
  { id: "powers", icon: "✧", label: "Poderes" },
  { id: "personality", icon: "☺", label: "Personalidade" },
  { id: "behavior", icon: "⚔", label: "Comportamento" },
];

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
  const [section, setSection] = useState<AdminSection>("powers");
  const [catalog, setCatalog] = useState<PowerCatalogItem[]>([]);
  const [selectedPower, setSelectedPower] = useState("");
  const [targetNpcId, setTargetNpcId] = useState(npcId);
  const [targetX, setTargetX] = useState(location.x + 1);
  const [targetY, setTargetY] = useState(location.y);
  const [action, setAction] = useState(5);
  const [traits, setTraits] = useState(() => personalityOf(personality));
  const [status, setStatus] = useState("");
  const [busy, setBusy] = useState(false);
  const [justAppliedPreset, setJustAppliedPreset] = useState<string | null>(null);
  const presetFlashTimeout = useRef<number | undefined>(undefined);

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
  useEffect(() => () => window.clearTimeout(presetFlashTimeout.current), []);
  const selected = useMemo(() => catalog.find((item) => item.id === selectedPower), [catalog, selectedPower]);

  function applyPreset(name: string, patch: Partial<PersonalityValues>) {
    setTraits((current) => ({ ...current, ...patch }));
    setJustAppliedPreset(name);
    setStatus(`Perfil "${name}" aplicado — clique em Salvar personalidade pra confirmar.`);
    window.clearTimeout(presetFlashTimeout.current);
    presetFlashTimeout.current = window.setTimeout(() => setJustAppliedPreset(null), 900);
  }

  function isActivePreset(patch: Partial<PersonalityValues>): boolean {
    return (Object.keys(patch) as Array<keyof PersonalityValues>).every((key) => traits[key] === patch[key]);
  }

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

  const powerUsable = powerIds.includes(selectedPower) && selected?.mode !== "Passive" && selected?.mode !== "Triggered";

  return (
    <section aria-labelledby="npc-authoring-title" className="npc-authoring-controls">
      <header className="npc-authoring-header">
        <h4 id="npc-authoring-title">⚡ Comandar NPC</h4>
        <p className="approximate-note">Comandos explícitos: o motor valida e registra; a rotina autônoma continua depois.</p>
      </header>

      <nav className="npc-admin-sections" aria-label="Seções de comando">
        {ADMIN_SECTIONS.map((s) => (
          <button key={s.id} type="button" aria-pressed={section === s.id} onClick={() => setSection(s.id)}>
            <span aria-hidden="true">{s.icon}</span> {s.label}
          </button>
        ))}
      </nav>

      {status && <p role="status" className="npc-authoring-status">{status}</p>}

      {section === "powers" && (
        <div className="npc-command-card">
          {catalog.length === 0 ? <p className="npc-command-empty">Nenhum poder configurado neste mundo.</p> : (
            <div className="npc-command-body">
              <label className="npc-field">
                <span>Poder</span>
                <select aria-label="Poder para autoria" value={selectedPower} onChange={(e) => setSelectedPower(e.target.value)}>
                  {catalog.map((item) => <option key={item.id} value={item.id}>{item.id}</option>)}
                </select>
              </label>
              {selected && (
                <p className="npc-power-summary">
                  <small>{selected.source} · {selected.mode} · {selected.effects.join(", ")}</small>
                  {selected.reliability === "ResolutionCheck" && <small>O motor fará a resolução determinística.</small>}
                </p>
              )}
              <div className="npc-command-actions">
                {!powerIds.includes(selectedPower) ? (
                  <button type="button" className="ui-btn ui-btn--primary" disabled={busy || !selectedPower} onClick={() => run("Concessão", () => source.grantPower(npcId, selectedPower))}>Conceder poder</button>
                ) : (
                  <button type="button" className="ui-btn ui-btn--danger" disabled={busy} onClick={() => run("Revogação", () => source.revokePower(npcId, selectedPower))}>Revogar poder</button>
                )}
              </div>

              <div className="npc-field-row">
                <label className="npc-field npc-field--compact">
                  <span>NPC alvo</span>
                  <input aria-label="NPC alvo do poder" type="number" value={targetNpcId} onChange={(e) => setTargetNpcId(Number(e.target.value))} />
                </label>
                <button type="button" className="ui-btn" disabled={busy || !powerUsable} onClick={() => run("Invocação", () => source.invokePower(npcId, selectedPower, targetNpcId, undefined))}>Usar no NPC</button>
              </div>

              <div className="npc-field-row">
                <label className="npc-field npc-field--compact">
                  <span>X</span>
                  <input aria-label="Célula X do constructo" type="number" value={targetX} onChange={(e) => setTargetX(Number(e.target.value))} />
                </label>
                <label className="npc-field npc-field--compact">
                  <span>Y</span>
                  <input aria-label="Célula Y do constructo" type="number" value={targetY} onChange={(e) => setTargetY(Number(e.target.value))} />
                </label>
                <button type="button" className="ui-btn" disabled={busy || !powerUsable} onClick={() => run("Criação no mapa", () => source.invokePower(npcId, selectedPower, npcId, { x: targetX, y: targetY }))}>Criar/usar na célula</button>
              </div>
            </div>
          )}
        </div>
      )}

      {section === "personality" && (
        <div className="npc-command-card">
          <p className="npc-command-hint">Perfis rápidos ajustam os traços de uma vez — o efeito aparece nos sliders na hora. Depois, confirme em "Salvar personalidade".</p>
          <div className="npc-preset-chips" aria-label="Perfis rápidos de personalidade">
            {Object.entries(PERSONALITY_PRESETS).map(([name, patch]) => (
              <button
                key={name}
                type="button"
                className={`ui-btn npc-preset-chip${isActivePreset(patch) ? " npc-preset-chip--active" : " ui-btn--ghost"}${justAppliedPreset === name ? " npc-preset-chip--flash" : ""}`}
                aria-pressed={isActivePreset(patch)}
                onClick={() => applyPreset(name, patch)}
              >
                {name}
              </button>
            ))}
          </div>
          <div className="npc-trait-grid">
            {TRAITS.map(([key, label]) => (
              <label key={key} className="npc-trait">
                <span className="npc-trait-label">{label}</span>
                <input aria-label={label} type="range" min="0" max="100" value={traits[key]} onChange={(e) => setTraits({ ...traits, [key]: Number(e.target.value) })} />
                <span className="npc-trait-value">{traits[key]}</span>
              </label>
            ))}
          </div>
          <div className="npc-command-actions">
            <button type="button" className="ui-btn ui-btn--primary" disabled={busy} onClick={() => run("Personalidade", () => source.rewritePersonality(npcId, traits))}>Salvar personalidade</button>
          </div>
        </div>
      )}

      {section === "behavior" && (
        <div className="npc-command-card">
          <label className="npc-field">
            <span>Ação imediata</span>
            <select aria-label="Ação forçada" value={action} onChange={(e) => setAction(Number(e.target.value))}>
              {ACTIONS.map((label, value) => <option key={label} value={value}>{label}</option>)}
            </select>
          </label>
          <div className="npc-command-actions">
            <button type="button" className="ui-btn ui-btn--primary" disabled={busy} onClick={() => run("Ordem", () => source.forceAction(npcId, action))}>Dar ordem agora</button>
          </div>
        </div>
      )}
    </section>
  );
}
