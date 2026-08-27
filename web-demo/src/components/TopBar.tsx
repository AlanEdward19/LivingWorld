import { useState, useSyncExternalStore } from "react";
import type { WorldFixture } from "../fixture/types";
import type { NavigationStore } from "../nav/NavigationStore";
import { Breadcrumb } from "./Breadcrumb";
import { SearchBar } from "./SearchBar";
import { followStore } from "../state/followStore";

export interface TopBarProps {
  fixture: WorldFixture;
  nav: NavigationStore;
}

/**
 * Menu do World Selector (doc §31) — mundo único nesta demo (Out of Scope: sem múltiplos
 * mundos/troca de fixture), então só "World Details" é uma ação real; o resto é desabilitado
 * em vez de escondido (mesmo princípio do doc §6 pro Inhabit: nunca mostrar como quebrado,
 * só deixar claro que não está disponível ainda).
 */
function WorldSelector({ fixture, nav }: { fixture: WorldFixture; nav: NavigationStore }) {
  const [open, setOpen] = useState(false);
  return (
    <div data-testid="world-selector" className="topbar-menu">
      <button type="button" onClick={() => setOpen((o) => !o)}>
        {fixture.world.name} ▾
      </button>
      {open && (
        <ul role="menu">
          <li>
            <button
              type="button"
              onClick={() => {
                nav.push({ kind: "world" });
                setOpen(false);
              }}
            >
              World Details
            </button>
          </li>
          <li>
            <button type="button" disabled title="Not available — this demo has a single fixed world">
              Switch World
            </button>
          </li>
          <li>
            <button type="button" disabled title="Not available in this demo">
              Duplicate
            </button>
          </li>
          <li>
            <button type="button" disabled title="Not available in this demo">
              Export
            </button>
          </li>
        </ul>
      )}
    </div>
  );
}

/** Mode Selector (doc §32) — só Observe existe de verdade nesta demo; Table/Inhabit ficam
 * visíveis com "Coming", nunca escondidos como quebrados (mesmo texto/princípio do doc §6). */
function ModeSelector() {
  const [open, setOpen] = useState(false);
  return (
    <div data-testid="mode-selector" className="topbar-menu">
      <button type="button" onClick={() => setOpen((o) => !o)}>
        Observe ▾
      </button>
      {open && (
        <ul role="menu">
          <li>
            <button type="button" aria-pressed="true">
              Observe
              <br />
              <small>Explore a living simulation</small>
            </button>
          </li>
          <li>
            <button type="button" disabled title="Coming later">
              Table
              <br />
              <small>Run a campaign inside this world — Coming</small>
            </button>
          </li>
          <li>
            <button type="button" disabled title="Coming later">
              Inhabit
              <br />
              <small>Take control of an Agent — Coming</small>
            </button>
          </li>
        </ul>
      )}
    </div>
  );
}

/** Simulation Controls (doc §34-35) — desabilitados: esta demo é um fixture congelado, sem
 * simulação real rodando (Out of Scope do spec.md) — nenhum destes botões teria efeito. */
function SimulationControls() {
  return (
    <div data-testid="simulation-controls" title="This demo is a frozen snapshot — no simulation is running">
      <button type="button" disabled>
        ❚❚
      </button>
      <button type="button" disabled>
        1×
      </button>
      <button type="button" disabled>
        5×
      </button>
    </div>
  );
}

/** Notifications (doc §111-112) — reais: eventos que afetam alguma entidade seguida. Sem
 * estado de "lido/não lido" (não existe conceito de sessão longa nesta demo), só contagem. */
function Notifications({ fixture, nav }: { fixture: WorldFixture; nav: NavigationStore }) {
  const followedIds = useSyncExternalStore(
    (listener) => followStore.subscribe(listener),
    () => followStore.followedIds(),
  );
  const [open, setOpen] = useState(false);

  const relevantEvents = fixture.events.filter(
    (event) =>
      followedIds.includes(event.settlementId) ||
      event.affectedAgentIds.some((id) => followedIds.includes(id)) ||
      event.affectedHouseholdIds.some((id) => followedIds.includes(id)),
  );

  if (relevantEvents.length === 0) {
    return (
      <button type="button" data-testid="notifications-button" disabled title="No events for followed entities yet">
        ●
      </button>
    );
  }

  return (
    <div data-testid="notifications-menu" className="topbar-menu">
      <button type="button" data-testid="notifications-button" onClick={() => setOpen((o) => !o)}>
        ● {relevantEvents.length}
      </button>
      {open && (
        <ul role="menu">
          {relevantEvents.map((event) => (
            <li key={event.eventId}>
              <button
                type="button"
                onClick={() => {
                  nav.push({ kind: "causal", eventId: event.eventId });
                  setOpen(false);
                }}
              >
                {event.summary}
              </button>
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}

/**
 * Top Bar (doc §29) — Logo, World selector, Mode selector, Breadcrumb, Date, Simulation
 * controls, Search, Notifications, Settings, nessa ordem. Altura 48px (doc §29).
 */
export function TopBar({ fixture, nav }: TopBarProps) {
  const lastEvent = fixture.events[fixture.events.length - 1];

  return (
    <header data-testid="top-bar">
      <button type="button" data-testid="logo" onClick={() => nav.push({ kind: "world" })}>
        ● LivingWorld
      </button>

      <WorldSelector fixture={fixture} nav={nav} />
      <ModeSelector />

      <div data-testid="topbar-breadcrumb">
        <Breadcrumb fixture={fixture} nav={nav} />
      </div>

      <span data-testid="world-date" title="Snapshot — this demo has no time travel">
        {lastEvent?.tick}
      </span>

      <SimulationControls />

      <div data-testid="topbar-search">
        <SearchBar fixture={fixture} nav={nav} />
      </div>

      <Notifications fixture={fixture} nav={nav} />

      <button type="button" data-testid="settings-button" disabled title="Not available in this demo">
        ⚙
      </button>
    </header>
  );
}
