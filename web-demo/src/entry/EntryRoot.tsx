import { useEffect, useMemo, useState, useSyncExternalStore } from "react";
import { EntryRouter } from "./EntryRouter";
import { EntryServicesProvider, useEntryServices } from "./EntryContext";
import { MainMenu } from "./MainMenu";
import { WorldLibrary } from "./WorldLibrary";
import { SettingsView } from "./SettingsView";
import { WorldCreatorShell } from "./creator/WorldCreatorShell";
import { NotFoundScreen } from "./WorldNotFound";
import { App } from "../App";

const router = new EntryRouter();

function WorldScreen({ worldId, onNavigate }: { worldId: string; onNavigate: (path: string) => void }) {
  const { worlds } = useEntryServices();
  const [exists, setExists] = useState<boolean | null>(null);

  useEffect(() => {
    let cancelled = false;
    setExists(null);
    worlds.getWorld(worldId).then((w) => {
      if (!cancelled) setExists(w !== null);
    });
    return () => {
      cancelled = true;
    };
  }, [worldId, worlds]);

  if (exists === null) return null;
  if (!exists) return <NotFoundScreen kind="world" onNavigate={onNavigate} />;

  // ponytail: single-mock-world demo — the existing shell owns full pathname navigation from
  // here on (see plan's "don't touch NavigationStore" decision), so entering a world doesn't
  // nest `App`'s own routes under `/worlds/:worldId`.
  return <App />;
}

/** Composition root: routes between the entry experience (Main Menu/Create/Worlds/Settings) and the existing world shell. */
export function EntryRoot() {
  useEffect(() => {
    router.start();
    return () => router.stop();
  }, []);

  const screen = useSyncExternalStore(
    (listener) => router.subscribe(listener),
    () => router.current(),
  );

  const navigate = useMemo(() => (path: string) => router.navigate(path), []);

  return (
    <EntryServicesProvider>
      {screen.kind === "main-menu" && <MainMenu onNavigate={navigate} />}
      {screen.kind === "create" && <WorldCreatorShell draftId={screen.draftId} onNavigate={navigate} />}
      {screen.kind === "worlds" && <WorldLibrary onNavigate={navigate} />}
      {screen.kind === "settings" && <SettingsView onNavigate={navigate} />}
      {screen.kind === "world" && <WorldScreen worldId={screen.worldId} onNavigate={navigate} />}
    </EntryServicesProvider>
  );
}
