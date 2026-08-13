// Fase 15.1, T31: implementação real de `SnapshotSource` — GET /visual/subscribe via
// `fetchSnapshot` (api.ts), sempre Spectator (Player Mode saiu do cliente em T17). Nenhuma linha
// do `SimulationStore` muda: a interface e o formato de `VisualSnapshotEnvelope` são os mesmos
// consumidos hoje contra `MockSnapshotSource`.
import type { SnapshotSource } from "../sources";
import type { SpaceId } from "../../map-engine/types";
import type { VisualSnapshotEnvelope } from "../../types";
import { ViewerMode } from "../../types";
import { fetchSnapshot } from "../../api";
import { spaceIdToFocusScope } from "./focusScope";

export class RealSnapshotSource implements SnapshotSource {
  async load(space: SpaceId): Promise<VisualSnapshotEnvelope<unknown>> {
    return fetchSnapshot(spaceIdToFocusScope(space), ViewerMode.Spectator);
  }
}
