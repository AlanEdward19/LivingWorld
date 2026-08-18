import { useEffect, useState } from "react";
import { fetchPeriodTemplate, listPeriodTemplates, type PeriodSummary } from "../../api";
import { defaultScenarioForm, jsonToScenarioForm, type ScenarioFormState } from "../../scenarioDefaults";
import { creatorGroundAt } from "./creatorWorldVisuals";

export interface PresetStartProps {
  onStart: (form: ScenarioFormState, name: string, periodId?: string) => void;
  onBack: () => void;
}

type SizePresetKey = "pequeno" | "medio" | "grande" | "imenso";

interface SizePreset {
  label: string;
  width: number;
  height: number;
  initialPopulation: number;
}

// Feedback do usuário (T44b): 10x10 ("Pequeno" original) deixava pouco espaço pra uma vila de
// verdade — footprint de cidade vai até 12x12 (CityBoundsResolver), então uma vila nesse mapa
// dominava o território. Presets sobem o mínimo e ganham um degrau extra.
const SIZE_PRESETS: Record<SizePresetKey, SizePreset> = {
  pequeno: { label: "Pequeno", width: 16, height: 16, initialPopulation: 30 },
  medio: { label: "Médio", width: 30, height: 30, initialPopulation: 80 },
  grande: { label: "Grande", width: 50, height: 50, initialPopulation: 180 },
  imenso: { label: "Imenso", width: 80, height: 80, initialPopulation: 400 },
};
const SIZE_PRESET_KEYS = Object.keys(SIZE_PRESETS) as SizePresetKey[];

const BLANK = "__blank__";
const PREVIEW_SCALE: Record<SizePresetKey, number> = { pequeno: 0.46, medio: 0.62, grande: 0.78, imenso: 0.94 };

