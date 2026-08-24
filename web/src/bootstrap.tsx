import { createRoot } from "react-dom/client";
import { App } from "./App";
import type { SimulationStore } from "./state/simulationStore";
import type { ViewStore } from "./state/viewStore";
import type { SelectionStore } from "./state/selectionStore";
import type { AuthoringSource, NarrativeSources, TimeControlSource } from "./data/sources";

export interface AppDependencies {
  simulationStore: SimulationStore;
  viewStore: ViewStore;
  selectionStore: SelectionStore;
  timeControlSource: TimeControlSource;
  narrativeSources?: NarrativeSources;
  authoringSource?: AuthoringSource;
}

// Fase 15.1, T27: montagem de React compartilhada entre `main.tsx` (produção, `Real*Source`) e
// `demo.tsx` (demo offline, `Mock*Source`) — os dois composition roots só diferem em QUE fontes
// injetam, nunca em como o React monta. Sem StrictMode: efeitos duplo-invocados em dev
// chamariam `observeSpace` duas vezes quase juntas, reabrindo o WebSocket duplicado que a Fase
// 15 já teve.
export function mountApp(deps: AppDependencies): void {
  createRoot(document.getElementById("root")!).render(
    <App
      simulationStore={deps.simulationStore}
      viewStore={deps.viewStore}
      selectionStore={deps.selectionStore}
      timeControlSource={deps.timeControlSource}
      narrativeSources={deps.narrativeSources}
      authoringSource={deps.authoringSource}
    />,
  );
}
