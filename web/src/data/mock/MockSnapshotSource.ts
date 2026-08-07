// Fase 15.1, T0: implementação mock de `SnapshotSource` — resolve por fixture estática,
// indexada pela mesma chave que `TickStreamSource`/`PortalSource` usam para o mesmo escopo.
import type { SnapshotSource } from "../sources";
import type { VisualSnapshotEnvelope } from "../../types";
import type { SpaceId } from "../../map-engine/types";
import { mockScopeKey } from "./mockScopeKey";

export class MockSnapshotSource implements SnapshotSource {
  constructor(private readonly snapshotsByScope: Record<string, VisualSnapshotEnvelope<unknown>>) {}

  async load(space: SpaceId): Promise<VisualSnapshotEnvelope<unknown>> {
    const key = mockScopeKey(space);
    const found = this.snapshotsByScope[key];
    if (!found) {
      throw new Error(`no mock snapshot fixture for scope "${key}"`);
    }
    return found;
  }
}
