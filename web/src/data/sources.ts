// Fase 15.1, T0: o seam de dado (design.md "Mock Adapter / Validação offline do frontend").
// Cada interface tem exatamente uma responsabilidade e é injetada por construtor em quem a
// consome (SimulationStore, TimeControls, ViewStore). `Mock*` (Estágio 1) e `Real*` (Estágio 3)
// implementam a mesma interface; nada além do composition root (`main.tsx`) sabe qual está viva.
import type { SpaceId } from "../map-engine/types";
import type { VisualSnapshotEnvelope } from "../types";
import type {
  ConversationSendOutcome,
  ConversationStartOutcome,
  NarrativeProse,
  NpcInspection,
  ScopeTickDelta,
  SimulationStatus,
  SpatialPortalDto,
} from "./contracts";

export interface SnapshotSource {
  load(space: SpaceId): Promise<VisualSnapshotEnvelope<unknown>>;
}

export interface TickStreamSource {
  /**
   * `onDrop` (T10, VTT2-11/36): sinaliza perda de conexão do stream para este escopo — a
   * contraparte real (T31) chama isto do `onclose`/`onerror` do WebSocket. Opcional porque nem
   * toda fonte tem noção de "queda" (ex.: um replay finito de fixture nunca cai).
   */
  subscribe(space: SpaceId, onDelta: (delta: ScopeTickDelta) => void, onDrop?: () => void): () => void;
}

export interface TimeControlSource {
  pause(): Promise<void>;
  resume(): Promise<void>;
  setSpeed(ticksPerSecond: number): Promise<void>;
  step(): Promise<void>;
  status(): Promise<SimulationStatus>;
}

export interface PortalSource {
  portalsOf(space: SpaceId): SpatialPortalDto[];
}

export interface NpcInspectionSource {
  load(npcId: number): Promise<NpcInspection | null>;
}

/** Fase 15.1, T7 (LWV-05): biografia narrada de um NPC — reusa `GET /narratives/biographies/{id}`
 * já pronto (Fase 12, T7); `null` quando o NPC não existe/não tem timeline (404). */
export interface BiographySource {
  load(npcId: number): Promise<NarrativeProse | null>;
}

/** Crônica narrada de uma cidade para uma janela de ticks — reusa `GET /narratives/chronicles`. */
export interface ChronicleSource {
  load(cityId: string, periodStart: number, periodEnd: number): Promise<NarrativeProse>;
}

/** Sessão de conversa segura com um NPC — reusa `POST /conversations/{start,send,end}`
 * (Fase 11, T7). Nenhuma decisão nova aqui: o provider/validador já roda inteiramente no
 * servidor (`ConversationOrchestrator`); este seam só traduz request/response. */
export interface ConversationSource {
  start(npcId: number): Promise<ConversationStartOutcome>;
  send(sessionId: number, message: string): Promise<ConversationSendOutcome>;
  end(sessionId: number): Promise<void>;
}

/** Agrupa as três fontes de T7 num único prop opcional — evita perfurar `EntityInspector`/`App`
 * com três props separados para algo que sempre viaja junto (inspetores narrativos). */
export interface NarrativeSources {
  biography: BiographySource;
  chronicle: ChronicleSource;
  conversation: ConversationSource;
}
