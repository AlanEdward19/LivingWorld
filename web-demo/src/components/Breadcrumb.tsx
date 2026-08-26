import { useSyncExternalStore } from "react";
import type { WorldFixture } from "../fixture/types";
import { NavigationStore, type Route } from "../nav/NavigationStore";

export interface BreadcrumbProps {
  fixture: WorldFixture;
  nav: NavigationStore;
}

function labelFor(route: Route, fixture: WorldFixture): string {
  switch (route.kind) {
    case "world":
      return "World";
    case "settlement":
      return fixture.settlements.find((s) => s.id === route.id)?.name ?? route.id;
    case "household":
      return fixture.households.find((h) => h.id === route.id)?.name ?? route.id;
    case "agent":
      return fixture.agents.find((a) => a.id === route.id)?.name ?? route.id;
    case "causal":
      return "Causal Explorer";
    case "timeline":
      return "Timeline";
    case "life":
      return "Life";
    case "feed":
      return "World Feed";
    case "threads":
      return "Story Threads";
    case "thread":
      return fixture.storyThreads.find((t) => t.id === route.id)?.title ?? route.id;
  }
}

/**
 * Breadcrumb visível em toda tela, lendo `NavigationStore.breadcrumb()` (design.md §
 * Architecture — fonte única de verdade de navegação). Botão voltar chama `nav.back()`,
 * preservando o estado da tela anterior em vez de resetar pro World View (spec P1 AC8).
 */
export function Breadcrumb({ fixture, nav }: BreadcrumbProps) {
  const breadcrumb = useSyncExternalStore(
    (listener) => nav.subscribe(listener),
    () => nav.breadcrumb(),
  );

  return (
    <nav data-testid="breadcrumb">
      <ol>
        {breadcrumb.map((route, index) => (
          <li key={index}>{labelFor(route, fixture)}</li>
        ))}
      </ol>
      {breadcrumb.length > 1 && (
        <button type="button" onClick={() => nav.back()}>
          Back
        </button>
      )}
    </nav>
  );
}
