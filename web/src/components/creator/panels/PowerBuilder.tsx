import { useState } from "react";
import type { ExtraordinaryDescriptorRow } from "../../../scenarioDefaults";
import {
  ACQUISITION_GRAMMAR_HINT, CONDITION_PRESETS, FAILURE_TAG_OPTIONS, NEED_OPTIONS, SOURCE_OPTIONS,
  STAT_KEYS, STAT_LABELS, TAG_PRESETS, TINT_SWATCHES, TRAIL_OPTIONS,
  parseCosts, parseEffects, parseFailureModes, serializeCosts, serializeEffects, serializeFailureModes,
  type ParsedCosts, type ParsedEffects, type ParsedFailureModes,
} from "./powerBuilderVocab";

export interface PowerBuilderProps {
  row: ExtraordinaryDescriptorRow;
  onSave: (row: ExtraordinaryDescriptorRow) => void;
  onCancel: () => void;
}

function tokensOf(csv: string): string[] {
  return csv.split(",").map((t) => t.trim()).filter(Boolean);
}

function ChipPicker({
  label, value, onChange, curated,
}: {
  label: string;
  value: string;
  onChange: (csv: string) => void;
  curated: ReadonlyArray<{ value: string; label: string }>;
}) {
  const [draftTag, setDraftTag] = useState("");
  const active = tokensOf(value);
  const custom = active.filter((tag) => !curated.some((c) => c.value === tag));

  function toggle(tag: string) {
    onChange(active.includes(tag) ? active.filter((t) => t !== tag).join(", ") : [...active, tag].join(", "));
  }

  function addCustom() {
    const tag = draftTag.trim();
    if (!tag || active.includes(tag)) return;
    onChange([...active, tag].join(", "));
    setDraftTag("");
  }

  function removeCustom(tag: string) {
    onChange(active.filter((t) => t !== tag).join(", "));
  }

  return (
    <div className="power-chip-field">
      <span className="power-field-label">{label}</span>
      <div className="power-chip-row">
        {curated.map((tag) => (
          <button
            key={tag.value}
            type="button"
            className={`ui-btn power-chip${active.includes(tag.value) ? " power-chip--active" : " ui-btn--ghost"}`}
            aria-pressed={active.includes(tag.value)}
            onClick={() => toggle(tag.value)}
          >
            {tag.label}
          </button>
        ))}
        {custom.map((tag) => (
          <button key={tag} type="button" className="ui-btn power-chip power-chip--active" onClick={() => removeCustom(tag)}>
            {tag} ✕
          </button>
        ))}
      </div>
      <div className="power-chip-add">
        <input
          aria-label={`Adicionar ${label.toLowerCase()}`}
          value={draftTag}
          placeholder="outro (personalizado)"
          onChange={(e) => setDraftTag(e.target.value)}
          onKeyDown={(e) => { if (e.key === "Enter") { e.preventDefault(); addCustom(); } }}
        />
        <button type="button" className="ui-btn ui-btn--ghost" onClick={addCustom} disabled={!draftTag.trim()}>+ Adicionar</button>
      </div>
    </div>
  );
}

function StatEffectRow({
  label, value, onChange, min, max, defaultValue,
}: {
  label: string;
  value: number | undefined;
  onChange: (value: number | undefined) => void;
  min: number;
  max: number;
  defaultValue: number;
}) {
  return (
    <label className="power-toggle-field">
      <input type="checkbox" checked={value !== undefined} onChange={(e) => onChange(e.target.checked ? defaultValue : undefined)} />
      <span>{label}</span>
      {value !== undefined && (
        <span className="power-slider-inline">
          <input aria-label={`Variação de ${label.toLowerCase()}`} type="range" min={min} max={max} value={value} onChange={(e) => onChange(Number(e.target.value))} />
          <strong>{value > 0 ? `+${value}` : value}</strong>
        </span>
      )}
    </label>
  );
}

