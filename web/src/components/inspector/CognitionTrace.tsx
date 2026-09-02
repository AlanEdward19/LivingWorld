import { useEffect, useState } from "react";
import type { NpcInspection } from "../../data/contracts";
import { ACTION_LABELS } from "../../map-engine/actionVisuals";

export interface CognitionPressure {
  kind: string;
  intensity: number;
  factors: string[];
}

export interface CognitionOpportunity {
  kind: string;
  attractiveness: number;
  detail?: string | null;
}

export interface DecisionTraceData {
  wakeReason: number;
  previousIntent: number | null;
  topPressures: CognitionPressure[];
  knownOpportunities: CognitionOpportunity[];
  winner: number;
  winningUtility: number;
  topPositiveFactors: string[];
  topNegativeFactors: string[];
  blockingFactors: string[];
  knownAlternatives: number[];
}

export interface CognitionTraceEntry {
  tick: number;
  trace: DecisionTraceData;
}

export type NpcInspectionWithCognition = NpcInspection & {
  cognitionTrace?: CognitionTraceEntry[];
};

export function cognitionTraceOf(inspection: NpcInspection): CognitionTraceEntry[] {
  return (inspection as NpcInspectionWithCognition).cognitionTrace ?? [];
}

const WAKE_REASON_LABELS: Record<number, string> = {
  0: "Desconhecido",
  1: "Necessidade urgente",
  2: "Ação concluída",
  3: "Evento roteado",
  4: "Agendado",
};

function actionLabel(actionId: number | null): string {
  if (actionId === null) return "—";
  return ACTION_LABELS[actionId] ?? `Atividade ${actionId}`;
}

function wakeReasonLabel(reason: number): string {
  return WAKE_REASON_LABELS[reason] ?? `motivo ${reason}`;
}

function pressuresSummary(pressures: CognitionPressure[]): string {
  if (pressures.length === 0) return "—";
  return pressures.map((pressure) => `${pressure.kind} (${pressure.intensity})`).join(", ");
}

function opportunitiesSummary(opportunities: CognitionOpportunity[]): string {
  if (opportunities.length === 0) return "—";
  return opportunities
    .map((opportunity) => `${opportunity.kind} (${opportunity.attractiveness})`)
    .join(", ");
}

function factorsSummary(factors: string[]): string {
  if (factors.length === 0) return "—";
  return factors.join(", ");
}

function alternativesSummary(alternatives: number[]): string {
  if (alternatives.length === 0) return "—";
  return alternatives.map((id) => actionLabel(id)).join(", ");
}

export interface CognitionTraceProps {
  entries: CognitionTraceEntry[];
}

export function CognitionTrace({ entries }: CognitionTraceProps) {
  const [selectedIndex, setSelectedIndex] = useState(() => Math.max(0, entries.length - 1));

  useEffect(() => {
    setSelectedIndex(Math.max(0, entries.length - 1));
  }, [entries]);

  if (entries.length === 0) {
    return <p role="status">sem rastro — fora de observação</p>;
  }

  const selected = entries[selectedIndex];
  const trace = selected.trace;

  return (
    <div className="cognition-trace">
      <ol className="cognition-trace-timeline" aria-label="Linha do tempo de decisões">
        {entries.map((entry, index) => (
          <li key={entry.tick}>
            <button
              type="button"
              className={index === selectedIndex ? "cognition-trace-timeline-active" : undefined}
              aria-current={index === selectedIndex ? "step" : undefined}
              aria-label={`Tick ${entry.tick}`}
              onClick={() => setSelectedIndex(index)}
            >
              <time dateTime={`tick-${entry.tick}`}>Tick {entry.tick}</time>
            </button>
          </li>
        ))}
      </ol>

      <section className="cognition-trace-flow" aria-label="Fluxo visual de decisão">
        <nav className="cognition-trace-flow-nav" aria-label="Navegação entre decisões">
          <button
            type="button"
            aria-label="Decisão anterior"
            disabled={selectedIndex === 0}
            onClick={() => setSelectedIndex((index) => index - 1)}
          >
            Anterior
          </button>
          <span aria-live="polite">
            Decisão {selectedIndex + 1} de {entries.length} — tick {selected.tick}
          </span>
          <button
            type="button"
            aria-label="Próxima decisão"
            disabled={selectedIndex === entries.length - 1}
            onClick={() => setSelectedIndex((index) => index + 1)}
          >
            Próxima
          </button>
        </nav>

        <ol className="cognition-flow-steps">
          <li className="cognition-flow-stimulus" aria-label="Estímulo">
            <h5>Estímulo</h5>
            <dl>
              <div>
                <dt>Motivo de despertar</dt>
                <dd>{wakeReasonLabel(trace.wakeReason)}</dd>
              </div>
              <div>
                <dt>Intenção anterior</dt>
                <dd>{actionLabel(trace.previousIntent)}</dd>
              </div>
              <div>
                <dt>Pressões</dt>
                <dd>{pressuresSummary(trace.topPressures)}</dd>
              </div>
              <div>
                <dt>Oportunidades</dt>
                <dd>{opportunitiesSummary(trace.knownOpportunities)}</dd>
              </div>
            </dl>
          </li>

          <li className="cognition-flow-ponderation" aria-label="Ponderação">
            <h5>Ponderação</h5>
            <dl>
              <div>
                <dt>Fatores positivos</dt>
                <dd>{factorsSummary(trace.topPositiveFactors)}</dd>
              </div>
              <div>
                <dt>Fatores negativos</dt>
                <dd>{factorsSummary(trace.topNegativeFactors)}</dd>
              </div>
              <div>
                <dt>Bloqueios</dt>
                <dd>{factorsSummary(trace.blockingFactors)}</dd>
              </div>
              <div>
                <dt>Alternativas</dt>
                <dd>{alternativesSummary(trace.knownAlternatives)}</dd>
              </div>
            </dl>
          </li>

          <li className="cognition-flow-decision" aria-label="Decisão">
            <h5>Decisão</h5>
            <dl>
              <div>
                <dt>Ação escolhida</dt>
                <dd>{actionLabel(trace.winner)}</dd>
              </div>
              <div>
                <dt>Utilidade</dt>
                <dd>{trace.winningUtility}</dd>
              </div>
            </dl>
          </li>
        </ol>
      </section>

      <table className="cognition-trace-table">
        <caption>Rastro de decisão</caption>
        <thead>
          <tr>
            <th scope="col">Tick</th>
            <th scope="col">Motivo</th>
            <th scope="col">Intenção anterior</th>
            <th scope="col">Decisão</th>
            <th scope="col">Utilidade</th>
            <th scope="col">Pressões</th>
          </tr>
        </thead>
        <tbody>
          {entries.map((entry) => (
            <tr key={entry.tick}>
              <td>{entry.tick}</td>
              <td>{wakeReasonLabel(entry.trace.wakeReason)}</td>
              <td>{actionLabel(entry.trace.previousIntent)}</td>
              <td>{actionLabel(entry.trace.winner)}</td>
              <td>{entry.trace.winningUtility}</td>
              <td>{pressuresSummary(entry.trace.topPressures)}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
