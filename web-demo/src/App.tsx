import { useEffect, useSyncExternalStore } from "react";
import { WORLD_FIXTURE } from "./fixture/oakbridge";
import { NavigationStore } from "./nav/NavigationStore";
import { TopBar } from "./components/TopBar";
import { Explorer } from "./components/Explorer";
import { CenterStage } from "./components/CenterStage";
import { Inspector } from "./components/Inspector";
import { TimelineBar } from "./components/TimelineBar";

const nav = new NavigationStore(WORLD_FIXTURE);

/**
 * Composition root da demo — o shell de 1 janela só (doc §5): Top Bar / Explorer + World +
 * Inspector / Timeline. `NavigationStore.current()` decide o que aparece em cada região;
 * nenhuma view guarda estado de navegação próprio (design.md § Architecture).
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
    <div data-testid="shell">
      <TopBar fixture={WORLD_FIXTURE} nav={nav} />
      <div data-testid="shell-body">
        <Explorer fixture={WORLD_FIXTURE} nav={nav} />
        <CenterStage fixture={WORLD_FIXTURE} nav={nav} route={route} />
        <Inspector fixture={WORLD_FIXTURE} nav={nav} route={route} />
      </div>
      <TimelineBar fixture={WORLD_FIXTURE} />
    </div>
  );
}
