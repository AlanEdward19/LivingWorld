import { createRoot } from "react-dom/client";
import { App } from "./App";
import { SimulationStore } from "./state/simulationStore";
import { ViewStore } from "./state/viewStore";
import { SelectionStore } from "./state/selectionStore";
import { MockClock } from "./data/mock/MockClock";
import { MockSnapshotSource } from "./data/mock/MockSnapshotSource";
import { MockTickStreamSource } from "./data/mock/MockTickStreamSource";
import { MockTimeControlSource } from "./data/mock/MockTimeControlSource";
import { MockPortalSource } from "./data/mock/MockPortalSource";
import { npcsByScope, portalFixtures, snapshotsByScope } from "./data/mock/fixtures";
import "./styles/global.css";

// Fase 15.1, Estágio 1 (T14): composition root — o ÚNICO arquivo autorizado a nomear
// `Mock*Source` (design.md "Mock Adapter / Validação offline do frontend"). Todo o app corre
// contra fixtures estáticas, sem WebSocket/fetch — a troca por `Real*Source` é T31/T32/T33,
// e é só isto: mudar os argumentos abaixo, nenhuma linha de store/componente muda.
const clock = new MockClock();
clock.setSpeed(2);

const simulationStore = new SimulationStore(
  new MockSnapshotSource(snapshotsByScope),
  new MockTickStreamSource(clock, npcsByScope),
);
const viewStore = new ViewStore(new MockPortalSource(portalFixtures));
const selectionStore = new SelectionStore();
const timeControlSource = new MockTimeControlSource(clock);

// Sem StrictMode: efeitos duplo-invocados em dev chamariam `observeSpace` duas vezes quase
// juntas — inofensivo hoje (fontes mock são idempotentes), mas evita reintroduzir o problema
// real que a Fase 15 teve com WebSocket duplicado quando T31 trocar pela fonte real.
createRoot(document.getElementById("root")!).render(
  <App
    simulationStore={simulationStore}
    viewStore={viewStore}
    selectionStore={selectionStore}
    timeControlSource={timeControlSource}
  />,
);
