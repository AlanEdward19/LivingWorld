import { useEffect, useState, useSyncExternalStore } from "react";
import { WorldMapView } from "./components/WorldMapView";
import { CityView } from "./components/CityView";
import { InteriorView } from "./components/InteriorView";
import { CreateWorldForm } from "./components/CreateWorldForm";
import { StartMenu } from "./components/StartMenu";
import { SettingsView } from "./components/SettingsView";
import { Breadcrumb } from "./components/Breadcrumb";
import { SpaceTransition } from "./components/SpaceTransition";
import { EntityInspector } from "./components/inspector/EntityInspector";
import { TimeControls } from "./components/TimeControls";
import { toScopeKey } from "./map-engine/space";
import type { SpaceId } from "./map-engine/types";
import type { SimulationStore } from "./state/simulationStore";
import type { ViewStore } from "./state/viewStore";
import type { SelectionStore } from "./state/selectionStore";
import type { TimeControlSource } from "./data/sources";
import type { CitySnapshot, GlobalSnapshot, InteriorSnapshot } from "./types";

type Screen = "start" | "world" | "settings";

const WORLD: SpaceId = { kind: "World" };

export interface AppProps {
  simulationStore: SimulationStore;
  viewStore: ViewStore;
  selectionStore: SelectionStore;
  timeControlSource: TimeControlSource;
}

/// Fase 15.1, T14: `App` deixa de gerenciar `focus`/conexão realtime própria — o espaço
/// observado vem do `ViewStore` (`useSyncExternalStore`, só re-renderiza quando o espaço de
/// fato muda de referência) e os dados do `SimulationStore`, ambos injetados pelo composition
/// root (`main.tsx`). Nenhum store/componente daqui em diante importa `Mock*Source`.
export function App({ simulationStore, viewStore, selectionStore, timeControlSource }: AppProps) {
  const [screen, setScreen] = useState<Screen>("start");
  const [creatingWorld, setCreatingWorld] = useState(false);

  const space = useSyncExternalStore(
    (onStoreChange) => viewStore.subscribe(onStoreChange),
    () => viewStore.currentSpace(),
  );
  const payload = useSyncExternalStore(
    (onStoreChange) => simulationStore.subscribe(onStoreChange),
    () => simulationStore.currentPayload<GlobalSnapshot | CitySnapshot | InteriorSnapshot>(space),
  );

  useEffect(() => {
    if (screen !== "world" || creatingWorld) {
      return;
    }
    void simulationStore.observeSpace(space);
  }, [screen, creatingWorld, space, simulationStore]);

  const viewport = { width: window.innerWidth, height: window.innerHeight - 40 };

  if (screen === "start") {
    return (
      <div className="app-shell">
        <StartMenu
          onContinue={() => setScreen("world")}
          onCreateWorld={() => {
            setCreatingWorld(true);
            setScreen("world");
          }}
          onSettings={() => setScreen("settings")}
        />
      </div>
    );
  }

  if (screen === "settings") {
    return (
      <div className="app-shell">
        <main>
          <SettingsView onBack={() => setScreen("start")} />
        </main>
      </div>
    );
  }

  return (
    <div className="app-shell">
      <header className="hud-bar">
        <button type="button" onClick={() => setScreen("start")}>
          ☰ menu
        </button>
        <button type="button" onClick={() => setCreatingWorld((v) => !v)}>
          {creatingWorld ? "Cancelar" : "Criar mundo"}
        </button>
        {!creatingWorld && <TimeControls timeControlSource={timeControlSource} />}
      </header>

      <main className={creatingWorld ? "" : "fullbleed"}>
        {creatingWorld && (
          <CreateWorldForm
            onCreated={() => {
              setCreatingWorld(false);
              viewStore.goToAncestor(WORLD);
            }}
          />
        )}

        {!creatingWorld && (
          <>
            <Breadcrumb space={space} onNavigate={(target) => viewStore.goToAncestor(target)} />
            <SpaceTransition spaceKey={toScopeKey(space)}>
              {!payload && <p className="map-hud map-hud-top-left">Carregando…</p>}

              {payload && space.kind === "World" && (
                <WorldMapView
                  snapshot={payload as GlobalSnapshot}
                  viewport={viewport}
                  simulationStore={simulationStore}
                  viewStore={viewStore}
                  selectionStore={selectionStore}
                />
              )}

              {payload && space.kind === "City" && (
                <CityView
                  snapshot={payload as CitySnapshot}
                  viewport={viewport}
                  simulationStore={simulationStore}
                  viewStore={viewStore}
                  selectionStore={selectionStore}
                />
              )}

              {payload && space.kind === "Building" && (
                <InteriorView
                  snapshot={payload as InteriorSnapshot}
                  onBack={() => viewStore.goToAncestor({ kind: "City", cityId: space.cityId })}
                />
              )}
            </SpaceTransition>
            <EntityInspector
              selectionStore={selectionStore}
              simulationStore={simulationStore}
              viewStore={viewStore}
            />
          </>
        )}
      </main>
    </div>
  );
}
