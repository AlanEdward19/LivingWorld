// Fase 15.1, T31: conversão de `SpaceId` (map-engine, T9) para `FocusScope` (types.ts, T8) — as
// fontes reais (`api.ts`) só entendem `FocusScope`; `ViewStore`/`SimulationStore` só entendem
// `SpaceId`. Reconciliação isolada aqui para não duplicar o `switch` em cada fonte real.
import type { SpaceId } from "../../map-engine/types";
import type { FocusScope } from "../../types";

export function spaceIdToFocusScope(space: SpaceId): FocusScope {
  switch (space.kind) {
    case "World":
      return { kind: "World" };
    case "City":
      return { kind: "City", cityId: space.cityId };
    case "Building":
      return { kind: "Interior", buildingId: space.buildingId, cityId: space.cityId };
  }
}
