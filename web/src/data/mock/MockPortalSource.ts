// Fase 15.1, T0: implementação mock de `PortalSource` — filtra a lista fixa de portais pelas
// origens/destinos que tocam o espaço consultado. Mesma interface que a projeção real (T33)
// vai satisfazer a partir do campo `Portals` de `GlobalSnapshot`/`CitySnapshot`.
import type { PortalSource } from "../sources";
import type { PortalEndpointDto, SpatialPortalDto } from "../contracts";
import type { SpaceId } from "../../map-engine/types";

function endpointMatchesSpace(endpoint: PortalEndpointDto, space: SpaceId): boolean {
  if (endpoint.space === "World" && space.kind === "World") {
    return true;
  }
  if (endpoint.space === "City" && space.kind === "City") {
    return endpoint.refId === space.cityId;
  }
  if (endpoint.space === "Building" && space.kind === "Building") {
    return endpoint.refId === space.buildingId;
  }
  return false;
}

export class MockPortalSource implements PortalSource {
  constructor(private readonly portals: SpatialPortalDto[]) {}

  portalsOf(space: SpaceId): SpatialPortalDto[] {
    return this.portals.filter(
      (portal) => endpointMatchesSpace(portal.from, space) || endpointMatchesSpace(portal.to, space),
    );
  }
}
