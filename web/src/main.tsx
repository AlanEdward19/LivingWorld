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
import { RealSnapshotSource } from "./data/real/snapshotSource";
import { RealTickStreamSource } from "./data/real/tickStreamSource";
import { RealTimeControlSource } from "./data/real/timeControlSource";
import { RealPortalSource } from "./data/real/portalSource";
import { npcsByScope, portalFixtures, snapshotsByScope } from "./data/mock/fixtures";
import "./styles/global.css";

// Fase 15.1, Estágio 1 (T14) + T31: composition root — o ÚNICO arquivo autorizado a nomear
// `Mock*Source`/`Real*Source` (design.md "Mock Adapter / Validação offline do frontend"). Por
// padrão o app fala com o backend real; `VITE_DEMO_MODE=true` liga os mocks para o modo de demo
// offline (spec.md T27) sem nenhum arquivo de store/componente mudar.
const demoMode = import.meta.env.VITE_DEMO_MODE === "true";

const clock = new MockClock();
clock.setSpeed(2);

const simulationStore = new SimulationStore(
  demoMode ? new MockSnapshotSource(snapshotsByScope) : new RealSnapshotSource(),
  demoMode ? new MockTickStreamSource(clock, npcsByScope) : new RealTickStreamSource(),
);
const viewStore = new ViewStore(demoMode ? new MockPortalSource(portalFixtures) : new RealPortalSource(simulationStore));
const selectionStore = new SelectionStore();
const timeControlSource = demoMode ? new MockTimeControlSource(clock) : new RealTimeControlSource();

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
