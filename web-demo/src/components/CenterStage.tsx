import { useEffect, useRef } from "react";
import type { WorldFixture } from "../fixture/types";
import type { NavigationStore, Route } from "../nav/NavigationStore";
import { WorldStage } from "../render/WorldStage";
import { SettlementStage } from "../render/SettlementStage";
import { CausalExplorer } from "../views/CausalExplorer";
import { Timeline } from "../views/Timeline";
import { LifeView } from "../views/LifeView";
import { WorldFeed } from "../views/WorldFeed";
import { StoryThreads } from "../views/StoryThreads";

export interface CenterStageProps {
  fixture: WorldFixture;
  nav: NavigationStore;
  route: Route;
}

/** Rotas que "abrem em cima" do mapa (AD-021) em vez de substituí-lo — o usuário reportou que
 * perder a cidade/mundo de vista ao abrir Why?/Timeline/Life era desorientador. */
const OVERLAY_KINDS = new Set<Route["kind"]>(["causal", "timeline", "life", "feed", "threads", "thread"]);

/** Resolve o settlement a mostrar no Settlement View pra qualquer rota escopada a ele (doc
 * §22-23: "a cidade em si é o mapa" — household/agent/building continuam mostrando o MESMO
 * settlement, nunca uma página separada). */
function settlementIdForRoute(fixture: WorldFixture, route: Route): string | undefined {
  switch (route.kind) {
    case "settlement":
      return route.id;
    case "household":
      return fixture.households.find((h) => h.id === route.id)?.settlementId;
    case "agent":
      return fixture.agents.find((a) => a.id === route.id)?.settlementId;
    case "building":
      return fixture.settlements.find((s) => s.buildings.some((b) => b.id === route.id))?.id;
    default:
      return undefined;
  }
}

/**
 * Bug real achado ao vivo: clicar num NPC DENTRO de um prédio focado chama `onSelectAgent`, que
 * troca a rota inteira pra `{kind:"agent"}` — como `focusBuildingId` vinha só de
 * `route.kind === "building"`, isso derrubava o foco (câmera afastava, telhado voltava) junto
 * com a seleção do agent, mesmo ele continuando visualmente dentro da casa. `building` e "agent
 * inspecionado" são dois estados independentes (canvas vs. Inspector) que `Route` sozinha não
 * guarda os dois ao mesmo tempo.
 *
 * Fix ingênuo v1 (derivar de `agent.indoorLocation` sempre que a rota é "agent") causou uma
 * regressão: clicar num agent NA RUA (settlement sem prédio focado) também "puxava" a câmera
 * pra dentro da casa dele, mesmo ele não estando visualmente lá dentro no momento do clique. A
 * regra certa precisa de memória — só preserva o foco se o prédio já estava focado ANTES desse
 * agent ser selecionado (`lastFocusBuildingIdRef`), nunca cria foco novo a partir do nada.
 */
function useFocusBuildingId(fixture: WorldFixture, route: Route): string | null {
  const lastFocusBuildingIdRef = useRef<string | null>(null);

  let resolved: string | null = null;
  if (route.kind === "building") {
    resolved = route.id;
  } else if (route.kind === "agent") {
    const agent = fixture.agents.find((a) => a.id === route.id);
    if (agent?.indoorLocation && agent.indoorLocation.buildingId === lastFocusBuildingIdRef.current) {
      resolved = lastFocusBuildingIdRef.current;
    }
  }
  lastFocusBuildingIdRef.current = resolved;
  return resolved;
}

/**
 * Bug real reportado pelo usuário (2026-08-27): clicar num NPC diretamente no mapa mundi estava
 * "abrindo a cidade" dele — como toda rota `agent` resolve pro settlement dele via
 * `settlementIdForRoute`, selecionar um agent SEMPRE trocava a área espacial pro
 * `SettlementStage`, mesmo vindo do mapa mundi (onde clicar deveria só abrir a sidebar do NPC,
 * doc World Map §42-43: "click seleciona a entidade... o mapa não muda de tela imediatamente" —
 * igual já funciona clicar num agent DENTRO de um settlement, que nunca troca de settlement).
 *
 * Mesma família de bug do `useFocusBuildingId` acima: precisa de memória de qual era a área
 * espacial ANTES da seleção do agent, não pode derivar só do agent em si. Só entra na cidade dele
 * se a gente já estava DENTRO de algum settlement quando selecionou (comportamento antigo,
 * inalterado); se estava no mapa mundi, continua no mapa mundi.
 */
