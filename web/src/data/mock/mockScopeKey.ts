// Fase 15.1, T0: chave de indexação interna dos mocks, só para casar fixture -> SpaceId.
// Não é "a" implementação canônica de scope-key do cliente — essa é responsabilidade de T9
// (SpatialContext), que vai reconciliar SpaceId com o FocusScope/focusScopeKey existente
// (web/src/types.ts:136-145). Mantido local e não reexportado fora de web/src/data/mock.
import type { SpaceId } from "../../map-engine/types";

export function mockScopeKey(space: SpaceId): string {
  switch (space.kind) {
    case "World":
      return "world";
    case "City":
      return `city:${space.cityId}`;
    case "Building":
      return `building:${space.buildingId}`;
  }
}
