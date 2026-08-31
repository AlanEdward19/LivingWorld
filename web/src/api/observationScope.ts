import { apiBaseUrl } from "../api";
import type { SpaceId } from "../map-engine/types";

export interface ObservationScopeDto {
  kind: string;
  cityId?: string;
  buildingId?: string;
}

export interface ObservationScopeRequest {
  sourceId: string;
  scope: ObservationScopeDto;
}

/** Espelha `ObservationScopeDto` da API — mesmo vocabulário de `SpaceId`, sem tradução (Fase 28, LOD-04). */
export function spaceIdToObservationScope(space: SpaceId): ObservationScopeDto {
  switch (space.kind) {
    case "World":
      return { kind: "World" };
    case "City":
      return { kind: "City", cityId: space.cityId };
    case "Building":
      return { kind: "Building", cityId: space.cityId, buildingId: space.buildingId };
  }
}

export async function postObservationScope(sourceId: string, space: SpaceId): Promise<Response> {
  const body: ObservationScopeRequest = {
    sourceId,
    scope: spaceIdToObservationScope(space),
  };
  return fetch(`${apiBaseUrl()}/observation/scope`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(body),
  });
}
