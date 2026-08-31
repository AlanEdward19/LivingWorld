import { useSyncExternalStore } from "react";
import type { WorldFixture } from "../fixture/types";
import { NavigationStore, routeToPath, type Route } from "../nav/NavigationStore";

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
    case "building":
      return fixture.settlements.flatMap((s) => s.buildings).find((b) => b.id === route.id)?.name ?? route.id;
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
 * Breadcrumb visível em toda tela, lendo `NavigationStore.breadcrumb()` — só rotas de
 * localização (World/Settlement/Building), cada uma clicável pra saltar direto de volta
 * (design.md § Architecture — fonte única de verdade de navegação). Assina `current()` (não
 * `breadcrumb()`) pra também re-renderizar quando só um overlay não-espacial (causal/timeline/
 * agent/household/...) muda por cima da localização, já que esses nunca tocam a pilha de
 * localização em si. Botão voltar chama `nav.back()`, preservando o estado da tela anterior em
 * vez de resetar pro World View (spec P1 AC8).
 */
export function Breadcrumb({ fixture, nav }: BreadcrumbProps) {
  const current = useSyncExternalStore(
    (listener) => nav.subscribe(listener),
    () => nav.current(),
  );
  const breadcrumb = nav.breadcrumb();
  const currentPath = routeToPath(current);

  return (
    <nav data-testid="breadcrumb">
      <ol>
        {breadcrumb.map((route, index) => {
          const isCurrent = routeToPath(route) === currentPath;
          return (
            <li key={index}>
              {isCurrent ? (
                labelFor(route, fixture)
              ) : (
                <button type="button" onClick={() => nav.goTo(route)}>
                  {labelFor(route, fixture)}
                </button>
              )}
            </li>
          );
        })}
      </ol>
      {nav.canGoBack() && (
        <button type="button" onClick={() => nav.back()}>
          Back
        </button>
      )}
    </nav>
  );
}
