import { SimulationStore } from "./state/simulationStore";
import { ViewStore } from "./state/viewStore";
import { SelectionStore } from "./state/selectionStore";
import { RealSnapshotSource } from "./data/real/snapshotSource";
import { RealTickStreamSource } from "./data/real/tickStreamSource";
import { RealTimeControlSource } from "./data/real/timeControlSource";
import { RealPortalSource } from "./data/real/portalSource";
import { RealNpcInspectionSource } from "./data/real/npcInspectionSource";
import { RealBiographySource } from "./data/real/biographySource";
import { RealChronicleSource } from "./data/real/chronicleSource";
import { RealConversationSource } from "./data/real/conversationSource";
import { RealAuthoringSource } from "./data/real/authoringSource";
import { mountApp } from "./bootstrap";
import "./styles/global.css";

// Fase 15.1, Estágio 3 (T31-T33) + T27: composition root de produção — só nomeia `Real*Source`.
// O modo de demo offline (`Mock*Source`) vive em `demo.tsx`, um entry point separado
// (`demo.html`), nunca alcançável a partir deste bundle.
const simulationStore = new SimulationStore(
  new RealSnapshotSource(),
  new RealTickStreamSource(),
  new RealNpcInspectionSource(),
);
const viewStore = new ViewStore(new RealPortalSource(simulationStore));
const selectionStore = new SelectionStore();
const timeControlSource = new RealTimeControlSource();
const narrativeSources = {
  biography: new RealBiographySource(),
  chronicle: new RealChronicleSource(),
  conversation: new RealConversationSource(),
};

mountApp({ simulationStore, viewStore, selectionStore, timeControlSource, narrativeSources, authoringSource: new RealAuthoringSource() });
