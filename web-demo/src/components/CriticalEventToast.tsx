import type { WorldFixture } from "../fixture/types";
import type { NavigationStore } from "../nav/NavigationStore";

export interface CriticalEventToastProps {
  fixture: WorldFixture;
  nav: NavigationStore;
  onDismiss: () => void;
}

/**
 * Important Event Presentation (doc §172) — toast pra evento severity "critical"
 * ("KING ARVEN HAS DIED" no exemplo do doc), sem bloquear a tela. Nesta demo (fixture
 * congelado, sem simulação rodando) o "momento" em que isso aparece é a abertura do app —
 * mostra o evento crítico mais recente, se houver algum.
 */
export function CriticalEventToast({ fixture, nav, onDismiss }: CriticalEventToastProps) {
  const criticalEvents = fixture.events.filter((e) => e.severity === "critical");
  const event = criticalEvents[criticalEvents.length - 1];
  if (!event) return null;

  return (
    <div data-testid="critical-event-toast" role="status">
      <h3>{event.summary}</h3>
      <p>{event.tick}</p>
      <button
        type="button"
        onClick={() => {
          nav.push({ kind: "causal", eventId: event.eventId });
          onDismiss();
        }}
      >
        View event
      </button>
      <button type="button" onClick={onDismiss}>
        Dismiss
      </button>
    </div>
  );
}
