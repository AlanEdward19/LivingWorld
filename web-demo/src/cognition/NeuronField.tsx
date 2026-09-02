import type { DecisionTrace } from "./types";

export interface NeuronFieldProps {
  trace: DecisionTrace;
  /** Bumps the pulse animation key — sandbox live mode re-triggers it per incoming tick, the
   * sidebar's static replay only needs it once per selected entry (both just change `key`). */
  pulseKey: number | string;
}

const WIDTH = 460;
const HEIGHT = 220;
const INPUT_X = 60;
const HIDDEN_X = 230;
const OUTPUT_X = 400;

function layoutY(index: number, count: number): number {
  if (count <= 1) return HEIGHT / 2;
  const gap = (HEIGHT - 40) / (count - 1);
  return 20 + index * gap;
}

/** SVG-only "neurons lighting up" view of a single decision: pressures/opportunities as input
 * nodes (radius by intensity/attractiveness), positive/negative/blocking factors as hidden nodes,
 * the winner as the single output node. No canvas lib — this is a sidebar/drawer widget, not the
 * map (pixi.js stays reserved for that, see plan). */
export function NeuronField({ trace, pulseKey }: NeuronFieldProps) {
  const inputs = [
    ...trace.topPressures.map((p) => ({ label: p.kind, weight: p.intensity, tone: "pressure" as const })),
    ...trace.knownOpportunities.map((o) => ({ label: o.kind, weight: o.attractiveness, tone: "opportunity" as const })),
  ];
  const hidden = [
    ...trace.topPositiveFactors.map((label) => ({ label, tone: "positive" as const })),
    ...trace.topNegativeFactors.map((label) => ({ label, tone: "negative" as const })),
    ...trace.blockingFactors.map((label) => ({ label, tone: "blocking" as const })),
  ];

  const inputPoints = inputs.map((node, index) => ({ ...node, x: INPUT_X, y: layoutY(index, inputs.length) }));
  const hiddenPoints = hidden.map((node, index) => ({ ...node, x: HIDDEN_X, y: layoutY(index, hidden.length) }));
  const outputPoint = { x: OUTPUT_X, y: HEIGHT / 2 };

  return (
    <svg
      key={pulseKey}
      data-testid="neuron-field"
      viewBox={`0 0 ${WIDTH} ${HEIGHT}`}
      role="img"
      aria-label={`Decision network for ${trace.winner}`}
    >
      <g className="neuron-edges">
        {inputPoints.map((from) =>
          hiddenPoints.map((to) => (
            <line
              key={`${from.label}-${to.label}`}
              x1={from.x}
              y1={from.y}
              x2={to.x}
              y2={to.y}
              className={`neuron-edge neuron-edge--${to.tone}`}
            />
          )),
        )}
        {hiddenPoints.map((from) => (
          <line
            key={`${from.label}-out`}
            x1={from.x}
            y1={from.y}
            x2={outputPoint.x}
            y2={outputPoint.y}
            className={`neuron-edge neuron-edge--${from.tone}`}
          />
        ))}
        {hiddenPoints.length === 0 &&
          inputPoints.map((from) => (
            <line
              key={`${from.label}-out-direct`}
              x1={from.x}
              y1={from.y}
              x2={outputPoint.x}
              y2={outputPoint.y}
              className="neuron-edge neuron-edge--neutral"
            />
          ))}
      </g>

      <g className="neuron-nodes">
        {inputPoints.map((node) => (
          <g key={node.label} className={`neuron-node neuron-node--${node.tone}`} transform={`translate(${node.x}, ${node.y})`}>
            <circle r={6 + node.weight * 10} className="neuron-node-glow" />
            <circle r={4 + node.weight * 6} className="neuron-node-core" />
            <text x={-14} y={4} textAnchor="end" className="neuron-node-label">
              {node.label}
            </text>
          </g>
        ))}

        {hiddenPoints.map((node) => (
          <g key={node.label} className={`neuron-node neuron-node--${node.tone}`} transform={`translate(${node.x}, ${node.y})`}>
            <circle r={9} className="neuron-node-glow" />
            <circle r={5} className="neuron-node-core" />
          </g>
        ))}

        <g className="neuron-node neuron-node--winner" transform={`translate(${outputPoint.x}, ${outputPoint.y})`}>
          {/* Full winner name already shown in the "Decision" pipeline stage above — a text
           * label here would overflow past the drawer's right edge for long action names, so
           * this node stays a glowing marker with an accessible <title> instead. */}
          <title>{trace.winner}</title>
          <circle r={16} className="neuron-node-glow" />
          <circle r={9} className="neuron-node-core" />
        </g>
      </g>
    </svg>
  );
}
