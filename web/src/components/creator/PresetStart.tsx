import { useEffect, useState } from "react";
import { fetchPeriodTemplate, listPeriodTemplates, type PeriodSummary } from "../../api";
import { defaultScenarioForm, jsonToScenarioForm, type ScenarioFormState } from "../../scenarioDefaults";
import { creatorGroundAt } from "./creatorWorldVisuals";

export interface PresetStartProps {
  onStart: (form: ScenarioFormState, name: string, periodId?: string) => void;
  onBack: () => void;
}

type SizePresetKey = "pequeno" | "medio" | "grande";

interface SizePreset {
  label: string;
  width: number;
  height: number;
  initialPopulation: number;
}

const SIZE_PRESETS: Record<SizePresetKey, SizePreset> = {
  pequeno: { label: "Pequeno", width: 10, height: 10, initialPopulation: 20 },
  medio: { label: "Médio", width: 20, height: 20, initialPopulation: 60 },
  grande: { label: "Grande", width: 40, height: 40, initialPopulation: 150 },
};

const BLANK = "__blank__";
const PREVIEW_SCALE: Record<SizePresetKey, number> = { pequeno: 0.52, medio: 0.72, grande: 0.94 };

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
            {Object.entries(SIZE_PRESETS).map(([key, preset]) => (
              <button key={key} type="button" className={size === key ? "selected" : ""} aria-pressed={size === key} disabled={startingPoint !== BLANK} onClick={() => setSize(key as SizePresetKey)}>
                <span className="size-preset-map" aria-hidden="true">{key === "pequeno" ? "▦" : key === "medio" ? "▦▦" : "▦▦▦"}</span>
                <strong>{preset.label}</strong>
                <small>{preset.width}×{preset.height} · {preset.initialPopulation} habitantes</small>
              </button>
            ))}
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
                <span aria-hidden="true">◫</span><strong>{template.source}</strong><small>Cenário preparado</small>
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
              key={size}
              className="preview-map-world"
              data-testid="preview-map-world"
              style={{
                transform: `scale(${PREVIEW_SCALE[size]})`,
                aspectRatio: `${SIZE_PRESETS[size].width} / ${SIZE_PRESETS[size].height}`,
                gridTemplateColumns: `repeat(${SIZE_PRESETS[size].width}, 1fr)`,
              }}
            >
              {Array.from({ length: SIZE_PRESETS[size].width * SIZE_PRESETS[size].height }, (_, index) => {
                const x = index % SIZE_PRESETS[size].width;
                const y = Math.floor(index / SIZE_PRESETS[size].width);
                const ground = creatorGroundAt(seed, x, y);
                return <i key={index} data-ground={ground.kind} style={{ background: ground.color }} />;
              })}
              <b style={{ left: `${(5.5 / SIZE_PRESETS[size].width) * 100}%`, top: `${(5.5 / SIZE_PRESETS[size].height) * 100}%` }}>⌂</b>
            </div>
          </div>
          <span>Prévia conceitual</span>
          <h3>{name.trim() || "Mundo sem nome"}</h3>
          <dl>
            <div><dt>Território</dt><dd>{SIZE_PRESETS[size].width} × {SIZE_PRESETS[size].height}</dd></div>
            <div><dt>População</dt><dd>{SIZE_PRESETS[size].initialPopulation}</dd></div>
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
