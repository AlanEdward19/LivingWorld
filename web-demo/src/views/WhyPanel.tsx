export interface WhyFactor {
  text: string;
  linkedEventId?: string;
}

export interface WhyPanelProps {
  factors: WhyFactor[];
  onFactorClick: (eventId: string) => void;
}

/**
 * Painel de motivos em linguagem humana (doc#114) — "household food is low", "grain prices
 * rose", "she is hungry". Fatores com `linkedEventId` são clicáveis e abrem o Causal Explorer
 * nesse evento (spec P1 AC5-6).
 */
export function WhyPanel({ factors, onFactorClick }: WhyPanelProps) {
  return (
    <ul data-testid="why-panel">
      {factors.map((factor, index) => (
        <li key={index}>
          {factor.linkedEventId ? (
            <button type="button" onClick={() => onFactorClick(factor.linkedEventId!)}>
              {factor.text}
            </button>
          ) : (
            <span>{factor.text}</span>
          )}
        </li>
      ))}
    </ul>
  );
}
