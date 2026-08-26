import { useEffect, useSyncExternalStore } from "react";
import { WORLD_FIXTURE } from "./fixture/oakbridge";
import { NavigationStore } from "./nav/NavigationStore";
import { Breadcrumb } from "./components/Breadcrumb";
import { SearchBar } from "./components/SearchBar";
import { WorldView } from "./views/WorldView";
import { SettlementView } from "./views/SettlementView";
import { HouseholdView } from "./views/HouseholdView";
import { AgentView } from "./views/AgentView";
import { CausalExplorer } from "./views/CausalExplorer";
import { Timeline } from "./views/Timeline";
import { LifeView } from "./views/LifeView";
import { WorldFeed } from "./views/WorldFeed";
import { StoryThreads } from "./views/StoryThreads";

const nav = new NavigationStore(WORLD_FIXTURE);

/**
 * Composition root da demo (design.md § Architecture: Router troca de view por
 * `NavigationStore.current().kind`). Único ponto que monta o `NavigationStore` de verdade e
 * escuta a URL (T12) — todas as views/telas de P1/P2/P3 são só consumidoras.
 */
export function App() {
  useEffect(() => {
    nav.syncWithHistory();
    return () => nav.stopSyncWithHistory();
  }, []);

  const route = useSyncExternalStore(
    (listener) => nav.subscribe(listener),
    () => nav.current(),
  );

  return (
    <div>
      <Breadcrumb fixture={WORLD_FIXTURE} nav={nav} />
      <SearchBar fixture={WORLD_FIXTURE} nav={nav} />

      {route.kind === "world" && <WorldView fixture={WORLD_FIXTURE} nav={nav} />}
      {route.kind === "settlement" && <SettlementView fixture={WORLD_FIXTURE} nav={nav} settlementId={route.id} />}
      {route.kind === "household" && <HouseholdView fixture={WORLD_FIXTURE} nav={nav} householdId={route.id} />}
      {route.kind === "agent" && <AgentView fixture={WORLD_FIXTURE} nav={nav} agentId={route.id} />}
      {route.kind === "causal" && <CausalExplorer fixture={WORLD_FIXTURE} nav={nav} eventId={route.eventId} />}
      {route.kind === "timeline" && <Timeline fixture={WORLD_FIXTURE} scope={route.scope} />}
      {route.kind === "life" && <LifeView fixture={WORLD_FIXTURE} agentId={route.agentId} />}
      {route.kind === "feed" && <WorldFeed fixture={WORLD_FIXTURE} />}
      {(route.kind === "threads" || route.kind === "thread") && <StoryThreads fixture={WORLD_FIXTURE} nav={nav} />}
    </div>
  );
}