/// Fase 15.1, T23: primeira tela do creator — no máximo 4 campos, nenhum parâmetro avançado. A
/// autoria campo-a-campo completa continua existindo no wizard de 6 abas (T26 evolui a
/// apresentação dele depois); esta tela só decide o ponto de partida.
///
/// `nome` é rótulo de sessão local, exibido pelo cliente enquanto o mundo está aberto — o
/// domínio não tem conceito de "nome de mundo" (`WorldCreateEndpoints.cs` não recebe esse campo),
/// então ele nunca entra no `ScenarioJson` submetido, só sobe pro `App` via `onStart`.
export function PresetStart({ onStart, onBack }: PresetStartProps) {
  const [name, setName] = useState("");
  const nameIsValid = name.trim().length > 0;
  const [seed, setSeed] = useState(1);
  const [size, setSize] = useState<SizePresetKey>("medio");
  const [startingPoint, setStartingPoint] = useState<string>(BLANK);
  const [templates, setTemplates] = useState<PeriodSummary[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    listPeriodTemplates()
      .then(setTemplates)
      .catch(() => setTemplates([]));
  }, []);

  // T44b: prévia (tamanho + animação) segue o template real quando um está selecionado, não só
  // o preset manual — trocar de template antes disso deixava a prévia congelada no último
  // tamanho escolhido, mesmo o template gerando um mundo bem diferente.
  const selectedTemplate = startingPoint === BLANK ? null : templates.find((t) => t.periodId === startingPoint);
  const previewDims = selectedTemplate ?? SIZE_PRESETS[size];
  const previewKey = selectedTemplate ? selectedTemplate.periodId : size;
  const previewScale = selectedTemplate
    ? Math.min(0.94, Math.max(0.4, Math.max(selectedTemplate.width, selectedTemplate.height) / 90))
    : PREVIEW_SCALE[size];
  const previewPopulation = selectedTemplate ? undefined : SIZE_PRESETS[size].initialPopulation;

  async function handleCreate() {
    setLoading(true);
    setError(null);
    try {
      let form: ScenarioFormState;
      if (startingPoint === BLANK) {
        const preset = SIZE_PRESETS[size];
        form = {
          ...defaultScenarioForm(),
          width: preset.width,
          height: preset.height,
          initialPopulation: preset.initialPopulation,
        };
      } else {
        form = jsonToScenarioForm(await fetchPeriodTemplate(startingPoint));
      }
      onStart({ ...form, seed }, name, startingPoint === BLANK ? undefined : startingPoint);
    } catch (err) {
      setError(String(err));
    } finally {
      setLoading(false);
    }
  }

  return (
    <div className="preset-start" data-testid="preset-start">
      <header className="preset-start-heading">
        <button type="button" className="preset-start-back" onClick={onBack}>
          ← Voltar
        </button>
        <span>Novo jogo</span>
        <h2>Que mundo vai nascer?</h2>
        <p>Escolha a escala e veja o ponto de partida antes de entrar no mapa.</p>
      </header>

      <div className="preset-start-layout">
        <section className="preset-choices">
          <div className="preset-inline-fields">
            <label>
              Nome do mundo
              <input aria-label="preset-name" type="text" value={name} placeholder="Ex.: Vale de Aster" onChange={(e) => setName(e.target.value)} />
            </label>
            <label>
              Seed
              <input aria-label="preset-seed" type="number" value={seed} onChange={(e) => setSeed(Number(e.target.value))} />
            </label>
          </div>

          <h3>Escala da simulação</h3>
          <div className="size-preset-cards">
            {SIZE_PRESET_KEYS.map((key, index) => {
              const preset = SIZE_PRESETS[key];
              return (
                <button key={key} type="button" className={size === key ? "selected" : ""} aria-pressed={size === key} disabled={startingPoint !== BLANK} onClick={() => setSize(key)}>
                  <span className="size-preset-map" aria-hidden="true">{"▦".repeat(index + 1)}</span>
                  <strong>{preset.label}</strong>
                  <small>{preset.width}×{preset.height} · {preset.initialPopulation} habitantes</small>
                </button>
              );
            })}
          </div>
          <label className="visually-hidden">
            Tamanho aproximado
        <select
          aria-label="preset-size"
          value={size}
          onChange={(e) => setSize(e.target.value as SizePresetKey)}
          disabled={startingPoint !== BLANK}
        >
          {Object.entries(SIZE_PRESETS).map(([key, preset]) => (
            <option key={key} value={key}>
              {preset.label}
            </option>
          ))}
        </select>
          </label>

          <h3>Ponto de partida</h3>
          <div className="origin-cards">
            <button type="button" className={startingPoint === BLANK ? "selected" : ""} aria-pressed={startingPoint === BLANK} onClick={() => setStartingPoint(BLANK)}>
              <span aria-hidden="true">✦</span><strong>Folha em branco</strong><small>Construa do zero no mapa</small>
            </button>
            {templates.map((template) => (
              <button key={template.periodId} type="button" className={startingPoint === template.periodId ? "selected" : ""} aria-pressed={startingPoint === template.periodId} onClick={() => setStartingPoint(template.periodId)}>
                <span aria-hidden="true">◫</span><strong>{template.source}</strong><small>{template.width}×{template.height} · cenário preparado</small>
              </button>
            ))}
          </div>
          <label className="visually-hidden">
            Começar de
        <select
          aria-label="preset-starting-point"
          value={startingPoint}
          onChange={(e) => setStartingPoint(e.target.value)}
        >
          <option value={BLANK}>novo mundo em branco</option>
          {templates.map((t) => (
            <option key={t.periodId} value={t.periodId}>
              {t.source}
            </option>
          ))}
        </select>
          </label>
        </section>

        <aside className="world-seed-preview" aria-label="Prévia do mundo">
          <div className="preview-map" aria-hidden="true">
            <div
              key={previewKey}
              className="preview-map-world"
              data-testid="preview-map-world"
              style={{
                transform: `scale(${previewScale})`,
                aspectRatio: `${previewDims.width} / ${previewDims.height}`,
                gridTemplateColumns: `repeat(${previewDims.width}, 1fr)`,
              }}
            >
              {Array.from({ length: previewDims.width * previewDims.height }, (_, index) => {
                const x = index % previewDims.width;
                const y = Math.floor(index / previewDims.width);
                const ground = creatorGroundAt(seed, x, y);
                return <i key={index} data-ground={ground.kind} style={{ background: ground.color }} />;
              })}
              <b style={{ left: `${(5.5 / previewDims.width) * 100}%`, top: `${(5.5 / previewDims.height) * 100}%` }}>⌂</b>
            </div>
          </div>
          <span>Prévia conceitual</span>
          <h3>{name.trim() || "Mundo sem nome"}</h3>
          <dl>
            <div><dt>Território</dt><dd>{previewDims.width} × {previewDims.height}</dd></div>
            <div><dt>População</dt><dd>{previewPopulation ?? "definida pelo cenário"}</dd></div>
            <div><dt>Seed</dt><dd>{seed}</dd></div>
          </dl>
          <button className="create-world-cta" type="button" onClick={handleCreate} disabled={loading || !nameIsValid}>
            {loading ? "Abrindo o mapa…" : "Começar"}
          </button>
          {!nameIsValid && <p role="alert">Dê um nome ao mundo para continuar.</p>}
          {error && <p role="alert">{error}</p>}
        </aside>
      </div>
    </div>
  );
}