function narrativeSummary(row: ExtraordinaryDescriptorRow, effects: ParsedEffects): string {
  const bits: string[] = [];
  if (row.source) bits.push(SOURCE_OPTIONS.find((s) => s.value === row.source)?.label ?? row.source);
  const powers: string[] = [];
  for (const key of STAT_KEYS) {
    const value = effects.stats[key];
    if (value !== undefined) powers.push(`${value > 0 ? "+" : ""}${value} de ${STAT_LABELS[key].toLowerCase()}`);
  }
  if (effects.flight) powers.push("voo");
  if (effects.speed !== null) powers.push(`${effects.speed}× velocidade`);
  if (effects.construct) powers.push("invoca constructos");
  if (powers.length) bits.push(powers.join(", "));
  if (!bits.length) return "Descreva a fonte e os efeitos pra ver o resumo aqui.";
  return bits.join(" — ");
}

export function PowerBuilder({ row, onSave, onCancel }: PowerBuilderProps) {
  const [draft, setDraft] = useState<ExtraordinaryDescriptorRow>(row);
  const [effects, setEffects] = useState<ParsedEffects>(() => parseEffects(row.effects));
  const [costs, setCosts] = useState<ParsedCosts>(() => parseCosts(row.costs));
  const [failureModes, setFailureModes] = useState<ParsedFailureModes>(() => parseFailureModes(row.failureModes));
  const [advancedOpen, setAdvancedOpen] = useState(false);
  const [rawOpen, setRawOpen] = useState(false);
  const [sourceCustomMode, setSourceCustomMode] = useState(
    () => draft.source !== "" && !SOURCE_OPTIONS.some((s) => s.value === draft.source),
  );

  const conditionIsCustom = !CONDITION_PRESETS.some((c) => c.value === draft.manifestationCondition);

  function set<K extends keyof ExtraordinaryDescriptorRow>(key: K, value: ExtraordinaryDescriptorRow[K]) {
    setDraft((current) => ({ ...current, [key]: value }));
  }

  function handleSave() {
    onSave({
      ...draft,
      effects: serializeEffects(effects),
      costs: serializeCosts(costs),
      failureModes: serializeFailureModes(failureModes),
    });
  }

  return (
    <div className="power-builder" data-testid="power-builder">
      <div className="power-builder-preview">
        <span className="power-field-label">Prévia</span>
        <p>{narrativeSummary(draft, effects)}</p>
        <button type="button" className="ui-btn ui-btn--ghost power-builder-raw-toggle" aria-expanded={rawOpen} onClick={() => setRawOpen((v) => !v)}>
          {rawOpen ? "Ocultar dados brutos" : "Ver dados brutos"}
        </button>
        {rawOpen && (
          <dl className="power-raw-data">
            <dt>effects</dt><dd>{serializeEffects(effects) || "—"}</dd>
            <dt>costs</dt><dd>{serializeCosts(costs) || "—"}</dd>
            <dt>failureModes</dt><dd>{serializeFailureModes(failureModes) || "—"}</dd>
            <dt>intrinsicVulnerabilities</dt><dd>{draft.intrinsicVulnerabilities || "—"}</dd>
            <dt>manifestations</dt><dd>{draft.manifestations || "—"}</dd>
          </dl>
        )}
      </div>

      <div className="power-builder-card">
        <h5>Identidade</h5>
        <label className="power-field">
          <span>Identificador</span>
          <input aria-label="Identificador do poder" value={draft.id} onChange={(e) => set("id", e.target.value)} />
        </label>
      </div>

      <div className="power-builder-card">
        <h5>Fonte</h5>
        <div className="power-card-grid">
          {SOURCE_OPTIONS.map((option) => (
            <button
              key={option.value}
              type="button"
              className={`ui-card power-source-card${!sourceCustomMode && draft.source === option.value ? " selected" : ""}`}
              onClick={() => { setSourceCustomMode(false); set("source", option.value); }}
            >
              <strong>{option.label}</strong>
              <small>{option.hint}</small>
            </button>
          ))}
          <button
            type="button"
            className={`ui-card power-source-card${sourceCustomMode ? " selected" : ""}`}
            onClick={() => setSourceCustomMode(true)}
          >
            <strong>Personalizada</strong>
            <small>Descreva a origem com suas próprias palavras.</small>
          </button>
        </div>
        {sourceCustomMode && (
          <label className="power-field">
            <span>Descrição da fonte</span>
            <input aria-label="Fonte personalizada" value={draft.source} onChange={(e) => set("source", e.target.value)} />
          </label>
        )}
        <label className="power-field">
          <span>Modo</span>
          <select aria-label="Modo do poder" value={draft.mode} onChange={(e) => set("mode", e.target.value)}>
            <option value="Active">Ativo (o portador aciona)</option>
            <option value="Passive">Passivo (sempre ligado)</option>
            <option value="Triggered">Disparado (por evento)</option>
            <option value="Conditional">Condicional (depende de algo)</option>
          </select>
        </label>
        <label className="power-field">
          <span>Confiabilidade</span>
          <select aria-label="Confiabilidade do poder" value={draft.reliability} onChange={(e) => set("reliability", e.target.value)}>
            <option value="Guaranteed">Garantido (sempre funciona)</option>
            <option value="ResolutionCheck">Incerto (o motor resolve na hora)</option>
          </select>
        </label>
      </div>

      <div className="power-builder-card">
        <h5>Efeitos</h5>
        <p className="power-vocab-note">
          O motor hoje só aplica estes efeitos (lista fechada — <em>teleporte, visão de calor,
          invisibilidade e outros ainda não existem</em>: exigem mudança no motor, não na interface).
        </p>
        {STAT_KEYS.map((key) => (
          <StatEffectRow
            key={key}
            label={`${STAT_LABELS[key]} do alvo`}
            value={effects.stats[key]}
            min={-30}
            max={30}
            defaultValue={10}
            onChange={(value) => setEffects({ ...effects, stats: { ...effects.stats, [key]: value } })}
          />
        ))}
        <label className="power-toggle-field">
          <input type="checkbox" checked={effects.flight} onChange={(e) => setEffects({ ...effects, flight: e.target.checked })} />
          <span>Voo</span>
        </label>
        <label className="power-toggle-field">
          <input type="checkbox" checked={effects.speed !== null} onChange={(e) => setEffects({ ...effects, speed: e.target.checked ? 2 : null })} />
          <span>Velocidade extra</span>
          {effects.speed !== null && (
            <span className="power-slider-inline">
              <input aria-label="Multiplicador de velocidade" type="range" min={1} max={5} step={0.5} value={effects.speed} onChange={(e) => setEffects({ ...effects, speed: Number(e.target.value) })} />
              <strong>{effects.speed}×</strong>
            </span>
          )}
        </label>
        <label className="power-toggle-field">
          <input
            type="checkbox"
            checked={effects.construct !== null}
            onChange={(e) => setEffects({ ...effects, construct: e.target.checked ? { dims: "2x1", costA: "40", costB: "24", color: "green" } : null })}
          />
          <span>Invoca constructos (paredes, plataformas)</span>
        </label>
        {effects.construct && (
          <div className="power-card-grid power-swatch-grid">
            {TINT_SWATCHES.slice(0, 3).map((swatch) => (
              <button
                key={swatch.value}
                type="button"
                className={`power-swatch${effects.construct?.color === swatch.value ? " power-swatch--active" : ""}`}
                style={{ background: swatch.color }}
                aria-label={`Cor do constructo: ${swatch.label}`}
                onClick={() => setEffects({ ...effects, construct: { ...effects.construct!, color: swatch.value } })}
              />
            ))}
          </div>
        )}
      </div>

      <div className="power-builder-card">
        <h5>Custo</h5>
        <p className="power-vocab-note">Custo para o portador — mesma lista fechada do motor, agora do lado de quem usa o poder.</p>
        {STAT_KEYS.map((key) => (
          <StatEffectRow
            key={key}
            label={`${STAT_LABELS[key]} do portador`}
            value={costs.stats[key]}
            min={-20}
            max={20}
            defaultValue={-10}
            onChange={(value) => setCosts({ ...costs, stats: { ...costs.stats, [key]: value } })}
          />
        ))}
        <label className="power-toggle-field">
          <input
            type="checkbox"
            checked={costs.householdResource !== null}
            onChange={(e) => setCosts({ ...costs, householdResource: e.target.checked ? { resourceId: 0, amount: -1 } : null })}
          />
          <span>Consome estoque da casa</span>
        </label>
        {costs.householdResource && (
          <div className="power-field-row">
            <label className="power-field npc-field--compact">
              <span>Recurso (id)</span>
              <input aria-label="Recurso da casa consumido" type="number" value={costs.householdResource.resourceId} onChange={(e) => setCosts({ ...costs, householdResource: { ...costs.householdResource!, resourceId: Number(e.target.value) } })} />
            </label>
            <label className="power-field npc-field--compact">
              <span>Quantidade</span>
              <input aria-label="Quantidade consumida da casa" type="number" value={costs.householdResource.amount} onChange={(e) => setCosts({ ...costs, householdResource: { ...costs.householdResource!, amount: Number(e.target.value) } })} />
            </label>
          </div>
        )}
      </div>

      <div className="power-builder-card">
        <h5>Modo de falha</h5>
        {draft.reliability !== "ResolutionCheck" ? (
          <p className="power-vocab-note">
            Só se aplica a poderes com confiabilidade "Incerto" — este está "Garantido", nunca falha.
          </p>
        ) : (
          <>
            <StatEffectRow
              label="Machuca o portador na falha"
              value={failureModes.healthPenalty ?? undefined}
              min={-30}
              max={0}
              defaultValue={-10}
              onChange={(value) => setFailureModes({ ...failureModes, healthPenalty: value ?? null })}
            />
            <p className="power-vocab-note">
              As tags abaixo só aparecem na crônica do mundo — não mudam o resultado mecânico (o motor
              só reage de verdade ao custo de saúde acima).
            </p>
            <ChipPicker
              label="O que acontece na falha (narrativo)"
              value={failureModes.tags}
              onChange={(v) => setFailureModes({ ...failureModes, tags: v })}
              curated={FAILURE_TAG_OPTIONS}
            />
          </>
        )}
      </div>

      <div className="power-builder-card">
        <h5>Riscos e manifestação</h5>
        <ChipPicker label="Vulnerabilidades" value={draft.intrinsicVulnerabilities} onChange={(v) => set("intrinsicVulnerabilities", v)} curated={TAG_PRESETS.intrinsicVulnerabilities} />
        <ChipPicker label="Manifestações visíveis" value={draft.manifestations} onChange={(v) => set("manifestations", v)} curated={TAG_PRESETS.manifestations} />
        <div className="power-field">
          <span className="power-field-label">Quando se manifesta</span>
          <div className="power-card-grid">
            {CONDITION_PRESETS.map((preset) => (
              <button
                key={preset.label}
                type="button"
                className={`ui-card power-source-card${draft.manifestationCondition === preset.value ? " selected" : ""}`}
                onClick={() => set("manifestationCondition", preset.value)}
              >
                <strong>{preset.label}</strong>
                <small>{preset.hint}</small>
              </button>
            ))}
          </div>
          {conditionIsCustom && (
            <input aria-label="Condição de manifestação personalizada" value={draft.manifestationCondition} onChange={(e) => set("manifestationCondition", e.target.value)} />
          )}
        </div>
      </div>

      <div className="power-builder-card">
        <h5>Aparência</h5>
        <span className="power-field-label">Tom de pele</span>
        <div className="power-swatch-grid">
          {TINT_SWATCHES.map((swatch) => (
            <button
              key={swatch.value}
              type="button"
              className={`power-swatch${draft.appearanceSkinTint === swatch.value ? " power-swatch--active" : ""}`}
              style={{ background: swatch.color }}
              aria-label={`Tom de pele: ${swatch.label}`}
              onClick={() => set("appearanceSkinTint", swatch.value)}
            />
          ))}
        </div>
        <label className="power-field">
          <span>ou tom personalizado</span>
          <input aria-label="Tom de pele personalizado" value={draft.appearanceSkinTint} onChange={(e) => set("appearanceSkinTint", e.target.value)} />
        </label>
        <span className="power-field-label">Rastro de movimento</span>
        <div className="power-chip-row">
          {TRAIL_OPTIONS.map((option) => (
            <button
              key={option.value}
              type="button"
              className={`ui-btn power-chip${draft.appearanceMovementTrail === option.value ? " power-chip--active" : " ui-btn--ghost"}`}
              onClick={() => set("appearanceMovementTrail", draft.appearanceMovementTrail === option.value ? "" : option.value)}
            >
              {option.label}
            </button>
          ))}
        </div>
        <label className="power-field">
          <span>ou rastro personalizado</span>
          <input aria-label="Rastro de movimento personalizado" value={draft.appearanceMovementTrail} onChange={(e) => set("appearanceMovementTrail", e.target.value)} />
        </label>
        <label className="power-field">
          <span>Escala visual ({draft.appearanceScaleMultiplier.toFixed(1)}×)</span>
          <input aria-label="Escala visual" type="range" min={0.5} max={2} step={0.1} value={draft.appearanceScaleMultiplier} onChange={(e) => set("appearanceScaleMultiplier", Number(e.target.value))} />
        </label>
      </div>

      <button type="button" className="ui-btn power-advanced-toggle" aria-expanded={advancedOpen} onClick={() => setAdvancedOpen((v) => !v)}>
        {advancedOpen ? "Ocultar ajustes avançados" : "Ajustes avançados (raros)"}
      </button>
      {advancedOpen && (
        <div className="power-builder-card">
          <label className="power-toggle-field">
            <input type="checkbox" checked={draft.needSubstitutionReplacesNeed !== ""} onChange={(e) => set("needSubstitutionReplacesNeed", e.target.checked ? "hunger" : "")} />
            <span>Substitui uma necessidade por um recurso</span>
          </label>
          {draft.needSubstitutionReplacesNeed !== "" && (
            <>
              <label className="power-field">
                <span>Necessidade substituída</span>
                <select aria-label="Necessidade substituída" value={draft.needSubstitutionReplacesNeed} onChange={(e) => set("needSubstitutionReplacesNeed", e.target.value)}>
                  {NEED_OPTIONS.map((n) => <option key={n.value} value={n.value}>{n.label}</option>)}
                </select>
              </label>
              <label className="power-field">
                <span>Recurso metabólico (id)</span>
                <input aria-label="Recurso metabólico" type="number" value={draft.needSubstitutionResourceId ?? 0} onChange={(e) => set("needSubstitutionResourceId", Number(e.target.value))} />
              </label>
              <label className="power-field">
                <span>Unidades por uso</span>
                <input aria-label="Unidades por uso" type="number" min={1} value={draft.needSubstitutionUnitsPerUse} onChange={(e) => set("needSubstitutionUnitsPerUse", Number(e.target.value))} />
              </label>
            </>
          )}
          <label className="power-field">
            <span>Ritmo de envelhecimento ({draft.senescenceRateMultiplier.toFixed(1)}×, 0 = não envelhece)</span>
            <input aria-label="Multiplicador de senescência" type="range" min={0} max={2} step={0.1} value={draft.senescenceRateMultiplier} onChange={(e) => set("senescenceRateMultiplier", Number(e.target.value))} />
          </label>
          <label className="power-field">
            <span>Regra de aquisição</span>
            <input aria-label="Regra de aquisição" placeholder="event:authoring" value={draft.acquisitionRules} onChange={(e) => set("acquisitionRules", e.target.value)} />
            <small className="power-vocab-note">
              ⚠ Ainda não conectado: nenhum sistema do motor dispara eventos de aquisição hoje
              (nascimento, trauma, item, ritual…). Preencher aqui fica salvo, mas só a aba
              Administração concede o poder de verdade por enquanto. {ACQUISITION_GRAMMAR_HINT}.
            </small>
          </label>
          {(effects.extra || costs.extra) && (
            <div className="power-field">
              <span>Tokens não reconhecidos (preservados de uma edição anterior)</span>
              {effects.extra && (
                <label className="power-field">
                  <span>Efeitos</span>
                  <input aria-label="Efeitos adicionais" value={effects.extra} onChange={(e) => setEffects({ ...effects, extra: e.target.value })} />
                </label>
              )}
              {costs.extra && (
                <label className="power-field">
                  <span>Custos</span>
                  <input aria-label="Custos adicionais" value={costs.extra} onChange={(e) => setCosts({ ...costs, extra: e.target.value })} />
                </label>
              )}
              <small className="power-vocab-note">
                Nenhum controle acima gera estes tokens — vieram de uma edição anterior (ou CSV
                colado à mão) e o motor os rejeita se não forem um dos alvos suportados. Apague o
                texto pra descartá-los, ou deixe como está pra preservar sem mudar.
              </small>
            </div>
          )}
        </div>
      )}

      <div className="power-builder-actions">
        <button type="button" className="ui-btn ui-btn--ghost" onClick={onCancel}>Cancelar</button>
        <button type="button" className="ui-btn ui-btn--primary" disabled={!draft.id.trim()} onClick={handleSave}>Salvar poder</button>
      </div>
    </div>
  );
}
