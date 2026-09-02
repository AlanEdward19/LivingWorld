import { useEffect, useState } from "react";
import { computeMetrics } from "./metrics";
import { NeuronField } from "./NeuronField";
import { WAKE_REASON_LABELS, type CognitionTraceEntry } from "./types";

export interface CognitionPanelProps {
  entries: CognitionTraceEntry[];
  /** Sandbox mode: always tracks the newest tick and labels the pipeline as receiving live data,
   * instead of a fixed historical replay (sidebar usage). */
  live?: boolean;
}

function pressuresSummary(entry: CognitionTraceEntry): string {
  if (entry.trace.topPressures.length === 0) return "—";
  return entry.trace.topPressures.map((p) => `${p.kind} (${p.intensity.toFixed(2)})`).join(", ");
}

function opportunitiesSummary(entry: CognitionTraceEntry): string {
  if (entry.trace.knownOpportunities.length === 0) return "—";
  return entry.trace.knownOpportunities.map((o) => `${o.kind} (${o.attractiveness.toFixed(2)})`).join(", ");
}

/** The "see the brain" widget: ETL-pipeline framing (Stimulus → Weighting → Decision, same three
 * groups the real `DecisionTrace` already carries) over a `NeuronField` and a metrics strip. Used
 * both as a static historical replay (NPC sidebar, `live=false`) and a live sandbox feed
 * (`live=true`). Pure presentation — no network, no timers of its own. */
export function CognitionPanel({ entries, live = false }: CognitionPanelProps) {
  const [selectedIndex, setSelectedIndex] = useState(() => Math.max(0, entries.length - 1));

  useEffect(() => {
    if (live) {
      setSelectedIndex(Math.max(0, entries.length - 1));
      return;
    }
    setSelectedIndex((index) => Math.min(index, Math.max(0, entries.length - 1)));
  }, [entries.length, live]);

  if (entries.length === 0) {
    return (
      <div data-testid="cognition-panel-empty">
        <p>{live ? "Waiting for the first synthetic decision…" : "No decision trace recorded — out of observation."}</p>
      </div>
    );
  }

  const selected = entries[selectedIndex];
  const metrics = computeMetrics(entries);

  return (
    <div data-testid="cognition-panel">
      <ol className="cognition-timeline" aria-label="Decision ticks">
        {entries.map((entry, index) => (
          <li key={entry.tick}>
            <button
              type="button"
              className={index === selectedIndex ? "cognition-timeline-active" : undefined}
              aria-current={index === selectedIndex ? "step" : undefined}
              onClick={() => setSelectedIndex(index)}
            >
              Tick {entry.tick}
            </button>
          </li>
        ))}
      </ol>

      <div data-testid="cognition-pipeline" className="cognition-pipeline">
        <div className="pipeline-stage" data-testid="pipeline-stage" data-stage="stimulus">
          <h4>Stimulus</h4>
          <p className="pipeline-stage-meta">{WAKE_REASON_LABELS[selected.trace.wakeReason]}</p>
          <p className="pipeline-stage-detail">Pressures: {pressuresSummary(selected)}</p>
          <p className="pipeline-stage-detail">Opportunities: {opportunitiesSummary(selected)}</p>
        </div>
        <div className="pipeline-arrow" aria-hidden="true">
          <span className="pipeline-packet" />
        </div>
        <div className="pipeline-stage" data-testid="pipeline-stage" data-stage="weighting">
          <h4>Weighting</h4>
          <p className="pipeline-stage-detail">+ {selected.trace.topPositiveFactors.join(", ") || "—"}</p>
          <p className="pipeline-stage-detail">− {selected.trace.topNegativeFactors.join(", ") || "—"}</p>
          {selected.trace.blockingFactors.length > 0 && (
            <p className="pipeline-stage-detail pipeline-stage-detail--blocking">⊘ {selected.trace.blockingFactors.join(", ")}</p>
          )}
        </div>
        <div className="pipeline-arrow" aria-hidden="true">
          <span className="pipeline-packet" />
        </div>
        <div className="pipeline-stage pipeline-stage--decision" data-testid="pipeline-stage" data-stage="decision">
          <h4>Decision</h4>
          <p className="pipeline-stage-meta">{selected.trace.winner}</p>
          <p className="pipeline-stage-detail">Utility {selected.trace.winningUtility.toFixed(2)}</p>
        </div>
      </div>

      <NeuronField trace={selected.trace} pulseKey={selected.tick} />

      <div data-testid="cognition-metrics" className="cognition-metrics">
        <div className="metric-row">
          <span className="metric-row-label">Decisions observed</span>
          <span className="metric-row-value">{metrics.totalDecisions}</span>
        </div>
        <div className="metric-row">
          <span className="metric-row-label">Avg. winning utility</span>
          <span className="metric-row-value">{metrics.averageWinningUtility.toFixed(2)}</span>
        </div>
        <div className="metric-row">
          <span className="metric-row-label">Most frequent decision</span>
          <span className="metric-row-value">{metrics.topWinner ?? "—"}</span>
        </div>
        {metrics.wakeReasonBreakdown.map((entry) => (
          <div className="skill-row" key={entry.label}>
            <div className="skill-row-label">
              <span>{entry.label}</span>
              <span>{entry.count}</span>
            </div>
            <div className="skill-progress">
              <div style={{ width: `${(entry.count / metrics.totalDecisions) * 100}%` }} />
            </div>
          </div>
        ))}
      </div>
    </div>
  );
}
