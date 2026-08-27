import { useEffect, useState, useSyncExternalStore } from "react";
import { WORLD_FIXTURE } from "./fixture/oakbridge";
import { NavigationStore } from "./nav/NavigationStore";
import { followStore } from "./state/followStore";
import { TopBar } from "./components/TopBar";
import { Explorer } from "./components/Explorer";
import { CenterStage } from "./components/CenterStage";
import { Inspector } from "./components/Inspector";
import { TimelineBar } from "./components/TimelineBar";
import { CriticalEventToast } from "./components/CriticalEventToast";

const nav = new NavigationStore(WORLD_FIXTURE);

/** Entidade da rota atual que "Follow" (F) alterna, se houver uma (doc §148). */
function currentFollowTargetId(route: ReturnType<NavigationStore["current"]>): string | undefined {
  return route.kind === "settlement" || route.kind === "household" || route.kind === "agent" ? route.id : undefined;
}

const KEYBOARD_SHORTCUTS: { key: string; label: string }[] = [
  { key: "F", label: "Follow the currently selected entity" },
  { key: "W", label: "World View" },
  { key: "/", label: "Focus search" },
  { key: "?", label: "Toggle this help" },
];

/**
 * Composition root da demo — o shell de 1 janela só (doc §5): Top Bar / Explorer + World +
 * Inspector / Timeline. `NavigationStore.current()` decide o que aparece em cada região;
 * nenhuma view guarda estado de navegação próprio (design.md § Architecture).
 */
export function App() {
  const [helpOpen, setHelpOpen] = useState(false);
  const [toastDismissed, setToastDismissed] = useState(false);

  useEffect(() => {
    nav.syncWithHistory();
    return () => nav.stopSyncWithHistory();
  }, []);

  const route = useSyncExternalStore(
    (listener) => nav.subscribe(listener),
    () => nav.current(),
  );

  // § 148 Keyboard Navigation — só os atalhos que têm uma ação real nesta demo (Sim Controls
  // são desabilitados, então Space/1/2/3 não têm nada real pra fazer e ficam de fora). Evita
  // conflito com inputs (§148 nota final): ignora quando o foco já está num campo de texto.
  useEffect(() => {
    function onKeyDown(event: KeyboardEvent) {
      const target = event.target as HTMLElement | null;
      if (target && (target.tagName === "INPUT" || target.tagName === "TEXTAREA")) return;

      if (event.key === "w" || event.key === "W") {
        nav.push({ kind: "world" });
      } else if (event.key === "f" || event.key === "F") {
        const targetId = currentFollowTargetId(nav.current());
        if (targetId) followStore.toggleFollow(targetId);
      } else if (event.key === "/") {
        event.preventDefault();
        document.querySelector<HTMLInputElement>('[data-testid="search-input"]')?.focus();
      } else if (event.key === "?") {
        setHelpOpen((open) => !open);
      }
    }
    window.addEventListener("keydown", onKeyDown);
    return () => window.removeEventListener("keydown", onKeyDown);
  }, []);

  return (
    <div data-testid="shell">
      <TopBar fixture={WORLD_FIXTURE} nav={nav} />
      <div data-testid="shell-body">
        <Explorer fixture={WORLD_FIXTURE} nav={nav} />
        <CenterStage fixture={WORLD_FIXTURE} nav={nav} route={route} />
        <Inspector fixture={WORLD_FIXTURE} nav={nav} route={route} />
      </div>
      <TimelineBar fixture={WORLD_FIXTURE} />

      {!toastDismissed && <CriticalEventToast fixture={WORLD_FIXTURE} nav={nav} onDismiss={() => setToastDismissed(true)} />}

      {helpOpen && (
        <div data-testid="keyboard-help" role="dialog" aria-label="Keyboard shortcuts">
          <h3>Keyboard shortcuts</h3>
          <ul>
            {KEYBOARD_SHORTCUTS.map((shortcut) => (
              <li key={shortcut.key}>
                <kbd>{shortcut.key}</kbd> {shortcut.label}
              </li>
            ))}
          </ul>
          <button type="button" onClick={() => setHelpOpen(false)}>
            Close
          </button>
        </div>
      )}
    </div>
  );
}
