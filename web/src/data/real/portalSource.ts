// Fase 15.1, T33: implementação real de `PortalSource` — lê o campo `portals` já embutido no
// snapshot corrente do `SimulationStore` (`GlobalSnapshot`/`CitySnapshot`, T21), sem request
// própria. `ViewStore` sempre chama `portalsOf(currentSpace())`, e o snapshot desse espaço já foi
// carregado por `observeSpace` antes de qualquer navegação — nada aqui dispara fetch/WebSocket.
import type { PortalSource } from "../sources";
import type { SpatialPortalDto } from "../contracts";
import type { SpaceId } from "../../map-engine/types";
import type { SimulationStore } from "../../state/simulationStore";

interface SnapshotWithPortals {
  portals: SpatialPortalDto[];
}

export class RealPortalSource implements PortalSource {
  constructor(private readonly simulationStore: SimulationStore) {}

  portalsOf(space: SpaceId): SpatialPortalDto[] {
    return this.simulationStore.currentPayload<SnapshotWithPortals>(space)?.portals ?? [];
  }
}
