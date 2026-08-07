import { useEffect, useState } from "react";
import { fetchPeriodTemplate, listPeriodTemplates, type PeriodSummary } from "../../api";
import { defaultScenarioForm, jsonToScenarioForm, type ScenarioFormState } from "../../scenarioDefaults";

export interface PresetStartProps {
  onStart: (form: ScenarioFormState, name: string) => void;
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

/// Fase 15.1, T23: primeira tela do creator — no máximo 4 campos, nenhum parâmetro avançado. A
/// autoria campo-a-campo completa continua existindo no wizard de 6 abas (T26 evolui a
/// apresentação dele depois); esta tela só decide o ponto de partida.
///
/// `nome` é rótulo de sessão local, exibido pelo cliente enquanto o mundo está aberto — o
/// domínio não tem conceito de "nome de mundo" (`WorldCreateEndpoints.cs` não recebe esse campo),
/// então ele nunca entra no `ScenarioJson` submetido, só sobe pro `App` via `onStart`.
export function PresetStart({ onStart }: PresetStartProps) {
  const [name, setName] = useState("");
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
      onStart({ ...form, seed }, name);
    } catch (err) {
      setError(String(err));
    } finally {
      setLoading(false);
    }
  }

  return (
    <div className="preset-start" data-testid="preset-start">
      <h2>Criar mundo</h2>

      <label>
        Nome:{" "}
        <input aria-label="preset-name" type="text" value={name} onChange={(e) => setName(e.target.value)} />
      </label>

      <label>
        Seed:{" "}
        <input
          aria-label="preset-seed"
          type="number"
          value={seed}
          onChange={(e) => setSeed(Number(e.target.value))}
        />
      </label>

      <label>
        Tamanho aproximado:{" "}
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

      <label>
        Começar de:{" "}
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

      <button type="button" onClick={handleCreate} disabled={loading}>
        {loading ? "Carregando…" : "Criar"}
      </button>
      {error && <p role="alert">{error}</p>}
    </div>
  );
}
