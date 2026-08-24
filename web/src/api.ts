import type { FocusScope, VisualSnapshotEnvelope, ViewerMode } from "./types";
import type {
  ConversationSendOutcome,
  ConversationStartOutcome,
  ConversationTurn,
  NarrativeProse,
  NpcInspection,
} from "./data/contracts";
import type { PersonalityValues, PowerCatalogItem } from "./data/sources";

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

export async function fetchNpcInspection(npcId: number): Promise<NpcInspection | null> {
  const response = await fetch(`${apiBaseUrl()}/npcs/${npcId}`);
  if (response.status === 404) return null;
  if (!response.ok) throw new Error(`inspeção de NPC falhou: ${response.status}`);
  return (await response.json()) as NpcInspection;
}

/** GET /npcs/{id} nunca materializa (por design — G9); a maioria dos NPCs clicados no mapa ainda
 * está só no pool agregado da cidade, então o clique precisa desta rota explícita pra virar um
 * registro individual inspecionável. */
export async function materializeNpc(npcId: number): Promise<NpcInspection | null> {
  const response = await fetch(`${apiBaseUrl()}/npcs/${npcId}/materialize`, { method: "POST" });
  if (response.status === 404) return null;
  if (!response.ok) throw new Error(`materialização de NPC falhou: ${response.status}`);
  return (await response.json()) as NpcInspection;
}

async function authoringRequest(path: string, method: string, body?: unknown): Promise<void> {
  const response = await fetch(`${apiBaseUrl()}${path}`, {
    method,
    headers: body === undefined ? undefined : { "Content-Type": "application/json" },
    body: body === undefined ? undefined : JSON.stringify(body),
  });
  if (!response.ok) {
    const payload = await response.json().catch(() => ({})) as { error?: string };
    throw new Error(payload.error ?? `comando de autoria falhou: ${response.status}`);
  }
}

export async function fetchPowerCatalog(): Promise<PowerCatalogItem[]> {
  const response = await fetch(`${apiBaseUrl()}/authoring/extraordinary/catalog`);
  if (!response.ok) throw new Error(`catálogo extraordinário falhou: ${response.status}`);
  return (await response.json()) as PowerCatalogItem[];
}

export const grantNpcPower = (npcId: number, powerId: string) =>
  authoringRequest(`/authoring/npcs/${npcId}/extraordinary/grant`, "POST", { powerId });
export const revokeNpcPower = (npcId: number, powerId: string) =>
  authoringRequest(`/authoring/npcs/${npcId}/extraordinary/revoke`, "POST", { powerId });
export const invokeNpcPower = (
  npcId: number, powerId: string, targetNpcId: number, targetCell?: { x: number; y: number }, resolution?: number,
) => authoringRequest(`/authoring/npcs/${npcId}/extraordinary/invoke`, "POST", { powerId, targetNpcId, targetCell, resolution });
export const rewriteNpcPersonality = (npcId: number, personality: PersonalityValues) =>
  authoringRequest(`/authoring/npcs/${npcId}/personality`, "PUT", personality);
export const breakNpcRelationships = (npcId: number, otherNpcId: number) =>
  authoringRequest(`/authoring/npcs/${npcId}/relationships/break`, "POST", { otherNpcId });
export const forceNpcAction = (npcId: number, action: number) =>
  authoringRequest(`/authoring/npcs/${npcId}/action`, "POST", { action });

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
/// `name` é obrigatório no backend (`WorldCreateEndpoints.cs`: `Name é obrigatório.` em 400
/// sem ele) — bug real corrigido aqui: este helper nunca enviava o campo, então todo create
/// falhava com 400 independente do que o usuário digitasse na tela de criação.
export async function createWorld(scenarioJson: string, name: string): Promise<Response> {
  return fetch(`${apiBaseUrl()}/worlds/create`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ scenarioJson, name }),
  });
}

