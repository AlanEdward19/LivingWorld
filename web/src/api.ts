import type { FocusScope, VisualSnapshotEnvelope, ViewerMode } from "./types";

// Fase 15, T8: base URL da API — em dev via proxy do Vite (mesma origem), em outros ambientes
// via VITE_API_BASE_URL. Vazio == mesma origem do host que serve o cliente.
export function apiBaseUrl(): string {
  return import.meta.env.VITE_API_BASE_URL ?? "";
}

export function focusScopeToQuery(scope: FocusScope): { scope: string; refId?: string } {
  switch (scope.kind) {
    case "World":
      return { scope: "World" };
    case "City":
      return { scope: "City", refId: scope.cityId };
    case "Interior":
      return { scope: "Interior", refId: scope.buildingId };
  }
}

export function buildSubscribeUrl(
  scope: FocusScope,
  mode: ViewerMode,
  playerNpcId?: number,
): string {
  const { scope: scopeParam, refId } = focusScopeToQuery(scope);
  const params = new URLSearchParams({ scope: scopeParam, mode: String(mode) });
  if (refId !== undefined) params.set("refId", refId);
  if (playerNpcId !== undefined) params.set("playerNpcId", String(playerNpcId));
  return `${apiBaseUrl()}/visual/subscribe?${params.toString()}`;
}

export function buildWebSocketUrl(
  scope: FocusScope,
  mode: ViewerMode,
  playerNpcId?: number,
): string {
  const base = apiBaseUrl() || window.location.origin;
  const wsBase = base.replace(/^http/, "ws");
  const { scope: scopeParam, refId } = focusScopeToQuery(scope);
  const params = new URLSearchParams({ scope: scopeParam, mode: String(mode) });
  if (refId !== undefined) params.set("refId", refId);
  if (playerNpcId !== undefined) params.set("playerNpcId", String(playerNpcId));
  return `${wsBase}/visual/ws?${params.toString()}`;
}

export async function fetchSnapshot<TPayload>(
  scope: FocusScope,
  mode: ViewerMode,
  playerNpcId?: number,
): Promise<VisualSnapshotEnvelope<TPayload>> {
  const response = await fetch(buildSubscribeUrl(scope, mode, playerNpcId));
  if (!response.ok) throw new Error(`subscribe falhou: ${response.status}`);
  return (await response.json()) as VisualSnapshotEnvelope<TPayload>;
}

export interface MoveNpcRequest {
  targetX: number;
  targetY: number;
  inputMode: "click" | "wasd";
}

export async function moveNpc(npcId: number, request: MoveNpcRequest): Promise<Response> {
  return fetch(`${apiBaseUrl()}/visual/player/${npcId}/move`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request),
  });
}