function useSpatialScope(fixture: WorldFixture, route: Route): string | "world" {
  // `null` = "nunca resolveu nada ainda" (primeiro render) — DIFERENTE de já ter resolvido pra
  // "world" de verdade. Bug real pego pelos testes: usar `"world"` como valor inicial fazia um
  // deep-link direto pra `/agent/:id` (sem passar pelo mapa mundi antes) tratar isso como "veio
  // do mapa mundi" e nunca entrar no settlement do agent — quebrando o deep-link que sempre
  // funcionou (mesmo comportamento de building/household: mostra o settlement de verdade).
  const lastScopeRef = useRef<string | "world" | null>(null);

  let resolved: string | "world";
  if (route.kind === "world") {
    resolved = "world";
  } else if (route.kind === "agent") {
    if (lastScopeRef.current === "world") {
      resolved = "world";
    } else {
      resolved = fixture.agents.find((a) => a.id === route.id)?.settlementId ?? lastScopeRef.current ?? "world";
    }
  } else {
    resolved = settlementIdForRoute(fixture, route) ?? lastScopeRef.current ?? "world";
  }

  lastScopeRef.current = resolved;
  return resolved;
}

function SpatialLayer({ fixture, nav, route }: CenterStageProps) {
  // Chamados incondicionalmente (regra dos hooks) — pra rota "world" os valores específicos de
  // settlement são descartados, mas as refs internas ainda precisam "ver" cada render.
  const focusBuildingId = useFocusBuildingId(fixture, route);
  const spatialScope = useSpatialScope(fixture, route);

  if (spatialScope === "world") {
    return (
      <WorldStage
        fixture={fixture}
        onSelectSettlement={(settlementId) => nav.push({ kind: "settlement", id: settlementId })}
        onSelectNpc={(agentId) => nav.push({ kind: "agent", id: agentId })}
        onBackgroundClick={() => nav.replace({ kind: "world" })}
      />
    );
  }

  const settlementId = spatialScope;
  return (
    <SettlementStage
      fixture={fixture}
      settlementId={settlementId}
      focusBuildingId={focusBuildingId}
      onSelectAgent={(agentId) => nav.replace({ kind: "agent", id: agentId })}
      onFocusBuilding={(buildingId) => nav.push({ kind: "building", id: buildingId })}
      onBackgroundClick={() => nav.replace({ kind: "settlement", id: settlementId })}
    />
  );
}

function OverlayContent({ fixture, nav, route }: CenterStageProps) {
  switch (route.kind) {
    case "causal":
      return <CausalExplorer fixture={fixture} nav={nav} eventId={route.eventId} />;
    case "timeline":
      return <Timeline fixture={fixture} scope={route.scope} />;
    case "life":
      return <LifeView fixture={fixture} agentId={route.agentId} />;
    case "feed":
      return <WorldFeed fixture={fixture} />;
    case "threads":
    case "thread":
      return <StoryThreads fixture={fixture} nav={nav} />;
    default:
      return null;
  }
}

/**
 * "World" — o centro do shell (doc §6/§92-96). O mapa (mundo ou settlement) fica SEMPRE
 * montado — Causal Explorer/Timeline/Life/Feed/Threads abrem como um painel POR CIMA dele
 * (AD-021), não substituem mais o centro: usuário reportou que perder a cidade/NPC de vista ao
 * checar "Why?"/Timeline era desorientador. Fecha com X, clique fora, ou Esc — todos chamam
 * `nav.back()` (essas rotas continuam empilhadas via `push`, então back as remove de novo).
 */
export function CenterStage({ fixture, nav, route }: CenterStageProps) {
  const isOverlay = OVERLAY_KINDS.has(route.kind);
  const spatialRoute = isOverlay ? nav.spatialContext() : route;

  useEffect(() => {
    if (!isOverlay) return undefined;
    function onKeyDown(event: KeyboardEvent) {
      if (event.key === "Escape") nav.back();
    }
    window.addEventListener("keydown", onKeyDown);
    return () => window.removeEventListener("keydown", onKeyDown);
  }, [isOverlay, nav]);

  return (
    <div data-testid="center-stage">
      <SpatialLayer fixture={fixture} nav={nav} route={spatialRoute} />
      {isOverlay && (
        <div data-testid="center-stage-overlay-backdrop" onClick={() => nav.back()}>
          <div data-testid="center-stage-overlay-panel" onClick={(event) => event.stopPropagation()}>
            <button type="button" data-testid="center-stage-overlay-close" aria-label="Close" onClick={() => nav.back()}>
              ×
            </button>
            <OverlayContent fixture={fixture} nav={nav} route={route} />
          </div>
        </div>
      )}
    </div>
  );
}