export interface PeriodSummary {
  periodId: string;
  version: number;
  source: string;
  createdAtUtc: string;
  width: number;
  height: number;
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

/// Fase 15.1, T32: tradução HTTP fina de `TimeControlSource` sobre `SimulationControlEndpoints`
/// (`/simulation/{pause,resume,speed,step}` + `/simulation/status`). Erros (400 velocidade
/// inválida, 409 step fora de pausa) não lançam — o botão simplesmente não teve efeito, e o
/// `status()` chamado em seguida por `TimeControls` reflete o que o servidor de fato manteve.
export async function pauseSimulation(): Promise<void> {
  await fetch(`${apiBaseUrl()}/simulation/pause`, { method: "POST" });
}

export async function resumeSimulation(): Promise<void> {
  await fetch(`${apiBaseUrl()}/simulation/resume`, { method: "POST" });
}

export async function setSimulationSpeed(ticksPerSecond: number): Promise<void> {
  await fetch(`${apiBaseUrl()}/simulation/speed`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ ticksPerSecond }),
  });
}

export async function stepSimulation(): Promise<void> {
  await fetch(`${apiBaseUrl()}/simulation/step`, { method: "POST" });
}

export async function advanceSimulationYear(): Promise<void> {
  await fetch(`${apiBaseUrl()}/simulation/advance-year`, { method: "POST" });
}

export interface SimulationStatusDto {
  isPaused: boolean;
  ticksPerSecond: number;
  tick: number;
  year: number;
}

export async function fetchSimulationStatus(): Promise<SimulationStatusDto> {
  const response = await fetch(`${apiBaseUrl()}/simulation/status`);
  if (!response.ok) throw new Error(`status da simulação falhou: ${response.status}`);
  return (await response.json()) as SimulationStatusDto;
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

/// Fase 15.1, T7 (LWV-05): `GET /narratives/biographies/{npcId}` já pronto (Fase 12, T7) — este
/// helper só traduz request/response, mesmo padrão de `fetchNpcInspection`. 404 == sem timeline
/// ainda (NPC sem fatos registrados), nunca um erro.
export async function fetchBiography(npcId: number): Promise<NarrativeProse | null> {
  const response = await fetch(`${apiBaseUrl()}/narratives/biographies/${npcId}`);
  if (response.status === 404) return null;
  if (!response.ok) throw new Error(`biografia falhou: ${response.status}`);
  return (await response.json()) as NarrativeProse;
}

export async function fetchChronicle(
  cityId: string, periodStart: number, periodEnd: number,
): Promise<NarrativeProse> {
  const params = new URLSearchParams({
    location: cityId, periodStart: String(periodStart), periodEnd: String(periodEnd),
  });
  const response = await fetch(`${apiBaseUrl()}/narratives/chronicles?${params.toString()}`);
  if (!response.ok) throw new Error(`crônica falhou: ${response.status}`);
  return (await response.json()) as NarrativeProse;
}

/// `POST /conversations/start|send|end` já prontos (Fase 11, T7) — validação/fallback rodam
/// inteiramente no servidor (`ConversationOrchestrator`); estes helpers só traduzem
/// request/response, sem lógica de decisão nova.
export async function startConversation(npcId: number): Promise<ConversationStartOutcome> {
  const response = await fetch(`${apiBaseUrl()}/conversations/start`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ npcId }),
  });
  if (response.status === 404) return { accepted: false, reason: "npc-not-found" };
  if (!response.ok) throw new Error(`iniciar conversa falhou: ${response.status}`);
  const body = (await response.json()) as { decision: string; sessionId: number | null };
  return body.sessionId === null
    ? { accepted: false, reason: body.decision }
    : { accepted: true, sessionId: body.sessionId };
}

export async function sendConversationMessage(sessionId: number, message: string): Promise<ConversationSendOutcome> {
  const response = await fetch(`${apiBaseUrl()}/conversations/send`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ sessionId, message }),
  });
  if (response.status === 404) return { ok: false, reason: "session-not-found" };
  if (response.status === 409) {
    const reason = await response.text();
    return { ok: false, reason: reason.includes("npc-dead") ? "npc-dead" : "session-ended" };
  }
  if (!response.ok) throw new Error(`enviar mensagem falhou: ${response.status}`);
  return { ok: true, turn: (await response.json()) as ConversationTurn };
}

export async function endConversation(sessionId: number): Promise<void> {
  await fetch(`${apiBaseUrl()}/conversations/end`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ sessionId }),
  });
}
