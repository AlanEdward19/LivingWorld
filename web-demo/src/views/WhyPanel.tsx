import type { WorldEventFixture } from "../fixture/types";

export interface WhyFactor {
  text: string;
  linkedEventId?: string;
}

export interface WhyPanelProps {
  factors: WhyFactor[];
  onFactorClick: (eventId: string) => void;
  /** Debug Mode (doc#116) — mostra os campos técnicos do evento ligado em vez de só o texto
   * humano, sem mudar a navegação. */
  debug?: boolean;
  events?: WorldEventFixture[];
}

/**
 * Painel de motivos em linguagem humana (doc#114) — "household food is low", "grain prices
 * rose", "she is hungry". Fatores com `linkedEventId` são clicáveis e abrem o Causal Explorer
 * nesse evento (spec P1 AC5-6).
 */
export function WhyPanel({ factors, onFactorClick, debug = false, events = [] }: WhyPanelProps) {
  return (
    <ul data-testid="why-panel">
      {factors.map((factor, index) => {
        const linkedEvent = factor.linkedEventId ? events.find((e) => e.eventId === factor.linkedEventId) : undefined;
        return (
          <li key={index}>
            {factor.linkedEventId ? (
              <button type="button" onClick={() => onFactorClick(factor.linkedEventId!)}>
                {debug && linkedEvent ? (
                  <span data-testid="why-factor-debug">
                    {linkedEvent.eventId} · {linkedEvent.kind} · {linkedEvent.sourceSystem} · {linkedEvent.tick}
                  </span>
                ) : (
                  factor.text
                )}
              </button>
            ) : (
              <span>{factor.text}</span>
            )}
          </li>
        );
      })}
    </ul>
  );
}
