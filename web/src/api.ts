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

/// Feature ad-hoc "criar mundo" (AD-001): `scenarioJson` já vem pronto (montado por
/// `scenarioFormToJson`) — este helper só faz a chamada HTTP, mesmo padrão de `moveNpc`.
export async function createWorld(scenarioJson: string): Promise<Response> {
  return fetch(`${apiBaseUrl()}/worlds/create`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ scenarioJson }),
  });
}

export interface PeriodSummary {
  periodId: string;
  version: number;
  source: string;
  createdAtUtc: string;
}

/// UX pass 3: templates pra pré-popular o wizard de "criar mundo" (`DefaultPeriodSeeder.cs`
/// garante que sempre existe pelo menos um). `GET /periods` lista, `GET /periods/{id}` traz o
/// `PeriodDefinition` completo que `jsonToScenarioForm` sabe ler.
export async function listPeriodTemplates(): Promise<PeriodSummary[]> {
  const response = await fetch(`${apiBaseUrl()}/periods`);
  if (!response.ok) throw new Error(`listar templates falhou: ${response.status}`);
  return (await response.json()) as PeriodSummary[];
}

export async function fetchPeriodTemplate(id: string): Promise<Record<string, unknown>> {
  const response = await fetch(`${apiBaseUrl()}/periods/${encodeURIComponent(id)}`);
  if (!response.ok) throw new Error(`carregar template falhou: ${response.status}`);
  const body = (await response.json()) as { periodDefinition: Record<string, unknown> };
  return body.periodDefinition;
}

export interface PeriodCatalog {
  professionNames: Record<number, string>;
  skillNames: Record<number, string>;
}

/// T26: único catálogo real de id->nome no domínio (`PeriodCatalog.cs`) — condicional por
/// período (só ids com viés nomeado nesse período entram no dict). Terreno/bioma/recurso/
/// cultura/tipo-de-local/prédio não têm catálogo em lugar nenhum — não existe um endpoint
/// equivalente pra eles, então o cliente nunca finge um.
export async function fetchPeriodCatalog(id: string): Promise<PeriodCatalog> {
  const response = await fetch(`${apiBaseUrl()}/periods/${encodeURIComponent(id)}/catalog`);
  if (!response.ok) throw new Error(`carregar catálogo falhou: ${response.status}`);
  const body = (await response.json()) as { professionNames: Record<number, string>; skillNames: Record<number, string> };
  return { professionNames: body.professionNames, skillNames: body.skillNames };
}
