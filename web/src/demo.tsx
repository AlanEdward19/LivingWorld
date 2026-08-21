import { SimulationStore } from "./state/simulationStore";
import { ViewStore } from "./state/viewStore";
import { SelectionStore } from "./state/selectionStore";
import { MockClock } from "./data/mock/MockClock";
import { MockSnapshotSource } from "./data/mock/MockSnapshotSource";
import { MockTickStreamSource } from "./data/mock/MockTickStreamSource";
import { MockTimeControlSource } from "./data/mock/MockTimeControlSource";
import { MockPortalSource } from "./data/mock/MockPortalSource";
import { npcsByScope, portalFixtures, snapshotsByScope } from "./data/mock/fixtures";
import { MockNpcInspectionSource } from "./data/mock/MockNpcInspectionSource";
import { MockBiographySource } from "./data/mock/MockBiographySource";
import { MockChronicleSource } from "./data/mock/MockChronicleSource";
import { MockConversationSource } from "./data/mock/MockConversationSource";
import { mountApp } from "./bootstrap";
import "./styles/global.css";

// Fase 15.1, T27 (antes em main.tsx, T14/T29): composition root do modo de demo offline — o
// ÚNICO arquivo de produção autorizado a nomear `Mock*Source` (design.md "Mock Adapter /
// Validação offline do frontend"). Só alcançável abrindo `demo.html`; `index.html` (build de
// produção) aponta pra `main.tsx`, que só nomeia `Real*Source`.
const clock = new MockClock();
clock.setSpeed(2);

const simulationStore = new SimulationStore(
  new MockSnapshotSource(snapshotsByScope),
  new MockTickStreamSource(clock, npcsByScope),
  new MockNpcInspectionSource(new Map()),
);
const viewStore = new ViewStore(new MockPortalSource(portalFixtures));
const selectionStore = new SelectionStore();
const timeControlSource = new MockTimeControlSource(clock);
const narrativeSources = {
  biography: new MockBiographySource(new Map()),
  chronicle: new MockChronicleSource(new Map()),
  conversation: new MockConversationSource(),
};

mountApp({ simulationStore, viewStore, selectionStore, timeControlSource, narrativeSources });
